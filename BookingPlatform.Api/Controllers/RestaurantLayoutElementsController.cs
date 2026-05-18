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
public sealed class RestaurantLayoutElementsController : ApiControllerBase
{
    public RestaurantLayoutElementsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantLayoutElementDto>>> GetAll(
        [FromQuery] long restaurantAreaId,
        CancellationToken cancellationToken)
    {
        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantAreaId, cancellationToken);

        if (area is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(area.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.RestaurantLayoutElements
            .AsNoTracking()
            .Where(x => x.RestaurantAreaId == restaurantAreaId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new RestaurantLayoutElementDto
            {
                Id = x.Id,
                RestaurantAreaId = x.RestaurantAreaId,
                ElementType = (int)x.ElementType,
                Label = x.Label,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                Height = x.Height,
                RotationDeg = x.RotationDeg,
                ShapeType = (int)x.ShapeType,
                PointsJson = x.PointsJson,
                IsObstacle = x.IsObstacle,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RestaurantLayoutElementDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantLayoutElements
            .AsNoTracking()
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Element rasporeda ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.RestaurantArea.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantLayoutElementDto>> Create(
        [FromBody] CreateRestaurantLayoutElementRequest request,
        CancellationToken cancellationToken)
    {
        var area = await DbContext.RestaurantAreas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.RestaurantAreaId, cancellationToken);

        if (area is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(area.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = ValidateLayoutElement(
            request.ElementType,
            request.Label,
            request.Width,
            request.Height,
            request.RotationDeg,
            request.ShapeType,
            request.PointsJson);

        if (validationResult is not null)
            return validationResult;

        var now = DateTime.UtcNow;

        var entity = new RestaurantLayoutElement
        {
            RestaurantAreaId = request.RestaurantAreaId,
            ElementType = (RestaurantLayoutElementType)request.ElementType,
            Label = NormalizeLabel(request.Label),
            X = request.X,
            Y = request.Y,
            Width = request.Width,
            Height = request.Height,
            RotationDeg = request.RotationDeg,
            ShapeType = (LayoutShapeType)request.ShapeType,
            PointsJson = NormalizeJsonText(request.PointsJson),
            IsObstacle = request.IsObstacle,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantLayoutElements.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<RestaurantLayoutElementDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateRestaurantLayoutElementRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantLayoutElements
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Element rasporeda ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.RestaurantArea.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = ValidateLayoutElement(
            request.ElementType,
            request.Label,
            request.Width,
            request.Height,
            request.RotationDeg,
            request.ShapeType,
            request.PointsJson);

        if (validationResult is not null)
            return validationResult;

        entity.ElementType = (RestaurantLayoutElementType)request.ElementType;
        entity.Label = NormalizeLabel(request.Label);
        entity.X = request.X;
        entity.Y = request.Y;
        entity.Width = request.Width;
        entity.Height = request.Height;
        entity.RotationDeg = request.RotationDeg;
        entity.ShapeType = (LayoutShapeType)request.ShapeType;
        entity.PointsJson = NormalizeJsonText(request.PointsJson);
        entity.IsObstacle = request.IsObstacle;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("area/{restaurantAreaId:long}/replace")]
    public async Task<ActionResult<List<RestaurantLayoutElementDto>>> ReplaceForArea(
        [FromRoute] long restaurantAreaId,
        [FromBody] ReplaceRestaurantLayoutElementsRequest request,
        CancellationToken cancellationToken)
    {
        var area = await DbContext.RestaurantAreas
            .FirstOrDefaultAsync(x => x.Id == restaurantAreaId, cancellationToken);

        if (area is null)
            return NotFound("Sala ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(area.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        foreach (var item in request.Elements)
        {
            var validationResult = ValidateLayoutElement(
                item.ElementType,
                item.Label,
                item.Width,
                item.Height,
                item.RotationDeg,
                item.ShapeType,
                item.PointsJson);

            if (validationResult is not null)
                return validationResult;
        }

        var oldItems = await DbContext.RestaurantLayoutElements
            .Where(x => x.RestaurantAreaId == restaurantAreaId)
            .ToListAsync(cancellationToken);

        DbContext.RestaurantLayoutElements.RemoveRange(oldItems);

        var now = DateTime.UtcNow;

        var newItems = request.Elements
            .Select(item => new RestaurantLayoutElement
            {
                RestaurantAreaId = restaurantAreaId,
                ElementType = (RestaurantLayoutElementType)item.ElementType,
                Label = NormalizeLabel(item.Label),
                X = item.X,
                Y = item.Y,
                Width = item.Width,
                Height = item.Height,
                RotationDeg = item.RotationDeg,
                ShapeType = (LayoutShapeType)item.ShapeType,
                PointsJson = NormalizeJsonText(item.PointsJson),
                IsObstacle = item.IsObstacle,
                DisplayOrder = item.DisplayOrder,
                IsActive = item.IsActive,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToList();

        DbContext.RestaurantLayoutElements.AddRange(newItems);

        await DbContext.SaveChangesAsync(cancellationToken);

        var result = await DbContext.RestaurantLayoutElements
            .AsNoTracking()
            .Where(x => x.RestaurantAreaId == restaurantAreaId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => new RestaurantLayoutElementDto
            {
                Id = x.Id,
                RestaurantAreaId = x.RestaurantAreaId,
                ElementType = (int)x.ElementType,
                Label = x.Label,
                X = x.X,
                Y = x.Y,
                Width = x.Width,
                Height = x.Height,
                RotationDeg = x.RotationDeg,
                ShapeType = (int)x.ShapeType,
                PointsJson = x.PointsJson,
                IsObstacle = x.IsObstacle,
                DisplayOrder = x.DisplayOrder,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<RestaurantLayoutElementDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantLayoutElements
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Element rasporeda ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.RestaurantArea.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.IsActive)
            return BadRequest("Element rasporeda je već aktivan.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<RestaurantLayoutElementDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantLayoutElements
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Element rasporeda ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.RestaurantArea.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!entity.IsActive)
            return BadRequest("Element rasporeda je već neaktivan.");

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
        var entity = await DbContext.RestaurantLayoutElements
            .Include(x => x.RestaurantArea)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Element rasporeda ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.RestaurantArea.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.RestaurantLayoutElements.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private ActionResult? ValidateLayoutElement(
        int elementType,
        string? label,
        decimal width,
        decimal height,
        int rotationDeg,
        int shapeType,
        string? pointsJson)
    {
        if (!Enum.IsDefined(typeof(RestaurantLayoutElementType), elementType))
            return BadRequest("Nepoznat tip elementa rasporeda.");

        if (!Enum.IsDefined(typeof(LayoutShapeType), shapeType))
            return BadRequest("Nepoznat oblik elementa rasporeda.");

        if (!string.IsNullOrWhiteSpace(label) && label.Trim().Length > 150)
            return BadRequest("Naziv elementa može imati najviše 150 karaktera.");

        if (rotationDeg < -360 || rotationDeg > 360)
            return BadRequest("Rotacija mora biti između -360 i 360 stepeni.");

        var parsedShapeType = (LayoutShapeType)shapeType;

        if (parsedShapeType == LayoutShapeType.Line)
        {
            if (string.IsNullOrWhiteSpace(pointsJson))
                return BadRequest("Za liniju je potrebno poslati tačke.");
        }
        else if (parsedShapeType == LayoutShapeType.Polygon)
        {
            if (string.IsNullOrWhiteSpace(pointsJson))
                return BadRequest("Za nepravilan oblik je potrebno poslati tačke.");
        }
        else
        {
            if (width <= 0)
                return BadRequest("Širina elementa mora biti veća od 0.");

            if (height <= 0)
                return BadRequest("Visina elementa mora biti veća od 0.");
        }

        return null;
    }

    private static string? NormalizeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static string? NormalizeJsonText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private static RestaurantLayoutElementDto ToDto(RestaurantLayoutElement entity)
    {
        return new RestaurantLayoutElementDto
        {
            Id = entity.Id,
            RestaurantAreaId = entity.RestaurantAreaId,
            ElementType = (int)entity.ElementType,
            Label = entity.Label,
            X = entity.X,
            Y = entity.Y,
            Width = entity.Width,
            Height = entity.Height,
            RotationDeg = entity.RotationDeg,
            ShapeType = (int)entity.ShapeType,
            PointsJson = entity.PointsJson,
            IsObstacle = entity.IsObstacle,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive
        };
    }
}