using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantAddonsController : ApiControllerBase
{
    public RestaurantAddonsController(BookingDbContext dbContext)
        : base(dbContext)
    {
    }

    [HttpGet("groups")]
    public async Task<ActionResult<List<RestaurantAddonGroupDto>>> GetGroups(
        [FromQuery] long businessId,
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantAddonGroups
            .AsNoTracking()
            .Include(x => x.Addons)
            .Where(x => x.BusinessId == businessId);

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var groups = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(groups.Select(ToGroupDto).ToList());
    }

    [HttpPost("groups")]
    public async Task<ActionResult<RestaurantAddonGroupDto>> CreateGroup(
        [FromBody] CreateRestaurantAddonGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeText(request.Name, 160);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv grupe dodataka.");

        var businessExists = await DbContext.Businesses
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.BusinessId && x.IsActive, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji ili nije aktivna.");

        var now = DateTime.UtcNow;

        var entity = new RestaurantAddonGroup
        {
            BusinessId = request.BusinessId,
            Name = name,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantAddonGroups.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToGroupDto(entity));
    }

    [HttpPut("groups/{groupId:long}")]
    public async Task<ActionResult<RestaurantAddonGroupDto>> UpdateGroup(
        [FromRoute] long groupId,
        [FromBody] UpdateRestaurantAddonGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RestaurantAddonGroups
            .Include(x => x.Addons)
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);

        if (entity is null)
            return NotFound("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeText(request.Name, 160);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv grupe dodataka.");

        entity.Name = name;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToGroupDto(entity));
    }

    [HttpDelete("groups/{groupId:long}")]
    public async Task<ActionResult> DeleteGroup(
        [FromRoute] long groupId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RestaurantAddonGroups
            .Include(x => x.Addons)
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);

        if (entity is null)
            return NotFound("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var now = DateTime.UtcNow;

        entity.IsActive = false;
        entity.UpdatedAtUtc = now;

        foreach (var addon in entity.Addons)
        {
            addon.IsActive = false;
            addon.IsAvailable = false;
            addon.UpdatedAtUtc = now;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("groups/{groupId:long}/addons")]
    public async Task<ActionResult<RestaurantAddonDto>> CreateAddon(
        [FromRoute] long groupId,
        [FromBody] CreateRestaurantAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var group = await DbContext.RestaurantAddonGroups
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);

        if (group is null)
            return NotFound("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(group.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!group.IsActive)
            return BadRequest("Ne možete dodavati dodatke u neaktivnu grupu.");

        var name = NormalizeText(request.Name, 160);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv dodatka.");

        var now = DateTime.UtcNow;

        var entity = new RestaurantAddon
        {
            BusinessId = group.BusinessId,
            AddonGroupId = group.Id,
            Name = name,
            PriceDelta = request.PriceDelta,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            IsAvailable = request.IsAvailable,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantAddons.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToAddonDto(entity));
    }

    [HttpPut("addons/{addonId:long}")]
    public async Task<ActionResult<RestaurantAddonDto>> UpdateAddon(
        [FromRoute] long addonId,
        [FromBody] UpdateRestaurantAddonRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RestaurantAddons
            .FirstOrDefaultAsync(x => x.Id == addonId, cancellationToken);

        if (entity is null)
            return NotFound("Dodatak ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeText(request.Name, 160);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv dodatka.");

        entity.Name = name;
        entity.PriceDelta = request.PriceDelta;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.IsAvailable = request.IsAvailable;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToAddonDto(entity));
    }

    [HttpDelete("addons/{addonId:long}")]
    public async Task<ActionResult> DeleteAddon(
        [FromRoute] long addonId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.RestaurantAddons
            .FirstOrDefaultAsync(x => x.Id == addonId, cancellationToken);

        if (entity is null)
            return NotFound("Dodatak ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        entity.IsActive = false;
        entity.IsAvailable = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static RestaurantAddonGroupDto ToGroupDto(RestaurantAddonGroup entity)
    {
        return new RestaurantAddonGroupDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            Addons = entity.Addons
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(ToAddonDto)
                .ToList()
        };
    }

    private static RestaurantAddonDto ToAddonDto(RestaurantAddon entity)
    {
        return new RestaurantAddonDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            AddonGroupId = entity.AddonGroupId,
            Name = entity.Name,
            PriceDelta = entity.PriceDelta,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            IsAvailable = entity.IsAvailable
        };
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }
}