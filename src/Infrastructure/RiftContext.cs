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

        modelBuilder.Entity<TournamentResult>()
            .HasOne(tr => tr.Tournament)
            .WithMany(t => t.Results)
            .HasForeignKey(tr => tr.TournamentId);

        base.OnModelCreating(modelBuilder);   
    }



}