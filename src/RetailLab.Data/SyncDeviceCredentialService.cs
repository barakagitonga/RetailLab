using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace RetailLab.Data;

/// <summary>
/// Generates high-entropy device credentials and delegates their storage
/// protection and verification to ASP.NET Core Identity's password hasher.
/// </summary>
public sealed class SyncDeviceCredentialService(IPasswordHasher<SyncDevice> passwordHasher)
{
    public const int SecretByteCount = 32;

    public string IssueNewSecret(SyncDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var secret = ToBase64Url(RandomNumberGenerator.GetBytes(SecretByteCount));
        device.ReplaceKeyHash(passwordHasher.HashPassword(device, secret));
        return secret;
    }

    public bool Verify(SyncDevice device, string? presentedSecret)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (string.IsNullOrWhiteSpace(presentedSecret) || string.IsNullOrWhiteSpace(device.KeyHash))
        {
            return false;
        }

        var result = passwordHasher.VerifyHashedPassword(
            device,
            device.KeyHash,
            presentedSecret);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            device.ReplaceKeyHash(passwordHasher.HashPassword(device, presentedSecret));
        }

        return result != PasswordVerificationResult.Failed;
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
