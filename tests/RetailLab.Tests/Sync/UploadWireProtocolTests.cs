using System.Text;
using RetailLab.Web.Sync;

namespace RetailLab.Tests.Sync;

public sealed class UploadWireProtocolTests
{
    [Fact]
    public void Fingerprint_DistinguishesMissingNullAndEmptyValuesBeforeCoreConstruction()
    {
        var operationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var missing = ParseDraft(operationId, productId, "\"price\":12.5");
        var nullValue = ParseDraft(operationId, productId, "\"description\":null,\"price\":12.5");
        var emptyValue = ParseDraft(operationId, productId, "\"description\":\"\",\"price\":12.5");

        var fingerprints = new[]
        {
            missing.ComputeFingerprint(),
            nullValue.ComputeFingerprint(),
            emptyValue.ComputeFingerprint()
        };

        Assert.Equal(3, fingerprints.Distinct().Count());
        Assert.All(fingerprints, fingerprint =>
        {
            Assert.Equal(64, fingerprint.Length);
            Assert.Equal(fingerprint.ToLowerInvariant(), fingerprint);
        });
    }

    [Fact]
    public void AdjustmentFingerprint_UsesNewFormatAndIncludesDeltaAndExactReason()
    {
        var operationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var first = ParseAdjustmentDraft(operationId, productId, "\"delta\":5,\"reason\":\"restock\"");
        var changedDelta = ParseAdjustmentDraft(operationId, productId, "\"delta\":6,\"reason\":\"restock\"");
        var changedCase = ParseAdjustmentDraft(operationId, productId, "\"delta\":5,\"reason\":\"Restock\"");
        var missingReason = ParseAdjustmentDraft(operationId, productId, "\"delta\":5");
        var nullReason = ParseAdjustmentDraft(operationId, productId, "\"delta\":5,\"reason\":null");

        Assert.Equal(
            5,
            new[]
            {
                first.ComputeFingerprint(),
                changedDelta.ComputeFingerprint(),
                changedCase.ComputeFingerprint(),
                missingReason.ComputeFingerprint(),
                nullReason.ComputeFingerprint()
            }.Distinct().Count());
    }

    [Theory]
    [InlineData("\"delta\":0,\"reason\":\"correction\"", "invalid_adjustment")]
    [InlineData("\"delta\":1", "invalid_reason")]
    [InlineData("\"delta\":1,\"reason\":\"Damage\"", "invalid_reason")]
    [InlineData("\"delta\":1,\"reason\":\"damage\"", "reason_sign_mismatch")]
    public void AdjustmentDraft_ReturnsStableValidationCode(string details, string expectedCode)
    {
        var draft = ParseAdjustmentDraft(Guid.NewGuid(), Guid.NewGuid(), details);

        Assert.False(draft.TryCreateCoreOperation(out var operation, out var code));
        Assert.Null(operation);
        Assert.Equal(expectedCode, code);
    }

    private static UploadWireOperationDraft ParseDraft(
        Guid operationId,
        Guid productId,
        string detailProperties)
    {
        var json = $$"""
            {
              "protocolVersion": 1,
              "serverInstanceId": "{{Guid.NewGuid():D}}",
              "operations": [
                {
                  "operationId": "{{operationId:D}}",
                  "kind": "updateProductDetails",
                  "productId": "{{productId:D}}",
                  "createdAtUtc": "2026-10-21T09:14:03Z",
                  "baseServerRevision": 1,
                  "details": { {{detailProperties}} }
                }
              ]
            }
            """;
        return Assert.Single(UploadWireParser.Parse(Encoding.UTF8.GetBytes(json)).Operations);
    }

    private static UploadWireOperationDraft ParseAdjustmentDraft(
        Guid operationId,
        Guid productId,
        string detailProperties)
    {
        var json = $$"""
            {
              "protocolVersion": 1,
              "serverInstanceId": "{{Guid.NewGuid():D}}",
              "operations": [
                {
                  "operationId": "{{operationId:D}}",
                  "kind": "adjustStock",
                  "productId": "{{productId:D}}",
                  "createdAtUtc": "2026-10-21T09:14:03Z",
                  "baseServerRevision": 1,
                  "details": { {{detailProperties}} }
                }
              ]
            }
            """;
        return Assert.Single(UploadWireParser.Parse(Encoding.UTF8.GetBytes(json)).Operations);
    }
}
