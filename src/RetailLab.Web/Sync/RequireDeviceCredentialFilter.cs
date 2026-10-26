using Microsoft.EntityFrameworkCore;
using RetailLab.Data;

namespace RetailLab.Web.Sync;

/// <summary>
/// Authenticates one enrolled sync device and separately authorizes its role.
/// All credential failures intentionally return the same external response.
/// </summary>
public sealed class RequireDeviceCredentialFilter(
    ServerRetailDbContext dbContext,
    SyncDeviceCredentialService credentialService,
    TimeProvider timeProvider) : IEndpointFilter
{
    public const string DeviceIdHeaderName = "X-RetailLab-Device-Id";
    public const string DeviceKeyHeaderName = "X-RetailLab-Device-Key";

    private static readonly object AuthenticatedDeviceKey = new();

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var idValues = context.HttpContext.Request.Headers[DeviceIdHeaderName];
        var keyValues = context.HttpContext.Request.Headers[DeviceKeyHeaderName];
        if (idValues.Count != 1 ||
            keyValues.Count != 1 ||
            !Guid.TryParse(idValues[0], out var deviceId) ||
            deviceId == Guid.Empty)
        {
            return InvalidCredential();
        }

        var device = await dbContext.SyncDevices.SingleOrDefaultAsync(
            item => item.Id == deviceId,
            context.HttpContext.RequestAborted);
        if (device is null ||
            device.Status != SyncDeviceStatus.Active ||
            !credentialService.Verify(device, keyValues[0]))
        {
            return InvalidCredential();
        }

        device.RecordSuccessfulAuthentication(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(context.HttpContext.RequestAborted);

        if (!string.Equals(device.Role, SyncDeviceRoles.Staff, StringComparison.Ordinal))
        {
            return Results.Json(
                new SyncApiError(
                    "unauthorized_device",
                    "The authenticated device is not permitted to upload product changes."),
                statusCode: StatusCodes.Status403Forbidden);
        }

        context.HttpContext.Items[AuthenticatedDeviceKey] = device;
        return await next(context);
    }

    public static SyncDevice GetAuthenticatedDevice(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(AuthenticatedDeviceKey, out var value) &&
            value is SyncDevice device)
        {
            return device;
        }

        throw new InvalidOperationException("The sync device authentication filter did not run.");
    }

    private static IResult InvalidCredential() =>
        Results.Json(
            new SyncApiError(
                "invalid_device_credential",
                "The device credential is invalid."),
            statusCode: StatusCodes.Status401Unauthorized);
}
