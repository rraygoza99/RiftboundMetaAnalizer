using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using RiftboundMetaAnalizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace RiftboundMetaAnalizer.Application;

public class MetaService(RiftContext context, IDistributedCache cache) : IMetaService
{
    public async Task<Result<ChampionSynergyDto>> GetChampionSynergyAsync(string championId)
    {
        var legendCard = await context.Cards.FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard is null)
        {
            return Result<ChampionSynergyDto>.Failure(new List<string> { "Legend not found." });
        }

        // Collect all variant IDs for this legend group
        var legendIds = await GetLegendGroupIdsAsync(legendCard);

        // Use navigation properties for more efficient query and weighting
        var synergisticRunes = await context.DeckCards
            .Include(dc => dc.Deck)
                .ThenInclude(d => d.TournamentResults)
            .Where(dc => legendIds.Contains(dc.Deck.LegendCardId))
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

        var metaSnapshot = await GetMetaSnapshotAsync(championId);

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

    public async Task<Result<MetaSnapshotDto>> GetMetaSnapshotAsync(string championId)
    {
        var legendCard = await context.Cards
            .FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);

        if (legendCard == null)
        {
            return Result<MetaSnapshotDto>.Failure(new List<string> { "Legend not found" });
        }

        // Collect all variant IDs for this legend group
        var legendIds = await GetLegendGroupIdsAsync(legendCard);

        var deckIds = await context.Decks
            .Where(d => legendIds.Contains(d.LegendCardId))
            .Select(d => d.Id)
            .ToListAsync();

        var totalDecksForLegend = deckIds.Count;

        if (totalDecksForLegend == 0)
        {
            return Result<MetaSnapshotDto>.Success(new MetaSnapshotDto
            {
                LegendName = legendCard.Name,
                SampleSize = 0,
                AveragePlacement = null,
                CoreCards = new List<CardInsightDto>(),
                TechChoices = new List<CardInsightDto>()
            });
        }

        // Calculate average tournament placement for this legend
        var standings = await context.TournamentResults
            .Where(tr => deckIds.Contains(tr.DeckId))
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
            AppearanceRate = (double)stat.InclusionCount / totalDecksForLegend,
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
}
