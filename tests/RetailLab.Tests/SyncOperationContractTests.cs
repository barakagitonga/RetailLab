using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class SyncOperationContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateProductOperation_InitializesValidPropertiesWithoutCausalMetadata()
    {
        var opId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        var op = new CreateProductOperation(
            opId,
            prodId,
            "SKU-NEW",
            "New synced product",
            29.99m,
            12,
            Now);

        Assert.Equal(opId, op.OperationId);
        Assert.Equal(prodId, op.ProductId);
        Assert.Equal(SyncOperationKind.CreateProduct, op.Kind);
        Assert.Equal("SKU-NEW", op.Sku);
        Assert.Equal("New synced product", op.Description);
        Assert.Equal(29.99m, op.Price);
        Assert.Equal(12, op.InitialStockQuantity);
        Assert.Equal(Now.ToUniversalTime(), op.CreatedAtUtc);
    }

    [Fact]
    public void CreateProductOperation_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentException>(
            () => new CreateProductOperation(Guid.Empty, Guid.NewGuid(), "SKU", "Desc", 1m, 1, Now));
        Assert.Throws<ArgumentException>(
            () => new CreateProductOperation(Guid.NewGuid(), Guid.Empty, "SKU", "Desc", 1m, 1, Now));
        Assert.Throws<ArgumentException>(
            () => new CreateProductOperation(Guid.NewGuid(), Guid.NewGuid(), "", "Desc", 1m, 1, Now));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CreateProductOperation(Guid.NewGuid(), Guid.NewGuid(), "SKU", "Desc", -0.01m, 1, Now));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CreateProductOperation(Guid.NewGuid(), Guid.NewGuid(), "SKU", "Desc", 1m, -1, Now));
    }

    [Fact]
    public void UpdateDetailsOperation_BasedOnServer_AcceptsBaseRevision()
    {
        var opId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        var op = UpdateProductDetailsOperation.BasedOnServer(
            opId,
            prodId,
            "Updated Description",
            34.50m,
            baseServerRevision: 4L,
            createdAtUtc: Now);

        Assert.Equal(opId, op.OperationId);
        Assert.Equal(prodId, op.ProductId);
        Assert.Equal(SyncOperationKind.UpdateProductDetails, op.Kind);
        Assert.Equal("Updated Description", op.Description);
        Assert.Equal(34.50m, op.Price);
        Assert.Equal(4L, op.BaseServerRevision);
        Assert.Null(op.PredecessorOperationId);
    }

    [Fact]
    public void UpdateDetailsOperation_Chained_AcceptsPredecessorId()
    {
        var opId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var predId = Guid.NewGuid();

        var op = UpdateProductDetailsOperation.Chained(
            opId,
            prodId,
            "Chained Description",
            19.00m,
            predecessorOperationId: predId,
            createdAtUtc: Now);

        Assert.Equal(opId, op.OperationId);
        Assert.Equal(prodId, op.ProductId);
        Assert.Null(op.BaseServerRevision);
        Assert.Equal(predId, op.PredecessorOperationId);
    }

    [Fact]
    public void CausalOperations_EnforceEitherBaseRevisionOrPredecessor_NeverBoth_NeverNeither()
    {
        var opId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var predId = Guid.NewGuid();

        // Both specified -> BusinessRuleException
        var exBoth = Assert.Throws<BusinessRuleException>(() => new UpdateProductDetailsOperation(
            opId, prodId, "Desc", 10m, baseServerRevision: 2L, predecessorOperationId: predId, Now));
        Assert.Contains("either a base server revision or a predecessor operation ID, but not both", exBoth.Message);

        // Neither specified -> BusinessRuleException
        var exNeither = Assert.Throws<BusinessRuleException>(() => new UpdateProductDetailsOperation(
            opId, prodId, "Desc", 10m, baseServerRevision: null, predecessorOperationId: null, Now));
        Assert.Contains("either a base server revision or a predecessor operation ID, but not both", exNeither.Message);

        // Archive with both or neither
        Assert.Throws<BusinessRuleException>(() => new ArchiveProductOperation(
            opId, prodId, baseServerRevision: 1L, predecessorOperationId: predId, Now));
        Assert.Throws<BusinessRuleException>(() => new ArchiveProductOperation(
            opId, prodId, baseServerRevision: null, predecessorOperationId: null, Now));

        // Unarchive with both or neither
        Assert.Throws<BusinessRuleException>(() => new UnarchiveProductOperation(
            opId, prodId, baseServerRevision: 1L, predecessorOperationId: predId, Now));
        Assert.Throws<BusinessRuleException>(() => new UnarchiveProductOperation(
            opId, prodId, baseServerRevision: null, predecessorOperationId: null, Now));
    }

    [Fact]
    public void CausalOperations_RejectNegativeRevisionAndEmptyPredecessorGuid()
    {
        var opId = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => new UpdateProductDetailsOperation(
            opId, prodId, "Desc", 10m, baseServerRevision: -1L, predecessorOperationId: null, Now));

        Assert.Throws<ArgumentException>(() => new UpdateProductDetailsOperation(
            opId, prodId, "Desc", 10m, baseServerRevision: null, predecessorOperationId: Guid.Empty, Now));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ArchiveProductOperation(
            opId, prodId, baseServerRevision: -5L, predecessorOperationId: null, Now));

        Assert.Throws<ArgumentException>(() => new UnarchiveProductOperation(
            opId, prodId, baseServerRevision: null, predecessorOperationId: Guid.Empty, Now));
    }

    [Theory]
    [InlineData(5, SynchronizedAdjustmentReasons.Restock)]
    [InlineData(-2, SynchronizedAdjustmentReasons.Damage)]
    [InlineData(-1, SynchronizedAdjustmentReasons.Shrinkage)]
    [InlineData(3, SynchronizedAdjustmentReasons.Correction)]
    [InlineData(-3, SynchronizedAdjustmentReasons.Correction)]
    public void AdjustStockOperation_AcceptsSupportedReasonAndSignCombinations(
        int delta,
        string reason)
    {
        var operationId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var operation = AdjustStockOperation.BasedOnServer(
            operationId,
            productId,
            delta,
            reason,
            baseServerRevision: 7,
            createdAtUtc: Now);

        Assert.Equal(operationId, operation.OperationId);
        Assert.Equal(productId, operation.ProductId);
        Assert.Equal(SyncOperationKind.AdjustStock, operation.Kind);
        Assert.Equal(delta, operation.Delta);
        Assert.Equal(reason, operation.Reason);
        Assert.Equal(7, operation.BaseServerRevision);
        Assert.Null(operation.PredecessorOperationId);
    }

    [Theory]
    [InlineData(0, SynchronizedAdjustmentReasons.Correction)]
    [InlineData(-1, SynchronizedAdjustmentReasons.Restock)]
    [InlineData(1, SynchronizedAdjustmentReasons.Damage)]
    [InlineData(1, SynchronizedAdjustmentReasons.Shrinkage)]
    [InlineData(1, "Restock")]
    [InlineData(1, "other")]
    public void AdjustStockOperation_RejectsInvalidDeltaReasonOrSign(int delta, string reason)
    {
        Assert.Throws<BusinessRuleException>(() => AdjustStockOperation.BasedOnServer(
            Guid.NewGuid(),
            Guid.NewGuid(),
            delta,
            reason,
            baseServerRevision: 1,
            createdAtUtc: Now));
    }

    [Fact]
    public void AdjustStockOperation_Chained_AcceptsPredecessorId()
    {
        var predecessorId = Guid.NewGuid();

        var operation = AdjustStockOperation.Chained(
            Guid.NewGuid(),
            Guid.NewGuid(),
            -2,
            SynchronizedAdjustmentReasons.Correction,
            predecessorId,
            Now);

        Assert.Null(operation.BaseServerRevision);
        Assert.Equal(predecessorId, operation.PredecessorOperationId);
    }

    [Fact]
    public void ProductSnapshot_CapturesAndPreservesProductState()
    {
        var product = new Product("SNAP-01", "Snapshot item", 45.00m, 8);
        product.ApplyServerRevision(12L);
        product.Archive(Now);

        var snapshot = ProductSnapshot.FromProduct(product);

        Assert.Equal(product.Id, snapshot.Id);
        Assert.Equal("SNAP-01", snapshot.Sku);
        Assert.Equal("Snapshot item", snapshot.Description);
        Assert.Equal(45.00m, snapshot.Price);
        Assert.Equal(8, snapshot.StockQuantity);
        Assert.True(snapshot.IsArchived);
        Assert.Equal(Now.ToUniversalTime(), snapshot.ArchivedAtUtc);
        Assert.Equal(12L, snapshot.ServerRevision);
    }

    [Fact]
    public void ServerChangeEntry_InitializesAndValidates()
    {
        var prodId = Guid.NewGuid();
        var snapshot = new ProductSnapshot(prodId, "FEED-01", "Feed item", 10m, 5, false, null, 3L);

        var entry = new ServerChangeEntry(
            sequence: 101L,
            productId: prodId,
            changeKind: ProductChangeKind.Created,
            serverRevision: 3L,
            snapshot: snapshot,
            occurredAtUtc: Now);

        Assert.Equal(101L, entry.Sequence);
        Assert.Equal(prodId, entry.ProductId);
        Assert.Equal(ProductChangeKind.Created, entry.ChangeKind);
        Assert.Equal(3L, entry.ServerRevision);
        Assert.Equal(snapshot, entry.Snapshot);
        Assert.Equal(Now.ToUniversalTime(), entry.OccurredAtUtc);
    }

    [Fact]
    public void SyncOperationResult_FactoriesInitializeExpectedOutcomes()
    {
        var opId = Guid.NewGuid();
        var accept = SyncOperationResult.Accept(opId, resultingServerRevision: 5L);
        Assert.Equal(opId, accept.OperationId);
        Assert.Equal(SyncOperationOutcome.Accepted, accept.Outcome);
        Assert.Equal(5L, accept.ResultingServerRevision);
        Assert.Null(accept.Message);

        var conflict = SyncOperationResult.Conflict(opId, "Stale revision", null);
        Assert.Equal(opId, conflict.OperationId);
        Assert.Equal(SyncOperationOutcome.Conflict, conflict.Outcome);
        Assert.Equal("Stale revision", conflict.Message);

        var reject = SyncOperationResult.Reject(opId, "Invalid SKU format");
        Assert.Equal(opId, reject.OperationId);
        Assert.Equal(SyncOperationOutcome.Rejected, reject.Outcome);
        Assert.Equal("Invalid SKU format", reject.Message);
    }

    [Fact]
    public void UploadContracts_KeepOneOperationIdentityAndValidateTerminalResults()
    {
        var deviceId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var operation = new CreateProductOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UPLOAD-01",
            "Upload contract",
            12.50m,
            3,
            Now);

        var request = new UploadRequest(deviceId, serverId, [operation]);
        var result = new UploadOperationResult(
            operation.OperationId,
            SyncOperationOutcome.Accepted,
            code: null,
            resultingServerRevision: 1,
            snapshot: null,
            replay: false);
        var response = new UploadResponse(
            SyncProtocolVersions.Upload,
            serverId,
            Now,
            [result]);

        Assert.Same(operation, request.Operations.Single());
        Assert.Equal(operation.OperationId, response.Results.Single().OperationId);
        Assert.Throws<ArgumentException>(() => new UploadOperationResult(
            operation.OperationId,
            SyncOperationOutcome.Accepted,
            code: null,
            resultingServerRevision: null,
            snapshot: null,
            replay: false));
        Assert.Throws<ArgumentException>(() => new UploadOperationResult(
            operation.OperationId,
            SyncOperationOutcome.Rejected,
            code: null,
            resultingServerRevision: null,
            snapshot: null,
            replay: false));
    }
}
