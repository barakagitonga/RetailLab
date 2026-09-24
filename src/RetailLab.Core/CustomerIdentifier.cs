namespace RetailLab.Core;

public static class CustomerIdentifier
{
    public const int MaximumLength = 100;

    public static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Customer identifiers cannot exceed {MaximumLength} characters.",
                nameof(value));
        }

        return normalized;
    }
}
