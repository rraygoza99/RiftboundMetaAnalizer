public interface IDeckService
{
    Task<Result<Guid>> CreateDeckAsync(Deck deck);
}