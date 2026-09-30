namespace RetailLab.Core;

/// <summary>
/// A product changed under a staff or console request: a stale update lost an
/// optimistic-concurrency check on the product version. Presentation layers
/// catch this (never EF exception types) and ask for a retry against the
/// current product details. Checkout races use
/// <see cref="CheckoutConflictException" /> instead.
/// </summary>
public sealed class ProductConflictException : Exception
{
    public ProductConflictException()
        : base("A product changed while you were working; please review the current details and try again.")
    {
    }

    public ProductConflictException(Exception? innerException)
        : base("A product changed while you were working; please review the current details and try again.", innerException)
    {
    }
}
