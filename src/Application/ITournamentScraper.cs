using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Application;

public interface ITournamentScraper
{
    Task<List<TournamentResult>> ScrapeTournamentAsync(string url);
}
