namespace RetailLab.Core;

/// <summary>
/// Contract for mutating operations (update details, archive, unarchive) that
/// belong to a causal chain. They must specify either a BaseServerRevision or
/// a PredecessorOperationId, but never both.
/// </summary>
public interface ICausalOperation : ISyncProductOperation
{
    long? BaseServerRevision { get; }

    Guid? PredecessorOperationId { get; }
}
