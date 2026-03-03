public class Card {
    public required string Id { get; set; }
    public required string Name { get; set; }
    public CardType CardType { get; set; }
    public required string Domain { get; set; }
    public int? EnergyCost { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string? ChampionTag { get; set; }
}