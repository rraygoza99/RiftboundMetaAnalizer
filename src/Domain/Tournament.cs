public class Tournament
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTime Date { get; set; }
    public List<TournamentResult> Results { get; set; } = new();
}