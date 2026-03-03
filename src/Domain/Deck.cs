public class Deck{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string LegendCardId { get; set; }
    public required Card Legend { get; set; }
    public List<DeckCard> DeckCards { get; set; } = new();
}