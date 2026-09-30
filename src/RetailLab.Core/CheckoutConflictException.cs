namespace RetailLab.Core;

/// <summary>
/// The checkout lost a persistence race: a product or basket line changed
/// after it was loaded, or a duplicate submission already created the order.
/// Presentation layers catch this (never EF exception types) and ask the
/// customer to review the current basket and try again.
/// </summary>
public sealed class CheckoutConflictException : Exception
{
    public CheckoutConflictException()
        : base("Your basket or a product changed; please review your basket and try again.")
    {
    }

    public CheckoutConflictException(Exception? innerException)
        : base("Your basket or a product changed; please review your basket and try again.", innerException)
    {
    }
}
