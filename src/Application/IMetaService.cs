using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Application;

public interface IMetaService
{
    Task<Result<ChampionSynergyDto>> GetChampionSynergyAsync(string championId, DateTime? from = null);
    Task<Result<MetaSnapshotDto>> GetMetaSnapshotAsync(string championId, DateTime? from = null);
    Task<Result<List<TournamentTrendDto>>> GetTrendAsync(string championId, DateTime? from = null);
    Task<Result<List<MatchupRowDto>>> GetMatchupsAsync(string championId, DateTime? from = null);
    Task<Result<List<LegendTierDto>>> GetTierListAsync(DateTime? from = null);
    Task<Result<List<string>>> GetArchetypesAsync(string championId, DateTime? from = null);
    Task<Result<GeneratedDeckDto>> GenerateDeckAsync(string championId, string archetype, DateTime? from = null);
}
