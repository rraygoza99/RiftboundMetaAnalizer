using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Application;

public interface ICardScraper
{
    Task<List<Card>> ScrapeCardsAsync(string url);
}
