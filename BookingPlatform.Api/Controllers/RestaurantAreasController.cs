using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Resources;
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
public sealed class RestaurantAreasController : ApiControllerBase
{
    public RestaurantAreasController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantAreaDto>>> GetAll(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.RestaurantAreas
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => new RestaurantAreaDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                Name = x.Name,
                Capacity = x.Capacity,
                CanvasWidth = x.CanvasWidth,
                CanvasHeight = x.CanvasHeight,
                BoundaryPointsJson = x.BoundaryPointsJson,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive,
                IsReservableAsWhole = x.IsReservableAsWhole,
                WholeAreaResourceId = x.WholeAreaResourceId
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RestaurantAreaDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantAreaDto>> Create(
        [FromBody] CreateRestaurantAreaRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv sale.");

        var validationResult = await ValidateAreaRequestAsync(
            request.BusinessId,
            request.Capacity,
            request.CanvasWidth,
            request.CanvasHeight,
            request.WholeAreaResourceId,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        var normalizedName = request.Name.Trim();

        var nameExists = await DbContext.RestaurantAreas
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == request.BusinessId &&
                x.Name == normalizedName,
                cancellationToken);

        if (nameExists)
            return BadRequest("Sala sa ovim nazivom već postoji.");

        var now = DateTime.UtcNow;

        var entity = new RestaurantArea
        {
            BusinessId = request.BusinessId,
            Name = normalizedName,
            Capacity = request.Capacity,
            CanvasWidth = request.CanvasWidth,
            CanvasHeight = request.CanvasHeight,
            BoundaryPointsJson = NormalizeJsonText(request.BoundaryPointsJson),
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            IsReservableAsWhole = request.IsReservableAsWhole,
            WholeAreaResourceId = request.WholeAreaResourceId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantAreas.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<RestaurantAreaDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateRestaurantAreaRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreas
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv sale.");

        var validationResult = await ValidateAreaRequestAsync(
            entity.BusinessId,
            request.Capacity,
            request.CanvasWidth,
            request.CanvasHeight,
            request.WholeAreaResourceId,
            cancellationToken);

        if (validationResult is not null)
            return validationResult;

        var normalizedName = request.Name.Trim();

        var nameExists = await DbContext.RestaurantAreas
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == entity.BusinessId &&
                x.Id != entity.Id &&
                x.Name == normalizedName,
                cancellationToken);

        if (nameExists)
            return BadRequest("Sala sa ovim nazivom već postoji.");

        entity.Name = normalizedName;
        entity.Capacity = request.Capacity;
        entity.CanvasWidth = request.CanvasWidth;
        entity.CanvasHeight = request.CanvasHeight;
        entity.BoundaryPointsJson = NormalizeJsonText(request.BoundaryPointsJson);
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.IsReservableAsWhole = request.IsReservableAsWhole;
        entity.WholeAreaResourceId = request.WholeAreaResourceId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<RestaurantAreaDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreas
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.IsActive)
            return BadRequest("Sala je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<RestaurantAreaDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreas
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!entity.IsActive)
            return BadRequest("Sala je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantAreas
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var hasResources = await DbContext.Resources
            .AsNoTracking()
            .AnyAsync(x => x.RestaurantAreaId == id, cancellationToken);

        if (hasResources)
            return BadRequest("Sala ne može da se obriše jer postoje resursi povezani sa njom. Deaktivirajte je ili prvo premestite resurse.");

        var hasLayoutElements = await DbContext.RestaurantLayoutElements
            .AsNoTracking()
            .AnyAsync(x => x.RestaurantAreaId == id, cancellationToken);

        if (hasLayoutElements)
            return BadRequest("Sala ne može da se obriše jer ima elemente rasporeda. Prvo obrišite elemente ili deaktivirajte salu.");

        var hasTableReservations = await DbContext.RestaurantTableReservations
            .AsNoTracking()
            .AnyAsync(x => x.RestaurantAreaId == id, cancellationToken);

        if (hasTableReservations)
            return BadRequest("Sala ne može da se obriše jer ima rezervacije stolova. Deaktivirajte salu ako se više ne koristi.");

        var hasAreaReservations = await DbContext.RestaurantAreaReservations
            .AsNoTracking()
            .AnyAsync(x => x.RestaurantAreaId == id, cancellationToken);

        if (hasAreaReservations)
            return BadRequest("Sala ne može da se obriše jer ima rezervacije cele sale. Deaktivirajte salu ako se više ne koristi.");

        DbContext.RestaurantAreas.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult?> ValidateAreaRequestAsync(
        long businessId,
        int? capacity,
        int canvasWidth,
        int canvasHeight,
        long? wholeAreaResourceId,
        CancellationToken cancellationToken)
    {
        if (capacity.HasValue && capacity.Value <= 0)
            return BadRequest("Kapacitet sale mora biti veći od 0.");

        if (canvasWidth <= 0)
            return BadRequest("Širina platna mora biti veća od 0.");

        if (canvasHeight <= 0)
            return BadRequest("Visina platna mora biti veća od 0.");

        if (!wholeAreaResourceId.HasValue)
            return null;

        var resourceExists = await DbContext.Resources
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == wholeAreaResourceId.Value &&
                x.BusinessId == businessId &&
                x.IsActive &&
                x.ResourceType == ResourceType.EventHall,
                cancellationToken);

        if (!resourceExists)
            return BadRequest("Izabrani resurs za celu salu ne postoji ili nije tipa sala.");

        return null;
    }

    private static string? NormalizeJsonText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static RestaurantAreaDto ToDto(RestaurantArea entity)
    {
        return new RestaurantAreaDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Capacity = entity.Capacity,
            CanvasWidth = entity.CanvasWidth,
            CanvasHeight = entity.CanvasHeight,
            BoundaryPointsJson = entity.BoundaryPointsJson,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            IsReservableAsWhole = entity.IsReservableAsWhole,
            WholeAreaResourceId = entity.WholeAreaResourceId
        };
    }
}