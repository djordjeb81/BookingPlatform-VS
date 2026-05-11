using BookingPlatform.Domain.Licensing;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class DeviceLicenseService : IDeviceLicenseService
{
    private const int LicenseLeaseDays = 5;

    private readonly BookingDbContext _dbContext;

    public DeviceLicenseService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegisterDeviceResult> RegisterDeviceAsync(
        long appUserId,
        string hwidHash,
        string computerName,
        string programVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hwidHash))
        {
            return new RegisterDeviceResult
            {
                ErrorMessage = "HWID hash je obavezan.",
                ErrorReasonCode = "hwid_hash_required"
            };
        }

        var now = DateTime.UtcNow;

        var device = await _dbContext.LicensedDevices
            .FirstOrDefaultAsync(
                x => x.AppUserId == appUserId && x.HwidHash == hwidHash,
                cancellationToken);

        if (device is null)
        {
            device = new LicensedDevice
            {
                AppUserId = appUserId,
                HwidHash = hwidHash.Trim(),
                ComputerName = (computerName ?? string.Empty).Trim(),
                ProgramVersion = (programVersion ?? string.Empty).Trim(),
                Status = DeviceLicenseStatus.Pending,
                LastSeenAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.LicensedDevices.Add(device);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RegisterDeviceResult
            {
                Succeeded = true,
                DeviceId = device.Id,
                Status = device.Status,
                HwidHash = device.HwidHash,
                ComputerName = device.ComputerName,
                ProgramVersion = device.ProgramVersion,
                LastSeenAtUtc = device.LastSeenAtUtc,
                ValidUntilUtc = device.ValidUntilUtc,
                Message = "Uređaj je prijavljen i čeka odobrenje."
            };
        }

        device.ComputerName = (computerName ?? string.Empty).Trim();
        device.ProgramVersion = (programVersion ?? string.Empty).Trim();
        device.LastSeenAtUtc = now;
        device.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterDeviceResult
        {
            Succeeded = true,
            DeviceId = device.Id,
            Status = device.Status,
            HwidHash = device.HwidHash,
            ComputerName = device.ComputerName,
            ProgramVersion = device.ProgramVersion,
            LastSeenAtUtc = device.LastSeenAtUtc,
            ValidUntilUtc = device.ValidUntilUtc,
            Message = device.Status switch
            {
                DeviceLicenseStatus.Pending => "Uređaj je već prijavljen i čeka odobrenje.",
                DeviceLicenseStatus.Approved => "Uređaj je već odobren.",
                DeviceLicenseStatus.Blocked => "Uređaj je blokiran.",
                _ => "Status uređaja je ažuriran."
            }
        };
    }

    public async Task<RefreshLicenseResult> RefreshAsync(
        long appUserId,
        string hwidHash,
        string computerName,
        string programVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hwidHash))
        {
            return new RefreshLicenseResult
            {
                ErrorMessage = "HWID hash je obavezan.",
                ErrorReasonCode = "hwid_hash_required"
            };
        }

        var device = await _dbContext.LicensedDevices
            .FirstOrDefaultAsync(
                x => x.AppUserId == appUserId && x.HwidHash == hwidHash,
                cancellationToken);

        if (device is null)
        {
            return new RefreshLicenseResult
            {
                NotFound = true,
                ErrorMessage = "Uređaj nije registrovan.",
                ErrorReasonCode = "device_not_registered"
            };
        }

        var now = DateTime.UtcNow;

        device.ComputerName = (computerName ?? string.Empty).Trim();
        device.ProgramVersion = (programVersion ?? string.Empty).Trim();
        device.LastSeenAtUtc = now;
        device.UpdatedAtUtc = now;

        if (device.Status == DeviceLicenseStatus.Pending)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshLicenseResult
            {
                Succeeded = true,
                DeviceId = device.Id,
                Status = device.Status,
                IsApproved = false,
                LastSeenAtUtc = device.LastSeenAtUtc,
                LastLicenseRefreshAtUtc = device.LastLicenseRefreshAtUtc,
                ValidUntilUtc = device.ValidUntilUtc,
                Message = "Uređaj još nije odobren."
            };
        }

        if (device.Status == DeviceLicenseStatus.Blocked)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RefreshLicenseResult
            {
                Succeeded = true,
                DeviceId = device.Id,
                Status = device.Status,
                IsApproved = false,
                LastSeenAtUtc = device.LastSeenAtUtc,
                LastLicenseRefreshAtUtc = device.LastLicenseRefreshAtUtc,
                ValidUntilUtc = device.ValidUntilUtc,
                Message = "Uređaj je blokiran."
            };
        }

        device.LicenseToken = Guid.NewGuid().ToString("N");
        device.LicenseTokenIssuedAtUtc = now;
        device.LastLicenseRefreshAtUtc = now;
        device.ValidUntilUtc = now.AddDays(LicenseLeaseDays);
        device.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshLicenseResult
        {
            Succeeded = true,
            DeviceId = device.Id,
            Status = device.Status,
            IsApproved = true,
            LicenseToken = device.LicenseToken,
            ValidUntilUtc = device.ValidUntilUtc,
            LastSeenAtUtc = device.LastSeenAtUtc,
            LastLicenseRefreshAtUtc = device.LastLicenseRefreshAtUtc,
            Message = "Licenca je uspešno osvežena na 5 dana."
        };
    }

    public async Task<LicenseStatusResult> GetStatusAsync(
        long appUserId,
        string hwidHash,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hwidHash))
        {
            return new LicenseStatusResult
            {
                ErrorMessage = "HWID hash je obavezan.",
                ErrorReasonCode = "hwid_hash_required"
            };
        }

        var device = await _dbContext.LicensedDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == appUserId && x.HwidHash == hwidHash,
                cancellationToken);

        if (device is null)
        {
            return new LicenseStatusResult
            {
                NotFound = true,
                ErrorMessage = "Uređaj nije registrovan.",
                ErrorReasonCode = "device_not_registered"
            };
        }

        return new LicenseStatusResult
        {
            Succeeded = true,
            DeviceId = device.Id,
            Status = device.Status,
            IsApproved = device.Status == DeviceLicenseStatus.Approved,
            HwidHash = device.HwidHash,
            ComputerName = device.ComputerName,
            ProgramVersion = device.ProgramVersion,
            LastSeenAtUtc = device.LastSeenAtUtc,
            LastLicenseRefreshAtUtc = device.LastLicenseRefreshAtUtc,
            ValidUntilUtc = device.ValidUntilUtc,
            Message = device.Status switch
            {
                DeviceLicenseStatus.Pending => "Uređaj čeka odobrenje.",
                DeviceLicenseStatus.Approved => "Uređaj je odobren.",
                DeviceLicenseStatus.Blocked => "Uređaj je blokiran.",
                _ => "Nepoznat status uređaja."
            }
        };
    }
}