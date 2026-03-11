using Microsoft.AspNetCore.Mvc;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetaController : ControllerBase
    {
        private readonly IMetaService _metaService;

        public MetaController(IMetaService metaService)
        {
            _metaService = metaService;
        }

        private static DateTime? ParseRange(string? range) => range switch
        {
            "1w" => DateTime.UtcNow.AddDays(-7),
            "2w" => DateTime.UtcNow.AddDays(-14),
            "1m" => DateTime.UtcNow.AddMonths(-1),
            _ => null
        };

        [HttpGet("champion-synergy/{championId}")]
        public async Task<IActionResult> GetChampionSynergy(string championId, [FromQuery] string? range = null)
        {
            var from = ParseRange(range);
            var result = await _metaService.GetChampionSynergyAsync(championId, from);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return NotFound(new { Errors = result.Errors });
        }

        [HttpGet("trend/{championId}")]
        public async Task<IActionResult> GetTrend(string championId, [FromQuery] string? range = null)
        {
            var from = ParseRange(range);
            var result = await _metaService.GetTrendAsync(championId, from);
            return result.IsSuccess ? Ok(result.Value) : NotFound(new { Errors = result.Errors });
        }

        [HttpGet("matchups/{championId}")]
        public async Task<IActionResult> GetMatchups(string championId, [FromQuery] string? range = null)
        {
            var from = ParseRange(range);
            var result = await _metaService.GetMatchupsAsync(championId, from);
            return result.IsSuccess ? Ok(result.Value) : NotFound(new { Errors = result.Errors });
        }

        [HttpGet("tier-list")]
        public async Task<IActionResult> GetTierList([FromQuery] string? range = null)
        {
            var from = ParseRange(range);
            var result = await _metaService.GetTierListAsync(from);
            return result.IsSuccess ? Ok(result.Value) : NotFound(new { Errors = result.Errors });
        }

        [HttpGet("archetypes/{championId}")]
        public async Task<IActionResult> GetArchetypes(string championId, [FromQuery] string? range = null)
        {
            var from = ParseRange(range);
            var result = await _metaService.GetArchetypesAsync(championId, from);
            return result.IsSuccess ? Ok(result.Value) : NotFound(new { Errors = result.Errors });
        }

        [HttpGet("generate-deck/{championId}")]
        public async Task<IActionResult> GenerateDeck(string championId, [FromQuery] string archetype, [FromQuery] string? range = null)
        {
            var from = ParseRange(range);
            var result = await _metaService.GenerateDeckAsync(championId, archetype, from);
            return result.IsSuccess ? Ok(result.Value) : NotFound(new { Errors = result.Errors });
        }
    }
}
