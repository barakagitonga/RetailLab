namespace RetailLab.Core;

/// <summary>
/// Defines the stable reason tokens allowed for stock adjustments captured by an offline device.
/// </summary>
public static class SynchronizedAdjustmentReasons
{
    public const string Restock = "restock";
    public const string Damage = "damage";
    public const string Shrinkage = "shrinkage";
    public const string Correction = "correction";

    public static bool IsKnown(string? reason) =>
        reason is Restock or Damage or Shrinkage or Correction;

    public static bool IsSignAllowed(string reason, int delta) => reason switch
    {
        Restock => delta > 0,
        Damage or Shrinkage => delta < 0,
        Correction => delta != 0,
        _ => false
    };

    public static void Validate(string? reason, int delta)
    {
        if (!IsKnown(reason))
        {
            throw new BusinessRuleException(
                "Adjustment reason must be restock, damage, shrinkage, or correction.");
        }

        if (!IsSignAllowed(reason!, delta))
        {
            throw new BusinessRuleException(
                $"Adjustment reason '{reason}' is not valid for a delta of {delta}.");
        }
    }
}
