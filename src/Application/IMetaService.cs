using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Application;

public interface IMetaService
{
    Task<Result<ChampionSynergyDto>> GetChampionSynergyAsync(string championId);
    Task<Result<MetaSnapshotDto>> GetMetaSnapshotAsync(string championId);
}
