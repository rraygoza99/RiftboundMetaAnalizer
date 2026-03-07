using Microsoft.AspNetCore.Mvc;
using RiftboundMetaAnalizer.Application;

namespace RiftboundMetaAnalizer.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentsController : ControllerBase
    {
        private readonly ITournamentScraper _scraper;

        public TournamentsController(ITournamentScraper scraper)
        {
            _scraper = scraper;
        }

        [HttpPost("scrape")]
        public async Task<IActionResult> ScrapeTournament([FromBody] string url)
        {
            try
            {
                var results = await _scraper.ScrapeTournamentAsync(url);

                return Ok(new
                {
                    message = $"Scraped {results.Count} results",
                    count = results.Count,
                    tournament = results.FirstOrDefault()?.Tournament?.Name,
                    standings = results.Select(r => new { r.Standing, Deck = r.Deck?.Name }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("scrape-all")]
        public async Task<IActionResult> ScrapeAllNewTournaments([FromBody] string listUrl)
        {
            try
            {
                var result = await _scraper.ScrapeNewTournamentsAsync(listUrl);

                return Ok(new
                {
                    message = $"Imported {result.Imported}, skipped {result.Skipped}, failed {result.Failed}",
                    imported = result.Imported,
                    skipped = result.Skipped,
                    failed = result.Failed,
                    importedNames = result.ImportedNames,
                    failedNames = result.FailedNames
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [HttpPost("backfill-dates")]
        public async Task<IActionResult> BackfillDates()
        {
            try
            {
                var updated = await _scraper.BackfillTournamentDatesAsync();
                return Ok(new { message = $"Updated {updated} tournament dates" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}
