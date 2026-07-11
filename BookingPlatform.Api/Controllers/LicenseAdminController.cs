using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.License;
using BookingPlatform.Domain.Licensing;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/License/admin")]
public sealed class LicenseAdminController : ControllerBase
{
    private const string AdminAccessTokenHeaderName = "X-Admin-Access-Token";

    private readonly BookingDbContext _db;
    private readonly AdminAccessService _adminAccessService;

    public LicenseAdminController(
        BookingDbContext db,
        AdminAccessService adminAccessService)
    {
        _db = db;
        _adminAccessService = adminAccessService;
    }

    [HttpGet("devices")]
    [ProducesResponseType(typeof(List<LicenseAdminDeviceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<LicenseAdminDeviceDto>>> GetDevices(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!await HasAdminAccessAsync(cancellationToken))
            return Forbid();

        var query =
            from device in _db.LicensedDevices.AsNoTracking()
            join user in _db.AppUsers.AsNoTracking()
                on device.AppUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            select new
            {
                Device = device,
                UserEmail = user != null ? user.Email : ""
            };

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (Enum.TryParse<DeviceLicenseStatus>(
                    status,
                    ignoreCase: true,
                    out var parsedStatus))
            {
                query = query.Where(x => x.Device.Status == parsedStatus);
            }
        }

        var result = await query
            .OrderByDescending(x => x.Device.Status == DeviceLicenseStatus.Pending)
            .ThenByDescending(x => x.Device.UpdatedAtUtc)
            .ThenByDescending(x => x.Device.Id)
            .Select(x => new LicenseAdminDeviceDto
            {
                Id = x.Device.Id,
                AppUserId = x.Device.AppUserId,
                UserEmail = x.UserEmail ?? "",
                HwidHash = x.Device.HwidHash,
                ComputerName = x.Device.ComputerName,
                ProgramVersion = x.Device.ProgramVersion,
                Status = x.Device.Status.ToString(),
                StatusValue = (int)x.Device.Status,
                LicenseToken = x.Device.LicenseToken,
                ValidUntilUtc = x.Device.ValidUntilUtc,
                LastSeenAtUtc = x.Device.LastSeenAtUtc,
                LastLicenseRefreshAtUtc = x.Device.LastLicenseRefreshAtUtc,
                CreatedAtUtc = x.Device.CreatedAtUtc,
                UpdatedAtUtc = x.Device.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost("devices/{deviceId:long}/approve")]
    [ProducesResponseType(typeof(LicenseAdminDeviceActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseAdminDeviceActionResponse>> ApproveDevice(
        long deviceId,
        [FromBody] ApproveLicenseDeviceRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await HasAdminAccessAsync(cancellationToken))
            return Forbid();

        var device = await _db.LicensedDevices
            .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

        if (device is null)
            return NotFound(BuildError("Uređaj nije pronađen.", "device_not_found"));

        var nowUtc = DateTime.UtcNow;

        device.Status = DeviceLicenseStatus.Approved;
        device.ValidUntilUtc = request?.ValidUntilUtc ?? GetEndOfCurrentYearUtc();
        device.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new LicenseAdminDeviceActionResponse
        {
            Succeeded = true,
            DeviceId = device.Id,
            Status = device.Status.ToString(),
            ValidUntilUtc = device.ValidUntilUtc,
            Message = "Uređaj je odobren."
        });
    }

    [HttpPost("devices/{deviceId:long}/block")]
    [ProducesResponseType(typeof(LicenseAdminDeviceActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseAdminDeviceActionResponse>> BlockDevice(
        long deviceId,
        CancellationToken cancellationToken)
    {
        if (!await HasAdminAccessAsync(cancellationToken))
            return Forbid();

        var device = await _db.LicensedDevices
            .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

        if (device is null)
            return NotFound(BuildError("Uređaj nije pronađen.", "device_not_found"));

        var nowUtc = DateTime.UtcNow;

        device.Status = DeviceLicenseStatus.Blocked;
        device.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new LicenseAdminDeviceActionResponse
        {
            Succeeded = true,
            DeviceId = device.Id,
            Status = device.Status.ToString(),
            ValidUntilUtc = device.ValidUntilUtc,
            Message = "Uređaj je blokiran."
        });
    }

    [HttpPost("devices/{deviceId:long}/extend")]
    [ProducesResponseType(typeof(LicenseAdminDeviceActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseAdminDeviceActionResponse>> ExtendDevice(
        long deviceId,
        [FromBody] ExtendLicenseDeviceRequest request,
        CancellationToken cancellationToken)
    {
        if (!await HasAdminAccessAsync(cancellationToken))
            return Forbid();

        if (request.ValidUntilUtc <= DateTime.UtcNow)
            return BadRequest(BuildError("Datum važenja mora biti u budućnosti.", "invalid_valid_until"));

        var device = await _db.LicensedDevices
            .FirstOrDefaultAsync(x => x.Id == deviceId, cancellationToken);

        if (device is null)
            return NotFound(BuildError("Uređaj nije pronađen.", "device_not_found"));

        var nowUtc = DateTime.UtcNow;

        device.Status = DeviceLicenseStatus.Approved;
        device.ValidUntilUtc = request.ValidUntilUtc;
        device.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new LicenseAdminDeviceActionResponse
        {
            Succeeded = true,
            DeviceId = device.Id,
            Status = device.Status.ToString(),
            ValidUntilUtc = device.ValidUntilUtc,
            Message = "Licenca je produžena."
        });
    }

    private async Task<bool> HasAdminAccessAsync(CancellationToken cancellationToken)
    {
        var token = Request.Headers[AdminAccessTokenHeaderName].FirstOrDefault();

        return await _adminAccessService.IsTokenValidAsync(
            token,
            cancellationToken);
    }

    private static DateTime GetEndOfCurrentYearUtc()
    {
        var nowUtc = DateTime.UtcNow;

        return new DateTime(
            nowUtc.Year,
            12,
            31,
            23,
            59,
            59,
            DateTimeKind.Utc);
    }

    private static ApiErrorResponse BuildError(string message, string reasonCode)
    {
        return new ApiErrorResponse
        {
            Message = message,
            ReasonCode = reasonCode,
            ReasonCodes = string.IsNullOrWhiteSpace(reasonCode)
                ? new List<string>()
                : new List<string> { reasonCode }
        };
    }
}