public class Deck{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string LegendCardId { get; set; }
    public Card Legend { get; set; }
    public List<DeckCard> DeckCards { get; set; }
}