namespace RetailLab.Core;

internal static class CausalChainValidator
{
    public static void Validate(long? baseServerRevision, Guid? predecessorOperationId)
    {
        var hasBaseRevision = baseServerRevision.HasValue;
        var hasPredecessor = predecessorOperationId.HasValue && predecessorOperationId.Value != Guid.Empty;

        if (predecessorOperationId == Guid.Empty)
        {
            throw new ArgumentException("Predecessor operation ID cannot be empty.", nameof(predecessorOperationId));
        }

        if (hasBaseRevision && baseServerRevision!.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseServerRevision),
                "Base server revision cannot be negative.");
        }

        if (hasBaseRevision == hasPredecessor)
        {
            throw new BusinessRuleException(
                "An update, archive, or unarchive operation must have either a base server revision or a predecessor operation ID, but not both.");
        }
    }
}
