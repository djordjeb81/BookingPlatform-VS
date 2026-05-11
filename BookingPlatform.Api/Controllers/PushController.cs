using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Push;
using BookingPlatform.Domain.Push;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[Route("api/Push")]
public sealed class PushController : ApiControllerBase
{
    public PushController(BookingDbContext dbContext)
        : base(dbContext)
    {
    }

    [HttpPost("register-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterToken(
        [FromBody] RegisterPushTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var token = request.Token?.Trim();

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token nije poslat.");

        if (token.Length > 500)
            return BadRequest("Token je predugačak.");

        var platform = string.IsNullOrWhiteSpace(request.Platform)
            ? "Android"
            : request.Platform.Trim();

        var deviceName = string.IsNullOrWhiteSpace(request.DeviceName)
            ? null
            : request.DeviceName.Trim();

        var now = DateTime.UtcNow;

        var existing = await DbContext.UserPushTokens
            .FirstOrDefaultAsync(x => x.Token == token, cancellationToken);

        if (existing is null)
        {
            existing = new UserPushToken
            {
                AppUserId = userId.Value,
                Token = token,
                Platform = platform,
                DeviceName = deviceName,
                IsActive = true,
                LastSeenAtUtc = now,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            DbContext.UserPushTokens.Add(existing);
        }
        else
        {
            existing.AppUserId = userId.Value;
            existing.Platform = platform;
            existing.DeviceName = deviceName;
            existing.IsActive = true;
            existing.LastSeenAtUtc = now;
            existing.UpdatedAtUtc = now;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("unregister-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnregisterToken(
    [FromBody] UnregisterPushTokenRequest request,
    CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var token = request.Token?.Trim();

        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token nije poslat.");

        var existing = await DbContext.UserPushTokens
            .FirstOrDefaultAsync(
                x => x.Token == token &&
                     x.AppUserId == userId.Value,
                cancellationToken);

        if (existing is null)
            return NoContent();

        var now = DateTime.UtcNow;

        existing.IsActive = false;
        existing.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}