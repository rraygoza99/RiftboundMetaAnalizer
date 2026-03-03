using RiftboundMetaAnalizer.Domain;

namespace RiftboundMetaAnalizer.Application;

public interface IMetaService
{
    Task<Result<ChampionSynergyDto>> GetChampionSynergyAsync(string championId);
}
