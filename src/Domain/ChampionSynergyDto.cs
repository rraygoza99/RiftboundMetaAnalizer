namespace RiftboundMetaAnalizer.Domain;

public class ChampionSynergyDto
{
    public required string ChampionId { get; set; }
    public List<Card> SynergisticCards { get; set; } = new();
}
