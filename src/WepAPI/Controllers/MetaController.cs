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

        [HttpGet("champion-synergy/{championId}")]
        public async Task<IActionResult> GetChampionSynergy(string championId)
        {
            var result = await _metaService.GetChampionSynergyAsync(championId);

            if (result.IsSuccess)
            {
                return Ok(result.Value);
            }

            return NotFound(new { Errors = result.Errors });
        }
    }
}
