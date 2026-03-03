using HtmlAgilityPack;
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

    public async Task<List<TournamentResult>> ScrapeTournamentAsync(string url)
    {
        var web = new HtmlWeb();
        web.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
        
        var doc = await web.LoadFromWebAsync(url);
        var results = new List<TournamentResult>();
        
        var tournamentNameNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'page-title')]");
        var tournamentName = tournamentNameNode?.InnerText.Trim() ?? "Unknown Tournament";

        var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.Name == tournamentName);
        if (tournament == null)
        {
            tournament = new Tournament
            {
                Name = tournamentName,
                Date = DateTime.UtcNow,
                Results = new List<TournamentResult>()
            };
            // Note: We add it to the context so it's tracked. 
            // SaveChanges will be called later by the caller (Program.cs)
            _context.Tournaments.Add(tournament);
        }

        // Finding deck list items based on DOM rows
        var deckNodes = doc.DocumentNode.SelectNodes("//tr[starts-with(@id, 'desktop-deck-')]");

        if (deckNodes != null)
        {
            foreach (var node in deckNodes)
            {
                var rankNode = node.SelectSingleNode(".//td[1]//strong");
                var deckLinkNode = node.SelectSingleNode(".//td[3]//a");
                var deckUrl = node.GetAttributeValue("data-href", "");

                if (rankNode != null && !string.IsNullOrWhiteSpace(deckUrl))
                {
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
                            // Minimal properties
                            Legend = new Card { Id = "unknown", Name = "Unknown", Domain = "Unknown", ChampionTag = "Unknown" },
                            LegendCardId = "unknown"
                        };
                    }
                    else
                    {
                        deck.Name = string.IsNullOrWhiteSpace(deck.Name) ? deckName : deck.Name;
                    }

                    results.Add(new TournamentResult
                    {
                        Id = Guid.NewGuid(),
                        Tournament = tournament,
                        Standing = standing > 0 ? standing : 99, // Default if parsing fails
                        Deck = deck
                    });
                }
            }
        }

        return results;
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
                Legend = new Card { Id = "temp-legend", Name = "Unknown Legend", Domain = "Unknown", ChampionTag = "Unknown" },
                LegendCardId = "temp-legend"
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
                    deck.LegendCardId = card.Id;
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

            if (deck.LegendCardId == "temp-legend" && deck.DeckCards.Any())
            {
                var potentiaLegend = deck.DeckCards.FirstOrDefault(dc => dc.Card.CardType == CardType.Legend);
                if (potentiaLegend != null)
                {
                     deck.Legend = potentiaLegend.Card;
                     deck.LegendCardId = potentiaLegend.CardId;
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
}
