public class TournamentResult {
    public Guid Id { get; set; }
    public required string TournamentName { get; set; }
    public int Standing { get; set; } // e.g., 1 for Winner, 4 for Top 4
    public Guid DeckId { get; set; }
    public required Deck Deck { get; set; }
    public DateTime Date { get; set; }
}