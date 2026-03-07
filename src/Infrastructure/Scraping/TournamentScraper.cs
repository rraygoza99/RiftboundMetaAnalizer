using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;
using RiftboundMetaAnalizer.Infrastructure;

namespace RiftboundMetaAnalizer.Infrastructure.Scraping;

public class TournamentScraper : ITournamentScraper
{
    private readonly RiftContext _context;

    public TournamentScraper(RiftContext context)
    {
        _context = context;
    }

    public async Task<List<TournamentResult>> ScrapeTournamentAsync(string url, DateTime? date = null)
    {
        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
        
        var doc = await web.LoadFromWebAsync(url);
        var results = new List<TournamentResult>();
        
        var tournamentNameNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'page-title')]");
        var tournamentName = tournamentNameNode?.InnerText.Trim() ?? "Unknown Tournament";

        // Parse date from the page if not provided
        var tournamentDate = date ?? ParseTournamentDateFromPage(doc) ?? DateTime.UtcNow;

        var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.Name == tournamentName);
        if (tournament == null)
        {
            tournament = new Tournament
            {
                Name = tournamentName,
                Date = tournamentDate,
                Results = new List<TournamentResult>()
            };
            _context.Tournaments.Add(tournament);
        }

        // Ensure placeholder cards exist
        await EnsurePlaceholderCardAsync("UNKNOWN", "Unknown", "Unknown");
        await EnsurePlaceholderCardAsync("TEMP-LEGEND", "Unknown Legend", "Unknown");

        // Flush tournament + placeholders so the connection isn't held during scraping
        await _context.SaveChangesAsync();

        // Finding deck list items based on DOM rows
        var deckNodes = doc.DocumentNode.SelectNodes("//tr[starts-with(@id, 'desktop-deck-')]");

        if (deckNodes == null || deckNodes.Count == 0)
        {
            throw new InvalidOperationException(
                $"No deck rows found on the page. The site may be rate-limiting or the page structure changed. Tournament: '{tournamentName}'");
        }

        foreach (var node in deckNodes)
        {
            var rankNode = node.SelectSingleNode(".//td[1]//strong");
            var deckLinkNode = node.SelectSingleNode(".//td[3]//a");
            var deckUrl = node.GetAttributeValue("data-href", "");

            if (rankNode == null || string.IsNullOrWhiteSpace(deckUrl))
                continue;

            var rankText = rankNode.InnerText.Trim();
            var numericRank = new string(rankText.Where(char.IsDigit).ToArray());
            int.TryParse(numericRank, out int standing);

            var deckName = deckLinkNode?.InnerText.Trim() ?? "Unknown Deck";
            
            if (!deckUrl.StartsWith("http"))
            {
                var uri = new Uri(url);
                deckUrl = $"{uri.Scheme}://{uri.Host}{deckUrl}";
            }

            // Scrape the individual deck page details
            var deck = await ScrapeDeckDetailsAsync(deckUrl);
            if (deck is null)
            {
                // Fallback if detail scraping fails
                deck = new Deck 
                { 
                    Id = Guid.NewGuid(),
                    Name = deckName,
                    Legend = await GetOrTrackCardAsync("UNKNOWN", "Unknown", "Unknown"),
                    LegendCardId = "UNKNOWN"
                };
            }
            else
            {
                deck.Name = string.IsNullOrWhiteSpace(deck.Name) ? deckName : deck.Name;
            }

            var result = new TournamentResult
            {
                Id = Guid.NewGuid(),
                Tournament = tournament,
                Standing = standing > 0 ? standing : 99,
                Deck = deck
            };

            _context.TournamentResults.Add(result);

            // Save each result individually — keeps transactions short and
            // prevents a poisoned connection pool if one save fails.
            await _context.SaveChangesAsync();

            results.Add(result);
        }

        return results;
    }

    public async Task<BulkImportResult> ScrapeNewTournamentsAsync(string listUrl)
    {
        var result = new BulkImportResult();
        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        var existingNames = await _context.Tournaments.Select(t => t.Name).ToListAsync();
        var existingSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);

        // Parse base URL to build paginated URLs
        var baseUri = new Uri(listUrl);
        var existingQuery = QueryHelpers.ParseQuery(baseUri.Query);
        var basePathAndQuery = $"{baseUri.Scheme}://{baseUri.Host}{baseUri.AbsolutePath}";

        for (int page = 1; ; page++)
        {
            var queryDict = new Dictionary<string, string?>();
            foreach (var kvp in existingQuery)
                queryDict[kvp.Key] = kvp.Value.ToString();
            queryDict["page"] = page.ToString();

            var pageUrl = QueryHelpers.AddQueryString(basePathAndQuery, queryDict);

            Console.WriteLine($"Fetching tournament list page {page}: {pageUrl}");
            await Task.Delay(1500);

            var doc = await web.LoadFromWebAsync(pageUrl);

            var rows = doc.DocumentNode.SelectNodes("//tr[@data-href]");
            if (rows == null || rows.Count == 0)
                break;

            bool allExisted = true;

            foreach (var row in rows)
            {
                var tournamentUrl = row.GetAttributeValue("data-href", "");
                if (string.IsNullOrWhiteSpace(tournamentUrl))
                    continue;

                // Tournament name is in the 3rd <td>'s <a> tag
                var nameNode = row.SelectSingleNode(".//td[3]//a");
                var name = nameNode?.InnerText.Trim() ?? "";

                if (string.IsNullOrEmpty(name))
                    continue;

                // Parse date from the first <td>'s <b> tag (format: yyyy-MM-dd)
                var dateNode = row.SelectSingleNode(".//td[1]//b");
                DateTime? rowDate = null;
                if (dateNode != null && DateTime.TryParse(dateNode.InnerText.Trim(), out var parsed))
                    rowDate = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

                if (existingSet.Contains(name))
                {
                    result.Skipped++;
                    continue;
                }

                allExisted = false;

                if (!tournamentUrl.StartsWith("http"))
                    tournamentUrl = $"{baseUri.Scheme}://{baseUri.Host}{tournamentUrl}";

                try
                {
                    Console.WriteLine($"Importing tournament: {name}");
                    await Task.Delay(2000);
                    await ScrapeTournamentAsync(tournamentUrl, rowDate);
                    existingSet.Add(name);
                    result.Imported++;
                    result.ImportedNames.Add(name);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to import '{name}': {ex.Message}");
                    result.Failed++;
                    result.FailedNames.Add(name);
                }
            }

            // If every tournament on this page already existed, stop paginating
            if (allExisted)
                break;

            // Check if there's a next page
            var nextLink = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'page-item')]//a[@rel='next']");
            if (nextLink == null)
                break;
        }

        return result;
    }

    private async Task<Deck?> ScrapeDeckDetailsAsync(string deckUrl)
    {
        try
        {
            await Task.Delay(1000);
            var web = new HtmlWeb();
            web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
            
            var doc = await web.LoadFromWebAsync(deckUrl);
            
            // Extract Deck Name
            var deckNameNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'page-title')]");
            var deckName = deckNameNode?.InnerText.Trim() ?? "Unknown Deck";
            if (deckName.Contains(" by "))
            {
                deckName = deckName.Substring(0, deckName.LastIndexOf(" by "));
            }

            var deck = new Deck 
            { 
                Id = Guid.NewGuid(),
                DeckCards = new List<DeckCard>(),
                Name = deckName,
                Legend = await GetOrTrackCardAsync("TEMP-LEGEND", "Unknown Legend", "Unknown"),
                LegendCardId = "TEMP-LEGEND"
            };

            var cardRows = doc.DocumentNode.SelectNodes("//tr[contains(@class, 'card-list-item')]");
            
            if (cardRows == null) return null;

            foreach (var row in cardRows)
            {
                var quantityAttr = row.GetAttributeValue("data-quantity", "1");
                var cardTypeAttr = row.GetAttributeValue("data-card-type", "unit");

                if (!int.TryParse(quantityAttr, out int quantity))
                {
                    quantity = 1;
                }

                var nameNode = row.SelectSingleNode(".//td[3]/a");
                if (nameNode == null) continue;
                
                var cardName = nameNode.InnerText.Trim();
                
                // Extract ID from image source (ogn-185-298_full.png -> ogn-185)
                var imageSrc = row.GetAttributeValue("data-image-src", "");
                var cardId = string.Empty;

                if (!string.IsNullOrEmpty(imageSrc))
                {
                    var filename = imageSrc.Split('/').Last();
                    if (filename.Contains("_full"))
                    {
                        filename = filename.Substring(0, filename.LastIndexOf("_full"));
                    }
                    else if (filename.Contains("."))
                    {
                         filename = filename.Substring(0, filename.LastIndexOf("."));
                    }

                    var lastDash = filename.LastIndexOf('-');
                    if (lastDash > 0)
                    {
                        cardId = filename.Substring(0, lastDash);
                    }
                    else
                    {
                        cardId = filename;
                    }
                }

                // Fallback to name-based ID if image extraction fails completely
                if (string.IsNullOrEmpty(cardId))
                {
                    var cardHref = nameNode.GetAttributeValue("href", "");
                    cardId = cardHref.Split('/').Last().Replace("details-", "");
                }

                // Normalize to uppercase to match API card IDs
                cardId = cardId.ToUpperInvariant();
                
                var domainNodes = row.SelectNodes(".//td[5]//img[@alt]");
                var domains = new List<string>();
                if (domainNodes != null)
                {
                    foreach(var img in domainNodes)
                    {
                        var alt = img.GetAttributeValue("alt", "").ToLower();
                        if (!string.IsNullOrEmpty(alt) && alt != "rare" && alt != "common" && alt != "uncommon" && alt != "epic")
                        {
                            domains.Add(char.ToUpper(alt[0]) + alt.Substring(1));
                        }
                    }
                }
                var domainString = domains.Count > 0 ? string.Join(", ", domains.Distinct()) : "Neutral";

                // Check or Create Card
                var existingCard = _context.Cards.Local.FirstOrDefault(c => c.Id == cardId) 
                                   ?? await _context.Cards.FirstOrDefaultAsync(c => c.Id == cardId);
                
                Card card;
                if (existingCard != null)
                {
                    card = existingCard;
                    if (existingCard.Domain == "Unknown" && domainString != "Unknown") existingCard.Domain = domainString;
                }
                else
                {
                    card = new Card 
                    { 
                        Id = cardId, 
                        Name = cardName,
                        Domain = domainString, 
                        ChampionTag = cardTypeAttr == "champion" ? "Champion" : null,
                        CardType = cardTypeAttr == "legend" ? CardType.Legend : CardType.Main
                    };
                    // Track it immediately so subsequent iterations find it in Local
                    _context.Cards.Add(card);
                }

                if (cardTypeAttr == "legend")
                {
                    deck.Legend = card;
                    deck.LegendCardId = card.Id.ToUpper();
                }

                var existingDeckCard = deck.DeckCards.FirstOrDefault(dc => dc.CardId == card.Id);
                if (existingDeckCard != null)
                {
                    existingDeckCard.Quantity += quantity;
                }
                else
                {
                    deck.DeckCards.Add(new DeckCard 
                    { 
                        DeckId = deck.Id, 
                        CardId = card.Id,
                        Card = card,
                        Quantity = quantity 
                    });
                }
            }

            if (deck.LegendCardId == "TEMP-LEGEND" && deck.DeckCards.Any())
            {
                var potentiaLegend = deck.DeckCards.FirstOrDefault(dc => dc.Card.CardType == CardType.Legend);
                if (potentiaLegend != null)
                {
                     deck.Legend = potentiaLegend.Card;
                     deck.LegendCardId = potentiaLegend.CardId.ToUpper();
                }
            }
            
            return deck;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scraping deck {deckUrl}: {ex.Message}");
            return null;
        }
    }

    private async Task EnsurePlaceholderCardAsync(string id, string name, string domain)
    {
        var exists = _context.Cards.Local.Any(c => c.Id == id)
                     || await _context.Cards.AnyAsync(c => c.Id == id);
        if (!exists)
        {
            _context.Cards.Add(new Card { Id = id, Name = name, Domain = domain, ChampionTag = null });
        }
    }

    private async Task<Card> GetOrTrackCardAsync(string id, string name, string domain)
    {
        var card = _context.Cards.Local.FirstOrDefault(c => c.Id == id)
                   ?? await _context.Cards.FirstOrDefaultAsync(c => c.Id == id);
        if (card != null) return card;

        card = new Card { Id = id, Name = name, Domain = domain, ChampionTag = null };
        _context.Cards.Add(card);
        return card;
    }

    private static DateTime? ParseTournamentDateFromPage(HtmlDocument doc)
    {
        // Try og:description: "...that took place on 2026-03-01."
        var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='og:description']");
        if (ogDesc != null)
        {
            var content = ogDesc.GetAttributeValue("content", "");
            var match = Regex.Match(content, @"took place on (\d{4}-\d{2}-\d{2})");
            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var parsed))
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        // Fallback: article:published_time meta tag
        var pubMeta = doc.DocumentNode.SelectSingleNode("//meta[@name='article:published_time']");
        if (pubMeta != null)
        {
            var content = pubMeta.GetAttributeValue("content", "");
            if (DateTime.TryParse(content, out var parsed))
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }

    public async Task<int> BackfillTournamentDatesAsync()
    {
        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        var listUrl = "https://riftdecks.com/riftbound-tournaments?relevance=2";
        var baseUri = new Uri(listUrl);
        var existingQuery = QueryHelpers.ParseQuery(baseUri.Query);
        var basePathAndQuery = $"{baseUri.Scheme}://{baseUri.Host}{baseUri.AbsolutePath}";

        // Build a name -> date map from all list pages
        var dateMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        for (int page = 1; ; page++)
        {
            var queryDict = new Dictionary<string, string?>();
            foreach (var kvp in existingQuery)
                queryDict[kvp.Key] = kvp.Value.ToString();
            queryDict["page"] = page.ToString();

            var pageUrl = QueryHelpers.AddQueryString(basePathAndQuery, queryDict);
            Console.WriteLine($"Backfill: reading page {page}");
            await Task.Delay(1500);

            var doc = await web.LoadFromWebAsync(pageUrl);
            var rows = doc.DocumentNode.SelectNodes("//tr[@data-href]");
            if (rows == null || rows.Count == 0)
                break;

            foreach (var row in rows)
            {
                var nameNode = row.SelectSingleNode(".//td[3]//a");
                var dateNode = row.SelectSingleNode(".//td[1]//b");
                var name = nameNode?.InnerText.Trim() ?? "";
                if (string.IsNullOrEmpty(name) || dateNode == null) continue;
                if (DateTime.TryParse(dateNode.InnerText.Trim(), out var d))
                    dateMap.TryAdd(name, DateTime.SpecifyKind(d, DateTimeKind.Utc));
            }

            var nextLink = doc.DocumentNode.SelectSingleNode("//li[contains(@class, 'page-item')]//a[@rel='next']");
            if (nextLink == null) break;
        }

        // Update tournaments in DB
        var tournaments = await _context.Tournaments.ToListAsync();
        int updated = 0;
        foreach (var t in tournaments)
        {
            // Match by the short name (tournament name in DB includes store suffix)
            var match = dateMap.FirstOrDefault(kvp => t.Name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null && t.Date != match.Value)
            {
                t.Date = match.Value;
                updated++;
            }
        }

        if (updated > 0)
            await _context.SaveChangesAsync();

        return updated;
    }
}
