using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;
using RiftboundMetaAnalizer.Infrastructure;

namespace RiftboundMetaAnalizer.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TournamentsController : ControllerBase
    {
        private readonly ITournamentScraper _scraper;
        private readonly RiftContext _context;

        public TournamentsController(ITournamentScraper scraper, RiftContext context)
        {
            _scraper = scraper;
            _context = context;
        }

        [HttpPost("scrape")]
        public async Task<IActionResult> ScrapeTournament([FromBody] string url)
        {
            try
            {
                var results = await _scraper.ScrapeTournamentAsync(url);
                
                _context.TournamentResults.AddRange(results);
                await _context.SaveChangesAsync();

                return Ok(results);
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }
    }
}
