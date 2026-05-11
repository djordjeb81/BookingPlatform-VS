using System.Security.Claims;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class BusinessUsersController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public BusinessUsersController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessMembershipItemResponse>>> GetMembers(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .Join(
                _dbContext.AppUsers.AsNoTracking(),
                membership => membership.AppUserId,
                user => user.Id,
                (membership, user) => new BusinessMembershipItemResponse
                {
                    MembershipId = membership.Id,
                    AppUserId = user.Id,
                    BusinessId = membership.BusinessId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = membership.Role.ToString(),
                    IsActive = membership.IsActive
                })
            .OrderBy(x => x.Role)
            .ThenBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<BusinessMembershipItemResponse>> UpsertMembership(
        [FromBody] UpsertBusinessMembershipRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var email = request.UserEmail.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        if (!TryParseRole(request.Role, out var parsedRole))
            return BadRequest("Role mora biti Owner, Manager ili Staff.");

        var user = await _dbContext.AppUsers
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
            return BadRequest("Korisnik sa tim email-om ne postoji. Neka se prvo registruje.");

        if (!user.IsActive)
            return BadRequest("Korisnik nije aktivan.");

        var now = DateTime.UtcNow;

        var membership = await _dbContext.BusinessUserMemberships
            .FirstOrDefaultAsync(
                x => x.BusinessId == request.BusinessId && x.AppUserId == user.Id,
                cancellationToken);

        if (membership is null)
        {
            membership = new BusinessUserMembership
            {
                BusinessId = request.BusinessId,
                AppUserId = user.Id,
                Role = parsedRole,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.BusinessUserMemberships.Add(membership);
        }
        else
        {
            membership.Role = parsedRole;
            membership.IsActive = true;
            membership.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessMembershipItemResponse
        {
            MembershipId = membership.Id,
            AppUserId = user.Id,
            BusinessId = membership.BusinessId,
            FullName = user.FullName,
            Email = user.Email,
            Role = membership.Role.ToString(),
            IsActive = membership.IsActive
        });
    }

    [HttpPost("set-active")]
    public async Task<ActionResult<BusinessMembershipItemResponse>> SetMembershipActive(
        [FromBody] SetBusinessMembershipActiveRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var currentUserId = TryGetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized("Korisnik nije autentifikovan.");

        var membership = await _dbContext.BusinessUserMemberships
            .FirstOrDefaultAsync(
                x => x.Id == request.MembershipId && x.BusinessId == request.BusinessId,
                cancellationToken);

        if (membership is null)
            return NotFound("Membership ne postoji.");

        if (!request.IsActive)
        {
            if (membership.AppUserId == currentUserId.Value)
                return BadRequest("Ne možete deaktivirati sopstveni membership.");

            if (membership.Role == BusinessUserRole.Owner && membership.IsActive)
            {
                var activeOwnersCount = await _dbContext.BusinessUserMemberships
                    .AsNoTracking()
                    .CountAsync(x =>
                        x.BusinessId == request.BusinessId &&
                        x.IsActive &&
                        x.Role == BusinessUserRole.Owner,
                        cancellationToken);

                if (activeOwnersCount <= 1)
                    return BadRequest("Business mora imati bar jednog aktivnog owner-a.");
            }
        }

        membership.IsActive = request.IsActive;
        membership.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstAsync(x => x.Id == membership.AppUserId, cancellationToken);

        return Ok(new BusinessMembershipItemResponse
        {
            MembershipId = membership.Id,
            AppUserId = user.Id,
            BusinessId = membership.BusinessId,
            FullName = user.FullName,
            Email = user.Email,
            Role = membership.Role.ToString(),
            IsActive = membership.IsActive
        });
    }

    private long? TryGetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private async Task<ActionResult?> EnsureBusinessOwnerAccessAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije autentifikovan.");

        var membership = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.AppUserId == userId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (membership is null)
            return Forbid();

        if (membership.Role != BusinessUserRole.Owner)
            return Forbid();

        return null;
    }

    private static bool TryParseRole(string rawRole, out BusinessUserRole role)
    {
        if (Enum.TryParse<BusinessUserRole>(rawRole?.Trim(), true, out role))
        {
            return role == BusinessUserRole.Owner ||
                   role == BusinessUserRole.Manager ||
                   role == BusinessUserRole.Staff;
        }

        role = default;
        return false;
    }
}