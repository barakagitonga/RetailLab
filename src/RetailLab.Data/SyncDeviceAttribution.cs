namespace RetailLab.Data;

/// <summary>
/// Immutable server-resolved identity copied onto each upload receipt.
/// </summary>
public sealed record SyncDeviceAttribution(
    Guid DeviceId,
    string DeviceDisplayName,
    string ActorDisplayName,
    string DeviceRole)
{
    public static SyncDeviceAttribution FromDevice(SyncDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new SyncDeviceAttribution(
            device.Id,
            device.DisplayName,
            device.StaffDisplayName,
            device.Role);
    }
}
