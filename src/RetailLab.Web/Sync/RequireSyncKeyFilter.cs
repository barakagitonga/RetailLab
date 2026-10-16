using System.Security.Cryptography;
using System.Text;

namespace RetailLab.Web.Sync;

/// <summary>
/// Development-only coarse access gate for the read-only synchronization API.
/// It deliberately never logs either the configured or supplied key.
/// </summary>
public sealed class RequireSyncKeyFilter(IConfiguration configuration) : IEndpointFilter
{
    public const string HeaderName = "X-RetailLab-Sync-Key";
    public const string ConfigurationName = "RETAILLAB_SYNC_READ_KEY";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var configuredKey = configuration[ConfigurationName];
        var suppliedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

        if (!IsValid(configuredKey, suppliedKey))
        {
            return Results.Unauthorized();
        }

        return await next(context);
    }

    public static bool IsValid(string? configuredKey, string? suppliedKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrEmpty(suppliedKey))
        {
            return false;
        }

        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        return CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes);
    }
}
