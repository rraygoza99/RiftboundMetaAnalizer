public class TournamentResult {
    public Guid Id { get; set; }
    public int TournamentId { get; set; }
    public required Tournament Tournament { get; set; }
    public int Standing { get; set; }
    public Guid DeckId { get; set; }
    public required Deck Deck { get; set; }
}