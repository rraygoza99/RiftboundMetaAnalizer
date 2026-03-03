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
    }
}
