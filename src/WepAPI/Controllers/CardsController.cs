using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;
using RiftboundMetaAnalizer.Infrastructure;

namespace RiftboundMetaAnalizer.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CardsController : ControllerBase
    {
        private readonly ICardScraper _scraper;
        private readonly RiftContext _context;
        private readonly ILogger<CardsController> _logger;

        public CardsController(ICardScraper scraper, RiftContext context, ILogger<CardsController> logger)
        {
            _scraper = scraper;
            _context = context;
            _logger = logger;
        }

        [HttpGet("legends")]
        public async Task<IActionResult> GetLegends()
        {
            var allLegends = await _context.Cards
                .Where(c => c.CardType == CardType.Legend)
                .OrderBy(c => c.Id)
                .Select(c => new { c.Id, c.Name, c.Domain, c.LegendGroup })
                .ToListAsync();

            // Deduplicate by LegendGroup client-side to avoid EF GroupBy translation issues
            var legends = allLegends
                .GroupBy(c => c.LegendGroup ?? c.Name)
                .Select(g => g.First())
                .OrderBy(c => c.Name)
                .ToList();

            return Ok(legends);
        }

        [HttpPost("scrape")]
        public async Task<IActionResult> ScrapeCards([FromBody] string url)
        {
            try
            {
                _logger.LogInformation("Starting scrape for URL: {Url}", url);
                var result = await _scraper.ScrapeCardsAsync(url);
                _logger.LogInformation("Scraper found {Count} cards", result.Count);
                
                int newCardsCount = 0;
                foreach (var card in result)
                {
                     if (!await _context.Cards.AnyAsync(c => c.Id == card.Id))
                     {
                         _context.Cards.Add(card);
                         newCardsCount++;
                     }
                }
                
                if (newCardsCount > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved {Count} new cards to database", newCardsCount);
                }
                else
                {
                    _logger.LogInformation("No new cards to save. All {Count} cards already exist.", result.Count);
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping cards from {Url}", url);
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("update")]
        public async Task<IActionResult> UpdateCards([FromBody] string url)
        {
            try
            {
                _logger.LogInformation("Starting card update scrape for URL: {Url}", url);
                var scrapedCards = await _scraper.ScrapeCardsAsync(url);
                _logger.LogInformation("Scraper found {Count} cards for update", scrapedCards.Count);

                int updatedCount = 0;
                int newCount = 0;

                foreach (var scraped in scrapedCards)
                {
                    // Match case-insensitively: API uses uppercase (OGN-179), TournamentScraper uses lowercase (ogn-179)
                    var scrapedIdUpper = scraped.Id.ToUpperInvariant();
                    var existingCards = await _context.Cards
                        .Where(c => c.Id.ToUpper() == scrapedIdUpper)
                        .ToListAsync();

                    if (existingCards.Count > 0)
                    {
                        foreach (var existing in existingCards)
                        {
                            bool changed = false;

                            if (existing.CardType != scraped.CardType)
                            {
                                existing.CardType = scraped.CardType;
                                changed = true;
                            }

                            if (existing.Category != scraped.Category)
                            {
                                existing.Category = scraped.Category;
                                changed = true;
                            }

                            if (existing.Domain != scraped.Domain && scraped.Domain != "Colorless")
                            {
                                existing.Domain = scraped.Domain;
                                changed = true;
                            }

                            if (existing.EnergyCost != scraped.EnergyCost && scraped.EnergyCost.HasValue)
                            {
                                existing.EnergyCost = scraped.EnergyCost;
                                changed = true;
                            }

                            if (existing.ChampionTag != scraped.ChampionTag && scraped.ChampionTag != null)
                            {
                                existing.ChampionTag = scraped.ChampionTag;
                                changed = true;
                            }

                            if (existing.Name != scraped.Name)
                            {
                                existing.Name = scraped.Name;
                                changed = true;
                            }

                            if (existing.LegendGroup != scraped.LegendGroup && scraped.LegendGroup != null)
                            {
                                existing.LegendGroup = scraped.LegendGroup;
                                changed = true;
                            }

                            if (changed) updatedCount++;
                        }
                    }
                    else
                    {
                        _context.Cards.Add(scraped);
                        newCount++;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Card update complete: {Updated} updated, {New} new", updatedCount, newCount);
                return Ok(new { Updated = updatedCount, New = newCount, Total = scrapedCards.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cards from {Url}", url);
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}
