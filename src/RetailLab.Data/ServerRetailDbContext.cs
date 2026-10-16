using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Server-only context that turns every authoritative product save into one
/// durable change-feed entry in the same EF Core transaction.
/// </summary>
public sealed class ServerRetailDbContext(
    DbContextOptions<ServerRetailDbContext> options)
    : RetailLabDbContext(options)
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new NotSupportedException(
            "ServerRetailDbContext requires SaveChangesAsync so feed publication cannot be bypassed.");
    }

    public override int SaveChanges()
    {
        throw new NotSupportedException(
            "ServerRetailDbContext requires SaveChangesAsync so feed publication cannot be bypassed.");
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var derivation = new FeedDerivation([], []);

        try
        {
            DeriveFeedEntries(derivation);
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            RestoreFailedDerivation(derivation);
            throw;
        }
    }

    private void DeriveFeedEntries(FeedDerivation derivation)
    {
        var products = ChangeTracker
            .Entries<Product>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToList();

        var occurredAtUtc = DateTimeOffset.UtcNow;

        foreach (var entry in products)
        {
            var previousRevision = entry.Entity.ServerRevision;
            derivation.PreviousRevisions.Add(new ProductRevision(entry, previousRevision));

            entry.Entity.ApplyServerRevision(checked(previousRevision + 1));

            var changeKind = GetChangeKind(entry);
            var snapshot = ProductSnapshot.FromProduct(entry.Entity);
            var feedEntry = new SyncChangeFeedEntry(
                entry.Entity.Id,
                changeKind,
                entry.Entity.ServerRevision,
                JsonSerializer.Serialize(snapshot),
                occurredAtUtc);

            derivation.FeedEntries.Add(feedEntry);
            SyncChangeFeedEntries.Add(feedEntry);
        }
    }

    private static ProductChangeKind GetChangeKind(EntityEntry<Product> entry)
    {
        if (entry.State == EntityState.Added)
        {
            return ProductChangeKind.Created;
        }

        var archiveProperty = entry.Property(product => product.IsArchived);
        if (archiveProperty.IsModified && archiveProperty.CurrentValue != archiveProperty.OriginalValue)
        {
            return archiveProperty.CurrentValue
                ? ProductChangeKind.Archived
                : ProductChangeKind.Unarchived;
        }

        return ProductChangeKind.Updated;
    }

    private void RestoreFailedDerivation(FeedDerivation derivation)
    {
        foreach (var revision in derivation.PreviousRevisions)
        {
            revision.Entry.Property(product => product.ServerRevision).CurrentValue = revision.PreviousRevision;
        }

        foreach (var feedEntry in derivation.FeedEntries)
        {
            Entry(feedEntry).State = EntityState.Detached;
        }
    }

    private sealed record ProductRevision(EntityEntry<Product> Entry, long PreviousRevision);

    private sealed record FeedDerivation(
        List<ProductRevision> PreviousRevisions,
        List<SyncChangeFeedEntry> FeedEntries);
}
