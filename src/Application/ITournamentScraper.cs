using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Application;

public interface ITournamentScraper
{
    Task<List<TournamentResult>> ScrapeTournamentAsync(string url, DateTime? date = null);
    Task<BulkImportResult> ScrapeNewTournamentsAsync(string listUrl);
    Task<int> BackfillTournamentDatesAsync();
}

public class BulkImportResult
{
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> ImportedNames { get; set; } = new();
    public List<string> FailedNames { get; set; } = new();
}
