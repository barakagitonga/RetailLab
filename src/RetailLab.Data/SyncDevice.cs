namespace RetailLab.Data;

/// <summary>
/// Server-side identity for one enrolled synchronization client. The ID is a
/// public identifier; the separately verified key proves possession.
/// </summary>
public sealed class SyncDevice
{
    public const int MaximumDisplayNameLength = 100;
    public const int MaximumStaffDisplayNameLength = 100;
    public const int MaximumRoleLength = 50;

    private SyncDevice()
    {
    }

    public SyncDevice(
        Guid id,
        string displayName,
        string staffDisplayName,
        string role,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(id));
        }

        Id = id;
        DisplayName = NormalizeRequired(displayName, MaximumDisplayNameLength, nameof(displayName));
        StaffDisplayName = NormalizeRequired(
            staffDisplayName,
            MaximumStaffDisplayNameLength,
            nameof(staffDisplayName));
        Role = NormalizeRequired(role, MaximumRoleLength, nameof(role));
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Status = SyncDeviceStatus.Active;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    public string StaffDisplayName { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public string KeyHash { get; private set; } = string.Empty;

    public SyncDeviceStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? RevokedAtUtc { get; private set; }

    public DateTimeOffset? LastSeenAtUtc { get; private set; }

    public void ReplaceKeyHash(string keyHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);
        KeyHash = keyHash;
    }

    public void RecordSuccessfulAuthentication(DateTimeOffset seenAtUtc)
    {
        if (Status != SyncDeviceStatus.Active)
        {
            throw new InvalidOperationException("A revoked device cannot record successful authentication.");
        }

        LastSeenAtUtc = seenAtUtc.ToUniversalTime();
    }

    public void Revoke(DateTimeOffset revokedAtUtc)
    {
        Status = SyncDeviceStatus.Revoked;
        RevokedAtUtc = revokedAtUtc.ToUniversalTime();
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameterName} cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
