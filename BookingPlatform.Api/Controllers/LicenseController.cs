using System.Security.Claims;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.License;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/License")]
public sealed class LicenseController : ControllerBase
{
    private readonly IDeviceLicenseService _deviceLicenseService;

    public LicenseController(IDeviceLicenseService deviceLicenseService)
    {
        _deviceLicenseService = deviceLicenseService;
    }

    [HttpPost("register-device")]
    [ProducesResponseType(typeof(RegisterDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterDeviceResponse>> RegisterDevice(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(BuildError("Korisnik nije autentifikovan.", "unauthenticated"));

        var result = await _deviceLicenseService.RegisterDeviceAsync(
            userId.Value,
            request.HwidHash,
            request.ComputerName,
            request.ProgramVersion,
            cancellationToken);

        if (!result.Succeeded)
            return BadRequest(BuildError(result.ErrorMessage, result.ErrorReasonCode));

        return Ok(new RegisterDeviceResponse
        {
            DeviceId = result.DeviceId,
            Status = result.Status.ToString(),
            Message = result.Message,
            HwidHash = result.HwidHash,
            ComputerName = result.ComputerName,
            ProgramVersion = result.ProgramVersion,
            LastSeenAtUtc = result.LastSeenAtUtc,
            ValidUntilUtc = result.ValidUntilUtc
        });
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshLicenseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RefreshLicenseResponse>> Refresh(
        [FromBody] RefreshLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(BuildError("Korisnik nije autentifikovan.", "unauthenticated"));

        var result = await _deviceLicenseService.RefreshAsync(
            userId.Value,
            request.HwidHash,
            request.ComputerName,
            request.ProgramVersion,
            cancellationToken);

        if (result.NotFound)
            return NotFound(BuildError(result.ErrorMessage, result.ErrorReasonCode));

        if (!result.Succeeded)
            return BadRequest(BuildError(result.ErrorMessage, result.ErrorReasonCode));

        return Ok(new RefreshLicenseResponse
        {
            DeviceId = result.DeviceId,
            Status = result.Status.ToString(),
            IsApproved = result.IsApproved,
            LicenseToken = result.LicenseToken,
            ValidUntilUtc = result.ValidUntilUtc,
            LastSeenAtUtc = result.LastSeenAtUtc,
            LastLicenseRefreshAtUtc = result.LastLicenseRefreshAtUtc,
            Message = result.Message
        });
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(LicenseStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LicenseStatusResponse>> Status(
        [FromQuery] string hwidHash,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized(BuildError("Korisnik nije autentifikovan.", "unauthenticated"));

        var result = await _deviceLicenseService.GetStatusAsync(
            userId.Value,
            hwidHash,
            cancellationToken);

        if (result.NotFound)
            return NotFound(BuildError(result.ErrorMessage, result.ErrorReasonCode));

        if (!result.Succeeded)
            return BadRequest(BuildError(result.ErrorMessage, result.ErrorReasonCode));

        return Ok(new LicenseStatusResponse
        {
            DeviceId = result.DeviceId,
            Status = result.Status.ToString(),
            IsApproved = result.IsApproved,
            HwidHash = result.HwidHash,
            ComputerName = result.ComputerName,
            ProgramVersion = result.ProgramVersion,
            LastSeenAtUtc = result.LastSeenAtUtc,
            LastLicenseRefreshAtUtc = result.LastLicenseRefreshAtUtc,
            ValidUntilUtc = result.ValidUntilUtc,
            Message = result.Message
        });
    }

    private long? TryGetCurrentUserId()
    {
        var rawUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub") ??
            User.FindFirstValue("userId");

        if (long.TryParse(rawUserId, out var userId))
            return userId;

        return null;
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