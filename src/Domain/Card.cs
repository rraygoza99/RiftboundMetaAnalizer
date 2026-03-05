public class Card {
    public required string Id { get; set; }
    public required string Name { get; set; }
    public CardType CardType { get; set; }
    public string Category { get; set; } = "Unit";
    public required string Domain { get; set; }
    public int? EnergyCost { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string? ChampionTag { get; set; }
    /// <summary>
    /// Groups legend variants under a single name (e.g. "Kai'Sa" for OGN-247, OGN-299, OGN-299-STAR).
    /// Extracted from the card name before " - ".
    /// </summary>
    public string? LegendGroup { get; set; }
}