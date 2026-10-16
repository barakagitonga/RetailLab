using RetailLab.Web.Sync;

namespace RetailLab.Tests.Sync;

public sealed class SyncKeyFilterTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("configured", "")]
    [InlineData("configured", "wrong")]
    public void IsValid_RejectsMissingOrWrongKeys(string? configured, string? supplied)
    {
        Assert.False(RequireSyncKeyFilter.IsValid(configured, supplied));
    }

    [Fact]
    public void IsValid_AcceptsExactKeyWithoutExposingIt()
    {
        const string key = "test-only-key";

        Assert.True(RequireSyncKeyFilter.IsValid(key, key));
    }
}
