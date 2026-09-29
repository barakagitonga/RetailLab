namespace RetailLab.Core;

/// <summary>
/// The basket changed under a request: a concurrent first add collided on
/// the basket key, or a stale update/delete lost an optimistic-concurrency
/// check. Presentation layers catch this (never EF exception types) and ask
/// the customer to look at the current basket and try again.
/// </summary>
public sealed class BasketConflictException : Exception
{
    public BasketConflictException()
        : base("Your basket changed; please try again.")
    {
    }

    public BasketConflictException(Exception? innerException)
        : base("Your basket changed; please try again.", innerException)
    {
    }
}
