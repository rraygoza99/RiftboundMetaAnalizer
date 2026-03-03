public class DeckCard {
    public Guid DeckId { get; set; }
    public string CardId { get; set; }
    public Card Card { get; set; }
    public int Quantity { get; set; }
}