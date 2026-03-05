using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

public class RiftContext : DbContext
{
    public RiftContext(DbContextOptions<RiftContext> options) : base(options)
    {
        
    }

    public DbSet<Card> Cards { get; set; }
    public DbSet<Deck> Decks { get; set; }
    public DbSet<DeckCard> DeckCards { get; set; }
    public DbSet<Tournament> Tournaments { get; set; }
    public DbSet<TournamentResult> TournamentResults { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeckCard>()
            .HasKey(dc => new { dc.DeckId, dc.CardId });

        modelBuilder.Entity<DeckCard>()
            .HasOne(dc => dc.Deck)
            .WithMany(d => d.DeckCards)
            .HasForeignKey(dc => dc.DeckId);

        modelBuilder.Entity<TournamentResult>()
            .HasOne(tr => tr.Tournament)
            .WithMany(t => t.Results)
            .HasForeignKey(tr => tr.TournamentId);

        modelBuilder.Entity<TournamentResult>()
            .HasOne(tr => tr.Deck)
            .WithMany(d => d.TournamentResults)
            .HasForeignKey(tr => tr.DeckId);

        base.OnModelCreating(modelBuilder);   
    }



}