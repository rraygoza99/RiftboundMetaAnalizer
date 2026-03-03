using Microsoft.AspNetCore.Mvc;
using RiftboundMetaAnalizer.Application;
using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DecksController : ControllerBase
    {
        private readonly IDeckService _deckService;

        public DecksController(IDeckService deckService)
        {
            _deckService = deckService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateDeck(Deck deck)
        {
            var result = await _deckService.CreateDeckAsync(deck);

            if (result.IsSuccess)
            {
                return Created($"/api/decks/{result.Value}", result.Value);
            }

            return BadRequest(new { Errors = result.Errors });
        }
    }
}
