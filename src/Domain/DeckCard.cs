public class DeckCard {
    public Guid DeckId { get; set; }
    public required string CardId { get; set; }
    public required Card Card { get; set; }
    public int Quantity { get; set; }
}