using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using RiftboundMetaAnalizer.Domain;
using Microsoft.EntityFrameworkCore;

namespace RiftboundMetaAnalizer.Application;

public class MetaService(RiftContext context, IDistributedCache cache) : IMetaService
{
    public async Task<Result<ChampionSynergyDto>> GetChampionSynergyAsync(string championId)
    {
        var cacheKey = $"champion-synergy-{championId}";
        var cachedResult = await cache.GetStringAsync(cacheKey);

        if (cachedResult is not null)
        {
            var synergy = JsonConvert.DeserializeObject<ChampionSynergyDto>(cachedResult);
            return Result<ChampionSynergyDto>.Success(synergy!);
        }

        var legendCard = await context.Cards.FirstOrDefaultAsync(c => c.Id == championId && c.CardType == CardType.Legend);
        if (legendCard is null)
        {
            return Result<ChampionSynergyDto>.Failure(new List<string> { "Legend not found." });
        }

        var decksWithLegend = await context.Decks
            .Where(d => d.LegendCardId == championId)
            .Select(d => d.Id)
            .ToListAsync();

        if (!decksWithLegend.Any())
        {
            return Result<ChampionSynergyDto>.Success(new ChampionSynergyDto { ChampionId = championId });
        }

        var synergisticRunes = await context.DeckCards
            .Where(dc => decksWithLegend.Contains(dc.DeckId))
            .Join(context.Cards,
                deckCard => deckCard.CardId,
                card => card.Id,
                (deckCard, card) => new { deckCard, card })
            .Where(x => x.card.CardType == CardType.Rune)
            .GroupBy(x => x.card)
            .Select(g => new
            {
                Card = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .Select(x => x.Card)
            .ToListAsync();


        var resultDto = new ChampionSynergyDto
        {
            ChampionId = championId,
            SynergisticCards = synergisticRunes
        };

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60)
        };
        await cache.SetStringAsync(cacheKey, JsonConvert.SerializeObject(resultDto), cacheOptions);

        return Result<ChampionSynergyDto>.Success(resultDto);
    }
}
