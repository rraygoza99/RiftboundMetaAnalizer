using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using RiftboundMetaAnalizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace RiftboundMetaAnalizer.Application;

public class MetaService(RiftContext context, IDistributedCache cache) : IMetaService
{
    public async Task<Result<ChampionSynergyDto>> GetChampionSynergyAsync(string championId, DateTime? from = null)
    {
        var legendCard = await context.Cards.FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard is null)
        {
            return Result<ChampionSynergyDto>.Failure(new List<string> { "Legend not found." });
        }

        // Collect all variant IDs for this legend group
        var legendIds = await GetLegendGroupIdsAsync(legendCard);

        // Get deck IDs within the date range
        var deckIdsInRange = await GetDeckIdsInRangeAsync(legendIds, from);

        // Use navigation properties for more efficient query and weighting
        var synergisticRunes = await context.DeckCards
            .Include(dc => dc.Deck)
                .ThenInclude(d => d.TournamentResults)
            .Where(dc => deckIdsInRange.Contains(dc.DeckId))
            .Where(dc => dc.Card.CardType == CardType.Rune)
            .Select(dc => new 
            {
                dc.Card,
                // Weight decks with tournament results higher (lower standing is better, so 1/standing)
                Weight = dc.Deck.TournamentResults.Any() 
                         ? dc.Deck.TournamentResults.Max(tr => 10.0 / (tr.Standing + 1)) 
                         : 1.0
            })
            .ToListAsync();

        var resultCards = synergisticRunes
            .GroupBy(x => x.Card.Id)
            .Select(g => new
            {
                Card = g.First().Card,
                Score = g.Sum(x => x.Weight)
            })
            .OrderByDescending(x => x.Score)
            .Take(5)
            .Select(x => x.Card)
            .ToList();

        var metaSnapshot = await GetMetaSnapshotAsync(championId, from);

        var resultDto = new ChampionSynergyDto
        {
            ChampionId = championId,
            SynergisticCards = resultCards,
            MetaSnapshot = metaSnapshot.IsSuccess ? metaSnapshot.Value : null
        };

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
        };
        //await cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(resultDto), cacheOptions);

        return Result<ChampionSynergyDto>.Success(resultDto);
    }

    public async Task<Result<MetaSnapshotDto>> GetMetaSnapshotAsync(string championId, DateTime? from = null)
    {
        var legendCard = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);

        if (legendCard == null)
        {
            return Result<MetaSnapshotDto>.Failure(new List<string> { "Legend not found" });
        }

        // Collect all variant IDs for this legend group
        var legendIds = await GetLegendGroupIdsAsync(legendCard);

        // All decks for this legend (unfiltered) – used for play rate, top cut, standings
        var allDeckIds = await GetAllDeckIdsInRangeAsync(legendIds, from);
        var totalDecksForLegend = allDeckIds.Count;

        // Top 25% of decks by performance – used for card analysis (core, tech, synergy, energy, domain)
        var deckIds = await GetDeckIdsInRangeAsync(legendIds, from);

        // Total decks across all legends for play rate (also filtered by date range)
        var totalDecksOverall = from.HasValue
            ? await context.TournamentResults
                .Where(tr => tr.Tournament.Date >= from.Value)
                .Select(tr => tr.DeckId)
                .Distinct()
                .CountAsync()
            : await context.Decks.CountAsync();

        if (totalDecksForLegend == 0)
        {
            return Result<MetaSnapshotDto>.Success(new MetaSnapshotDto
            {
                LegendName = legendCard.Name,
                SampleSize = 0,
                AveragePlacement = null,
                PlayRate = 0,
                TopCutRate = 0,
                CoreCards = new List<CardInsightDto>(),
                TechChoices = new List<CardInsightDto>()
            });
        }

        // Calculate average tournament placement for this legend (using ALL decks)
        var standings = await context.TournamentResults
            .Where(tr => allDeckIds.Contains(tr.DeckId))
            .Select(tr => tr.Standing)
            .OrderBy(s => s)
            .ToListAsync();

        int? bestPlacement = standings.Count > 0 ? standings.First() : null;
        int? worstPlacement = standings.Count > 0 ? standings.Last() : null;

        // Trimmed average: remove best and worst, average the rest
        double? averagePlacement = null;
        if (standings.Count > 2)
        {
            var trimmed = standings.Skip(1).Take(standings.Count - 2).ToList();
            averagePlacement = Math.Round(trimmed.Average(), 2);
        }
        else if (standings.Count > 0)
        {
            averagePlacement = Math.Round(standings.Average(), 2);
        }

        // 1. Legend Play Rate
        var playRate = totalDecksOverall > 0 
            ? Math.Round((double)totalDecksForLegend / totalDecksOverall * 100, 2) 
            : 0;

        // 2. Top Cut Rate (top 4 placements)
        var topCutCount = standings.Count(s => s <= 4);
        var topCutRate = standings.Count > 0
            ? Math.Round((double)topCutCount / standings.Count * 100, 2)
            : 0;

        var filteredDeckCount = deckIds.Count;

        // 4. Energy Curve - average energy cost distribution across top-performing decks
        var energyCurve = await context.DeckCards
            .Where(dc => deckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId) && dc.Card.EnergyCost != null)
            .GroupBy(dc => dc.Card.EnergyCost!.Value)
            .Select(g => new { EnergyCost = g.Key, AvgCount = g.Sum(dc => dc.Quantity) / (double)filteredDeckCount })
            .OrderBy(x => x.EnergyCost)
            .ToListAsync();

        var energyCurveDict = energyCurve.ToDictionary(x => x.EnergyCost, x => Math.Round(x.AvgCount, 2));

        // 5. Domain Distribution - % of cards by domain across this legend's decks
        var domainStats = await context.DeckCards
            .Where(dc => deckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId))
            .GroupBy(dc => dc.Card.Domain)
            .Select(g => new { Domain = g.Key, Total = g.Sum(dc => dc.Quantity) })
            .ToListAsync();

        var totalCards = domainStats.Sum(d => d.Total);
        var domainDistribution = totalCards > 0
            ? domainStats.OrderByDescending(d => d.Total)
                .ToDictionary(d => d.Domain ?? "Unknown", d => Math.Round((double)d.Total / totalCards * 100, 2))
            : new Dictionary<string, double>();

        // 3. Card Pair Synergy - top co-occurring card pairs
        var deckCardSets = await context.DeckCards
            .Where(dc => deckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId) && dc.Card.CardType == CardType.Main)
            .GroupBy(dc => dc.DeckId)
            .Select(g => g.Select(dc => new { dc.CardId, dc.Card.Name }).ToList())
            .ToListAsync();

        var pairCounts = new Dictionary<(string, string), int>();
        var cardNames = new Dictionary<string, string>();
        foreach (var deckSet in deckCardSets)
        {
            var cardList = deckSet.OrderBy(c => c.CardId).ToList();
            foreach (var c in cardList)
                cardNames.TryAdd(c.CardId, c.Name);
            for (int i = 0; i < cardList.Count; i++)
            {
                for (int j = i + 1; j < cardList.Count; j++)
                {
                    var key = (cardList[i].CardId, cardList[j].CardId);
                    pairCounts[key] = pairCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        var cardPairSynergies = pairCounts
            .OrderByDescending(p => p.Value)
            .Take(10)
            .Select(p => new CardPairDto
            {
                Card1Id = p.Key.Item1,
                Card1Name = cardNames.GetValueOrDefault(p.Key.Item1, ""),
                Card2Id = p.Key.Item2,
                Card2Name = cardNames.GetValueOrDefault(p.Key.Item2, ""),
                CoOccurrenceRate = Math.Round((double)p.Value / filteredDeckCount, 2)
            })
            .ToList();

        var cardStats = await context.DeckCards
            .Where(dc => deckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId))
            .GroupBy(dc => new { dc.CardId, dc.Card.Name, dc.Card.Category })
            .Select(g => new
            {
                CardId = g.Key.CardId,
                CardName = g.Key.Name,
                Category = g.Key.Category,
                AverageQuantity = g.Average(x => x.Quantity),
                InclusionCount = g.Count()
            })
            .ToListAsync();

        var insights = cardStats.Select(stat => new CardInsightDto
        {
            CardId = stat.CardId,
            CardName = stat.CardName,
            Category = stat.Category,
            AppearanceRate = (double)stat.InclusionCount / filteredDeckCount,
            AvgQuantity = stat.AverageQuantity,
            SynergyScore = 0
        }).ToList();

        // Categorize
        // Core: > 60% inclusion
        // Tech: 15% - 60% inclusion
        var snapshot = new MetaSnapshotDto
        {
            LegendName = legendCard.Name,
            SampleSize = totalDecksForLegend,
            AveragePlacement = averagePlacement,
            BestPlacement = bestPlacement,
            WorstPlacement = worstPlacement,
            PlayRate = playRate,
            TopCutRate = topCutRate,
            EnergyCurve = energyCurveDict,
            DomainDistribution = domainDistribution,
            CardPairSynergies = cardPairSynergies,
            CoreCards = insights
                .Where(c => c.AppearanceRate >= 0.6)
                .OrderByDescending(c => c.AppearanceRate)
                .ToList(),
            TechChoices = insights
                .Where(c => c.AppearanceRate < 0.6 && c.AppearanceRate >= 0.15)
                .OrderByDescending(c => c.AppearanceRate)
                .ToList()
        };

        return Result<MetaSnapshotDto>.Success(snapshot);
    }

    /// <summary>
    /// Stat 6: Meta Trend Over Time – per-tournament performance for a legend.
    /// </summary>
    public async Task<Result<List<TournamentTrendDto>>> GetTrendAsync(string championId, DateTime? from = null)
    {
        var legendCard = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard == null)
            return Result<List<TournamentTrendDto>>.Failure(new List<string> { "Legend not found" });

        var legendIds = await GetLegendGroupIdsAsync(legendCard);

        var trend = await context.TournamentResults
            .Include(tr => tr.Tournament)
            .Include(tr => tr.Deck)
            .Where(tr => legendIds.Contains(tr.Deck.LegendCardId))
            .Where(tr => !from.HasValue || tr.Tournament.Date >= from.Value)
            .GroupBy(tr => new { tr.TournamentId, tr.Tournament.Name, tr.Tournament.Date })
            .Select(g => new TournamentTrendDto
            {
                TournamentId = g.Key.TournamentId,
                TournamentName = g.Key.Name,
                Date = g.Key.Date,
                DecksEntered = g.Count(),
                AveragePlacement = Math.Round(g.Average(tr => (double)tr.Standing), 2),
                BestPlacement = g.Min(tr => tr.Standing)
            })
            .OrderBy(t => t.Date)
            .ToListAsync();

        return Result<List<TournamentTrendDto>>.Success(trend);
    }

    /// <summary>
    /// Stat 7: Matchup Table – how a legend performs relative to others in shared tournaments.
    /// A negative delta means "we" place better (lower standing is better).
    /// </summary>
    public async Task<Result<List<MatchupRowDto>>> GetMatchupsAsync(string championId, DateTime? from = null)
    {
        var legendCard = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard == null)
            return Result<List<MatchupRowDto>>.Failure(new List<string> { "Legend not found" });

        var legendIds = await GetLegendGroupIdsAsync(legendCard);

        // Get our per-tournament average placements
        var ourResults = await context.TournamentResults
            .Include(tr => tr.Deck)
            .Where(tr => legendIds.Contains(tr.Deck.LegendCardId))
            .Where(tr => !from.HasValue || tr.Tournament.Date >= from.Value)
            .GroupBy(tr => tr.TournamentId)
            .Select(g => new { TournamentId = g.Key, AvgStanding = g.Average(tr => (double)tr.Standing) })
            .ToListAsync();

        var ourTournamentIds = ourResults.Select(r => r.TournamentId).ToHashSet();
        var ourByTournament = ourResults.ToDictionary(r => r.TournamentId, r => r.AvgStanding);

        // Get all other legends' per-tournament average placements in those tournaments
        var otherResults = await context.TournamentResults
            .Include(tr => tr.Deck)
                .ThenInclude(d => d.Legend)
            .Where(tr => ourTournamentIds.Contains(tr.TournamentId)
                      && !legendIds.Contains(tr.Deck.LegendCardId)
                      && tr.Deck.Legend.CardType == CardType.Legend)
            .GroupBy(tr => new { tr.Deck.Legend.LegendGroup, tr.Deck.Legend.Name, tr.TournamentId })
            .Select(g => new
            {
                LegendGroup = g.Key.LegendGroup ?? g.First().Deck.LegendCardId,
                LegendName = g.Key.Name,
                TournamentId = g.Key.TournamentId,
                AvgStanding = g.Average(tr => (double)tr.Standing)
            })
            .ToListAsync();

        // Compute per-opponent delta across shared tournaments
        var matchups = otherResults
            .GroupBy(r => new { r.LegendGroup, r.LegendName })
            .Select(g =>
            {
                var deltas = g
                    .Where(r => ourByTournament.ContainsKey(r.TournamentId))
                    .Select(r => ourByTournament[r.TournamentId] - r.AvgStanding)
                    .ToList();

                return new MatchupRowDto
                {
                    LegendId = g.First().LegendGroup,
                    LegendName = g.Key.LegendName.Split(" - ")[0],
                    PlacementDelta = deltas.Count > 0 ? Math.Round(deltas.Average(), 2) : 0,
                    SharedTournaments = deltas.Count
                };
            })
            .Where(m => m.SharedTournaments > 0)
            .OrderBy(m => m.PlacementDelta)
            .ToList();

        // Deduplicate by legend group name (keep best SharedTournaments count)
        var deduped = matchups
            .GroupBy(m => m.LegendName)
            .Select(g => g.OrderByDescending(m => m.SharedTournaments).First())
            .OrderBy(m => m.PlacementDelta)
            .ToList();

        return Result<List<MatchupRowDto>>.Success(deduped);
    }

    /// <summary>
    /// Stat 8: Legend Tier List – composite ranking of all legends.
    /// Score = (TopCutRate * 0.4) + ((1 - normalizedAvgPlacement) * 0.35) + (PlayRate * 0.25)
    /// </summary>
    public async Task<Result<List<LegendTierDto>>> GetTierListAsync(DateTime? from = null)
    {
        var totalDecks = from.HasValue
            ? await context.TournamentResults
                .Where(tr => tr.Tournament.Date >= from.Value)
                .Select(tr => tr.DeckId)
                .Distinct()
                .CountAsync()
            : await context.Decks.CountAsync();
        if (totalDecks == 0)
            return Result<List<LegendTierDto>>.Success(new List<LegendTierDto>());

        // Get distinct legend groups
        var legendGroups = await context.Cards
            .Where(c => c.CardType == CardType.Legend && c.LegendGroup != null)
            .GroupBy(c => c.LegendGroup)
            .Select(g => new { Group = g.Key!, Name = g.First().Name, Ids = g.Select(c => c.Id).ToList() })
            .ToListAsync();

        // Also include legends without a group
        var ungrouped = await context.Cards
            .Where(c => c.CardType == CardType.Legend && c.LegendGroup == null)
            .Select(c => new { Group = c.Id, Name = c.Name, Ids = new List<string> { c.Id } })
            .ToListAsync();

        var allGroups = legendGroups.Concat(ungrouped)
            .Select(g => new { g.Group, Name = g.Name.Split(" - ")[0], g.Ids })
            .ToList();

        var tierEntries = new List<LegendTierDto>();

        foreach (var group in allGroups)
        {
            var groupDeckIds = from.HasValue
                ? await context.TournamentResults
                    .Where(tr => tr.Tournament.Date >= from.Value && group.Ids.Contains(tr.Deck.LegendCardId))
                    .Select(tr => tr.DeckId)
                    .Distinct()
                    .ToListAsync()
                : await context.Decks
                    .Where(d => group.Ids.Contains(d.LegendCardId))
                    .Select(d => d.Id)
                    .ToListAsync();

            var deckCount = groupDeckIds.Count;
            if (deckCount == 0) continue;

            var standings = await context.TournamentResults
                .Where(tr => groupDeckIds.Contains(tr.DeckId))
                .Select(tr => tr.Standing)
                .ToListAsync();

            var playRate = Math.Round((double)deckCount / totalDecks * 100, 2);
            var topCutCount = standings.Count(s => s <= 4);
            var topCutRate = standings.Count > 0 ? Math.Round((double)topCutCount / standings.Count * 100, 2) : 0;
            var avgPlacement = standings.Count > 0 ? Math.Round(standings.Average(), 2) : (double?)null;

            tierEntries.Add(new LegendTierDto
            {
                LegendId = group.Ids.First(),
                LegendName = group.Name,
                PlayRate = playRate,
                TopCutRate = topCutRate,
                AveragePlacement = avgPlacement,
            });
        }

        if (tierEntries.Count == 0)
            return Result<List<LegendTierDto>>.Success(new List<LegendTierDto>());

        // Normalize and compute composite score
        var maxPlacement = tierEntries.Where(t => t.AveragePlacement != null).Max(t => t.AveragePlacement!.Value);
        var maxPlayRate = tierEntries.Max(t => t.PlayRate);

        foreach (var entry in tierEntries)
        {
            var normPlacement = (entry.AveragePlacement != null && maxPlacement > 0)
                ? 1.0 - (entry.AveragePlacement.Value / maxPlacement)
                : 0;
            var normTopCut = entry.TopCutRate / 100.0;
            var normPlay = maxPlayRate > 0 ? entry.PlayRate / maxPlayRate : 0;

            entry.CompositeScore = Math.Round(normTopCut * 0.40 + normPlacement * 0.35 + normPlay * 0.25, 3);
        }

        // Assign tiers based on composite score percentiles
        var sorted = tierEntries.OrderByDescending(t => t.CompositeScore).ToList();
        var count = sorted.Count;
        for (int i = 0; i < count; i++)
        {
            var pct = (double)i / count;
            sorted[i].Tier = pct switch
            {
                < 0.10 => "S",
                < 0.30 => "A",
                < 0.55 => "B",
                < 0.80 => "C",
                _ => "D"
            };
        }

        return Result<List<LegendTierDto>>.Success(sorted);
    }

    /// <summary>
    /// Classifies decks into archetypes based on energy curve characteristics.
    /// </summary>
    public async Task<Result<List<string>>> GetArchetypesAsync(string championId, DateTime? from = null)
    {
        var legendCard = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard == null)
            return Result<List<string>>.Failure(new List<string> { "Legend not found" });

        var legendIds = await GetLegendGroupIdsAsync(legendCard);
        var deckIds = await GetDeckIdsInRangeAsync(legendIds, from);

        if (deckIds.Count == 0)
            return Result<List<string>>.Success(new List<string>());

        var archetypes = new HashSet<string>();

        // Analyze each deck's energy curve to classify archetype
        var deckProfiles = await context.DeckCards
            .Where(dc => deckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId) && dc.Card.CardType == CardType.Main)
            .GroupBy(dc => dc.DeckId)
            .Select(g => new
            {
                DeckId = g.Key,
                AvgCost = g.Where(dc => dc.Card.EnergyCost != null).Average(dc => (double)dc.Card.EnergyCost!.Value),
                SpellCount = g.Count(dc => dc.Card.Category == "Spell"),
                UnitCount = g.Count(dc => dc.Card.Category == "Unit"),
                GearCount = g.Count(dc => dc.Card.Category == "Gear"),
                TotalCards = g.Count()
            })
            .ToListAsync();

        foreach (var deck in deckProfiles)
        {
            var archetype = ClassifyArchetype(deck.AvgCost, deck.SpellCount, deck.UnitCount, deck.GearCount, deck.TotalCards);
            archetypes.Add(archetype);
        }

        return Result<List<string>>.Success(archetypes.OrderBy(a => a).ToList());
    }

    /// <summary>
    /// Generates an optimal deck for a legend + archetype based on top-performing deck card stats.
    /// </summary>
    public async Task<Result<GeneratedDeckDto>> GenerateDeckAsync(string championId, string archetype, DateTime? from = null)
    {
        var legendCard = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard == null)
            return Result<GeneratedDeckDto>.Failure(new List<string> { "Legend not found" });

        var legendIds = await GetLegendGroupIdsAsync(legendCard);
        var allTopDeckIds = await GetDeckIdsInRangeAsync(legendIds, from);

        if (allTopDeckIds.Count == 0)
            return Result<GeneratedDeckDto>.Failure(new List<string> { "No deck data available" });

        // Filter to decks matching the requested archetype
        var deckProfiles = await context.DeckCards
            .Where(dc => allTopDeckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId) && dc.Card.CardType == CardType.Main)
            .GroupBy(dc => dc.DeckId)
            .Select(g => new
            {
                DeckId = g.Key,
                AvgCost = g.Where(dc => dc.Card.EnergyCost != null).Average(dc => (double)dc.Card.EnergyCost!.Value),
                SpellCount = g.Count(dc => dc.Card.Category == "Spell"),
                UnitCount = g.Count(dc => dc.Card.Category == "Unit"),
                GearCount = g.Count(dc => dc.Card.Category == "Gear"),
                TotalCards = g.Count()
            })
            .ToListAsync();

        var archetypeDeckIds = deckProfiles
            .Where(d => ClassifyArchetype(d.AvgCost, d.SpellCount, d.UnitCount, d.GearCount, d.TotalCards) == archetype)
            .Select(d => d.DeckId)
            .ToList();

        // Fall back to all top decks if no decks match the archetype
        if (archetypeDeckIds.Count == 0)
            archetypeDeckIds = allTopDeckIds;

        var deckCount = archetypeDeckIds.Count;

        // Get Main deck card stats (CardType.Main)
        var mainCardStats = await context.DeckCards
            .Where(dc => archetypeDeckIds.Contains(dc.DeckId) && !legendIds.Contains(dc.CardId) && dc.Card.CardType == CardType.Main)
            .GroupBy(dc => new { dc.CardId, dc.Card.Name, dc.Card.Category })
            .Select(g => new
            {
                CardId = g.Key.CardId,
                CardName = g.Key.Name,
                Category = g.Key.Category,
                AvgQuantity = g.Average(x => (double)x.Quantity),
                AppearanceRate = (double)g.Count() / deckCount
            })
            .OrderByDescending(c => c.AppearanceRate)
            .ThenByDescending(c => c.AvgQuantity)
            .ToListAsync();

        // Get Rune (side deck) card stats
        var runeCardStats = await context.DeckCards
            .Where(dc => archetypeDeckIds.Contains(dc.DeckId) && dc.Card.CardType == CardType.Rune)
            .GroupBy(dc => new { dc.CardId, dc.Card.Name, dc.Card.Category })
            .Select(g => new
            {
                CardId = g.Key.CardId,
                CardName = g.Key.Name,
                Category = g.Key.Category,
                AvgQuantity = g.Average(x => (double)x.Quantity),
                AppearanceRate = (double)g.Count() / deckCount
            })
            .OrderByDescending(c => c.AppearanceRate)
            .ThenByDescending(c => c.AvgQuantity)
            .ToListAsync();

        // Build Main Deck: pick cards with >= 15% appearance, round quantity, cap at ~40 cards
        var mainDeck = new List<DeckEntryDto>();
        var battlefields = new List<DeckEntryDto>();
        int mainTotal = 0;

        foreach (var card in mainCardStats.Where(c => c.AppearanceRate >= 0.15))
        {
            var qty = (int)Math.Round(card.AvgQuantity);
            if (qty < 1) qty = 1;

            if (card.Category == "Battlefield")
            {
                battlefields.Add(new DeckEntryDto
                {
                    CardId = card.CardId,
                    CardName = card.CardName,
                    Category = card.Category,
                    Quantity = qty,
                    AppearanceRate = Math.Round(card.AppearanceRate, 2)
                });
            }
            else
            {
                if (mainTotal + qty > 40) break;
                mainDeck.Add(new DeckEntryDto
                {
                    CardId = card.CardId,
                    CardName = card.CardName,
                    Category = card.Category,
                    Quantity = qty,
                    AppearanceRate = Math.Round(card.AppearanceRate, 2)
                });
                mainTotal += qty;
            }
        }

        // Build Side Deck (Runes): pick top runes, cap at ~10
        var sideDeck = new List<DeckEntryDto>();
        int sideTotal = 0;
        foreach (var card in runeCardStats.Where(c => c.AppearanceRate >= 0.15))
        {
            var qty = (int)Math.Round(card.AvgQuantity);
            if (qty < 1) qty = 1;
            if (sideTotal + qty > 10) break;
            sideDeck.Add(new DeckEntryDto
            {
                CardId = card.CardId,
                CardName = card.CardName,
                Category = card.Category,
                Quantity = qty,
                AppearanceRate = Math.Round(card.AppearanceRate, 2)
            });
            sideTotal += qty;
        }

        return Result<GeneratedDeckDto>.Success(new GeneratedDeckDto
        {
            LegendId = championId,
            LegendName = legendCard.Name,
            Archetype = archetype,
            MainDeck = mainDeck,
            Battlefields = battlefields,
            SideDeck = sideDeck
        });
    }

    private static string ClassifyArchetype(double avgCost, int spellCount, int unitCount, int gearCount, int totalCards)
    {
        var spellRatio = totalCards > 0 ? (double)spellCount / totalCards : 0;
        var unitRatio = totalCards > 0 ? (double)unitCount / totalCards : 0;

        if (avgCost <= 2.5 && unitRatio >= 0.5)
            return "Aggro";
        if (avgCost >= 4.5)
            return "Control";
        if (spellRatio >= 0.5)
            return "Miracle";
        if (avgCost >= 3.5 && unitRatio >= 0.4)
            return "Midrange";
        if (gearCount >= 4)
            return "Gear-Heavy";
        return "Midrange";
    }

    /// <summary>
    /// Returns all card IDs that share the same LegendGroup as the given legend card.
    /// Falls back to just the single card ID if LegendGroup is not set.
    /// </summary>
    private async Task<List<string>> GetLegendGroupIdsAsync(Card legendCard)
    {
        if (!string.IsNullOrEmpty(legendCard.LegendGroup))
        {
            return await context.Cards
                .Where(c => c.LegendGroup == legendCard.LegendGroup)
                .Select(c => c.Id)
                .ToListAsync();
        }

        return new List<string> { legendCard.Id };
    }

    private async Task<List<Guid>> GetDeckIdsInRangeAsync(List<string> legendIds, DateTime? from)
    {
        IQueryable<TournamentResult> query = context.TournamentResults
            .Where(tr => legendIds.Contains(tr.Deck.LegendCardId));

        if (from.HasValue)
        {
            query = query.Where(tr => tr.Tournament.Date >= from.Value);
        }

        // Rank decks by best (lowest) tournament standing and keep only the top 25%
        var rankedDecks = await query
            .GroupBy(tr => tr.DeckId)
            .Select(g => new
            {
                DeckId = g.Key,
                BestStanding = g.Min(tr => tr.Standing)
            })
            .OrderBy(d => d.BestStanding)
            .ToListAsync();

        var topCount = Math.Max(1, (int)Math.Ceiling(rankedDecks.Count * 0.25));

        return rankedDecks.Take(topCount).Select(d => d.DeckId).ToList();
    }

    private async Task<List<Guid>> GetAllDeckIdsInRangeAsync(List<string> legendIds, DateTime? from)
    {
        if (from.HasValue)
        {
            return await context.TournamentResults
                .Where(tr => tr.Tournament.Date >= from.Value && legendIds.Contains(tr.Deck.LegendCardId))
                .Select(tr => tr.DeckId)
                .Distinct()
                .ToListAsync();
        }

        return await context.Decks
            .Where(d => legendIds.Contains(d.LegendCardId))
            .Select(d => d.Id)
            .ToListAsync();
    }
}
