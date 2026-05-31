using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class RestaurantDeliveryZonesController : ApiControllerBase
{
    public RestaurantDeliveryZonesController(BookingDbContext dbContext)
        : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantDeliveryZoneDto>>> GetByBusiness(
        [FromQuery] long businessId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantDeliveryZones
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var items = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RestaurantDeliveryZoneDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantDeliveryZones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zona dostave ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantDeliveryZoneDto>> Create(
        [FromBody] CreateRestaurantDeliveryZoneRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeText(request.Name, 160);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Naziv zone dostave je obavezan.");

        if (request.DeliveryFeeAmount < 0)
            return BadRequest("Cena dostave ne može biti manja od 0.");

        if (request.MinimumOrderAmount < 0)
            return BadRequest("Minimalna porudžbina ne može biti manja od 0.");

        var alreadyExists = await DbContext.RestaurantDeliveryZones
            .AnyAsync(x =>
                x.BusinessId == request.BusinessId &&
                x.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (alreadyExists)
            return BadRequest("Zona dostave sa ovim nazivom već postoji.");

        var now = DateTime.UtcNow;

        var entity = new RestaurantDeliveryZone
        {
            BusinessId = request.BusinessId,
            Name = name,
            Description = NormalizeText(request.Description, 500),
            DeliveryFeeAmount = request.DeliveryFeeAmount,
            MinimumOrderAmount = request.MinimumOrderAmount,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantDeliveryZones.Add(entity);

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<RestaurantDeliveryZoneDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateRestaurantDeliveryZoneRequest request,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantDeliveryZones
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zona dostave ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeText(request.Name, 160);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Naziv zone dostave je obavezan.");

        if (request.DeliveryFeeAmount < 0)
            return BadRequest("Cena dostave ne može biti manja od 0.");

        if (request.MinimumOrderAmount < 0)
            return BadRequest("Minimalna porudžbina ne može biti manja od 0.");

        var duplicateExists = await DbContext.RestaurantDeliveryZones
            .AnyAsync(x =>
                x.Id != entity.Id &&
                x.BusinessId == entity.BusinessId &&
                x.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (duplicateExists)
            return BadRequest("Zona dostave sa ovim nazivom već postoji.");

        entity.Name = name;
        entity.Description = NormalizeText(request.Description, 500);
        entity.DeliveryFeeAmount = request.DeliveryFeeAmount;
        entity.MinimumOrderAmount = request.MinimumOrderAmount;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return BadRequest("Id je obavezan.");

        var entity = await DbContext.RestaurantDeliveryZones
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zona dostave ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var hasOrders = await DbContext.RestaurantOrders
            .AnyAsync(x => x.DeliveryZoneId == entity.Id, cancellationToken);

        if (hasOrders)
        {
            entity.IsActive = false;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await DbContext.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        DbContext.RestaurantDeliveryZones.Remove(entity);

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static RestaurantDeliveryZoneDto ToDto(RestaurantDeliveryZone entity)
    {
        return new RestaurantDeliveryZoneDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            DeliveryFeeAmount = entity.DeliveryFeeAmount,
            MinimumOrderAmount = entity.MinimumOrderAmount,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder
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