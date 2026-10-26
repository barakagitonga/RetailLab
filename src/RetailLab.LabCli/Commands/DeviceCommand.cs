using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Sync;

namespace RetailLab.LabCli;

/// <summary>
/// Headless administration for upload credentials. Secrets are returned only
/// when issued because the database keeps a one-way password hash, not a copy.
/// </summary>
public static class DeviceCommand
{
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;
    private const int UsageExitCode = 2;

    public static async Task<int> RunAsync(string command, string[] args)
    {
        if (!TryParse(command, args, out var options, out var error))
        {
            if (error is not null)
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
            }

            PrintHelp(command);
            return error is null ? SuccessExitCode : UsageExitCode;
        }

        try
        {
            var database = new RetailLabSqliteDatabase(options.DataDirectory);
            await using (var migrationContext = database.CreateDbContext())
            {
                // Administration upgrades the schema but never inserts sample products.
                await DatabaseInitializer.MigrateAsync(migrationContext);
            }

            await using var context = database.CreateServerDbContext();
            var credentials = new SyncDeviceCredentialService(new PasswordHasher<SyncDevice>());

            return command switch
            {
                "enroll-device" => await EnrollAsync(context, credentials, options),
                "rotate-device" => await RotateAsync(context, credentials, options),
                "revoke-device" => await RevokeAsync(context, options),
                _ => UsageExitCode
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or DbUpdateException)
        {
            Console.Error.WriteLine($"Device administration failed: {exception.Message}");
            return FailureExitCode;
        }
    }

    private static async Task<int> EnrollAsync(
        ServerRetailDbContext context,
        SyncDeviceCredentialService credentials,
        Options options)
    {
        if (await context.SyncDevices.AnyAsync(device => device.Id == options.DeviceId))
        {
            Console.Error.WriteLine($"Device {options.DeviceId:D} is already enrolled.");
            return FailureExitCode;
        }

        var device = new SyncDevice(
            options.DeviceId,
            options.DeviceName!,
            options.StaffName!,
            SyncDeviceRoles.Staff,
            DateTimeOffset.UtcNow);
        var secret = credentials.IssueNewSecret(device);
        context.SyncDevices.Add(device);
        await context.SaveChangesAsync();

        WriteIssuedSecret("enrolled", device, secret);
        return SuccessExitCode;
    }

    private static async Task<int> RotateAsync(
        ServerRetailDbContext context,
        SyncDeviceCredentialService credentials,
        Options options)
    {
        var device = await context.SyncDevices.SingleOrDefaultAsync(item => item.Id == options.DeviceId);
        if (device is null)
        {
            Console.Error.WriteLine($"Device {options.DeviceId:D} is not enrolled.");
            return FailureExitCode;
        }

        if (device.Status != SyncDeviceStatus.Active)
        {
            Console.Error.WriteLine("A revoked device cannot have its key rotated; enroll a new device ID.");
            return FailureExitCode;
        }

        var secret = credentials.IssueNewSecret(device);
        await context.SaveChangesAsync();
        WriteIssuedSecret("rotated", device, secret);
        return SuccessExitCode;
    }

    private static async Task<int> RevokeAsync(ServerRetailDbContext context, Options options)
    {
        var device = await context.SyncDevices.SingleOrDefaultAsync(item => item.Id == options.DeviceId);
        if (device is null)
        {
            Console.Error.WriteLine($"Device {options.DeviceId:D} is not enrolled.");
            return FailureExitCode;
        }

        if (device.Status == SyncDeviceStatus.Revoked)
        {
            Console.WriteLine($"Device {device.Id:D} is already revoked.");
            return SuccessExitCode;
        }

        device.Revoke(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
        Console.WriteLine($"Revoked device {device.Id:D} ({device.DisplayName}).");
        return SuccessExitCode;
    }

    private static void WriteIssuedSecret(string action, SyncDevice device, string secret)
    {
        Console.WriteLine($"Device {action}: {device.Id:D} ({device.DisplayName})");
        Console.WriteLine($"{HttpSyncTransport.DeviceKeyEnvironmentVariable}={secret}");
        Console.WriteLine("Store this key securely now. RetailLab cannot display it again.");
    }

    private static bool TryParse(
        string command,
        string[] args,
        out Options options,
        out string? error)
    {
        options = null!;
        error = null;
        string? dataDirectory = null;
        Guid deviceId = Guid.Empty;
        string? deviceName = null;
        string? staffName = null;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (argument is "--help" or "-h")
            {
                return false;
            }

            if (argument is not ("--data-directory" or "--device-id" or "--name" or "--staff"))
            {
                error = $"Unknown option: {argument}.";
                return false;
            }

            if (++index >= args.Length)
            {
                error = $"Missing value for {argument}.";
                return false;
            }

            var value = args[index];
            switch (argument)
            {
                case "--data-directory":
                    try
                    {
                        dataDirectory = Path.GetFullPath(value);
                    }
                    catch (Exception exception) when (
                        exception is ArgumentException or NotSupportedException or PathTooLongException)
                    {
                        error = $"--data-directory is not a valid path: {exception.Message}";
                        return false;
                    }
                    break;
                case "--device-id":
                    if (!Guid.TryParse(value, out deviceId) || deviceId == Guid.Empty)
                    {
                        error = "--device-id must be a non-empty GUID generated by the client.";
                        return false;
                    }
                    break;
                case "--name":
                    deviceName = value;
                    break;
                case "--staff":
                    staffName = value;
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            error = "The --data-directory option is required.";
            return false;
        }

        if (deviceId == Guid.Empty)
        {
            error = "The --device-id option is required.";
            return false;
        }

        if (command == "enroll-device" && string.IsNullOrWhiteSpace(deviceName))
        {
            error = "The --name option is required for enrollment.";
            return false;
        }

        if (command == "enroll-device" && string.IsNullOrWhiteSpace(staffName))
        {
            error = "The --staff option is required for enrollment.";
            return false;
        }

        options = new Options(dataDirectory, deviceId, deviceName, staffName);
        return true;
    }

    private static void PrintHelp(string command)
    {
        Console.WriteLine("RetailLab upload-device administration");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        if (command == "enroll-device")
        {
            Console.WriteLine(
                "  dotnet run --project src/RetailLab.LabCli -- enroll-device " +
                "--data-directory <server-data> --device-id <guid> " +
                "--name <device-name> --staff <audit-label>");
        }
        else
        {
            Console.WriteLine(
                $"  dotnet run --project src/RetailLab.LabCli -- {command} " +
                "--data-directory <server-data> --device-id <guid>");
        }
    }

    private sealed record Options(
        string DataDirectory,
        Guid DeviceId,
        string? DeviceName,
        string? StaffName);
}
