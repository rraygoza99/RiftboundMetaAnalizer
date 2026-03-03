
using FluentValidation;

public class DeckValidator : AbstractValidator<Deck>
{
    public DeckValidator()
    {
        RuleFor(deck => deck.Name).NotEmpty().MaximumLength(100).WithMessage("Deck name must be at most 100 characters");
        RuleFor(deck => deck.LegendCardId).NotEmpty().WithMessage("A Riftbound deck must have a Legend card");
        RuleFor(deck => deck.DeckCards).Must(cards => cards.Count(c => c.Card.CardType == CardType.Rune) == 12).WithMessage("Deck must contain exactly 12 Runes");
        RuleFor(deck => deck).Must(deck => deck.DeckCards.All(dc => 
        dc.Card.Domain == deck.Legend.Domain || dc.Card.Domain == "Neutral")).WithMessage("All cards in the deck must match the Legend's domain");
    }
}