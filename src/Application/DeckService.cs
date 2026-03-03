using FluentValidation;

public class DeckService : IDeckService
{
    private readonly RiftContext _context;
    private readonly IValidator<Deck> _validator;

    public DeckService(RiftContext context, IValidator<Deck> validator)
    {
        _context = context;
        _validator = validator;
    }

    public async Task<Result<Guid>> CreateDeckAsync(Deck deck)
    {
        var validationResult = await _validator.ValidateAsync(deck);
        if (!validationResult.IsValid)
        {
            return Result<Guid>.Failure(validationResult.Errors.Select(e => e.ErrorMessage));
        }
        _context.Decks.Add(deck);
        await _context.SaveChangesAsync();
        return Result<Guid>.Success(deck.Id);
        
    }
}