namespace RiftboundMetaAnalizer.Domain;

public class GeneratedDeckDto
{
    public string LegendId { get; set; } = "";
    public string LegendName { get; set; } = "";
    public string Archetype { get; set; } = "";
    public List<DeckEntryDto> MainDeck { get; set; } = new();
    public List<DeckEntryDto> Battlefields { get; set; } = new();
    public List<DeckEntryDto> SideDeck { get; set; } = new();
}

public class DeckEntryDto
{
    public string CardId { get; set; } = "";
    public string CardName { get; set; } = "";
    public string Category { get; set; } = "";
    public int Quantity { get; set; }
    public double AppearanceRate { get; set; }
}
