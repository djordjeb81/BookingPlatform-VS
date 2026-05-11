using BookingPlatform.Contracts.Resources;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Contracts.Common;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class ResourcesController : ApiControllerBase
{
    public ResourcesController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<ResourceDto>>> GetAll(
        [FromQuery] long? businessId,
        CancellationToken cancellationToken)
    {
        IQueryable<Resource> query = DbContext.Resources
            .AsNoTracking()
            .Include(x => x.ResourceGroup);

        if (businessId.HasValue)
        {
            var accessResult = await EnsureBusinessReadAccessAsync(businessId.Value, cancellationToken);
            if (accessResult is not null)
                return accessResult;

            query = query.Where(x => x.BusinessId == businessId.Value);
        }
        else
        {
            var accessibleBusinessIds = await GetAccessibleBusinessIdsAsync(cancellationToken);
            query = query.Where(x => accessibleBusinessIds.Contains(x.BusinessId));
        }

        var items = await query
            .OrderBy(x => x.Name)
            .Select(x => new ResourceDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                Name = x.Name,
                ResourceType = (int)x.ResourceType,
                Capacity = x.Capacity,
                AllowParallelUsage = x.AllowParallelUsage,
                CreatesOccupancy = x.CreatesOccupancy,
                IsActive = x.IsActive,
                ResourceGroupId = x.ResourceGroupId,
                CustomerActionText = x.CustomerActionText,
                ResourceGroupName = x.ResourceGroup != null ? x.ResourceGroup.Name : null
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ResourceDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Resources
            .AsNoTracking()
            .Include(x => x.ResourceGroup)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Resurs ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<ResourceDto>> Create(
        [FromBody] CreateResourceRequest request,
        CancellationToken cancellationToken)
    {

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv resursa.");

        if (request.Capacity.HasValue && request.Capacity.Value <= 0)
            return BadRequest("Kapacitet mora biti veći od 0.");

        var businessExists = await DbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        var groupValidationResult = await ValidateResourceGroupAsync(
            request.BusinessId,
            request.ResourceGroupId,
            cancellationToken);

        if (groupValidationResult is not null)
            return groupValidationResult;

        var entity = new Resource
        {
            BusinessId = request.BusinessId,
            Name = request.Name.Trim(),
            ResourceType = (ResourceType)request.ResourceType,
            Capacity = request.Capacity,
            AllowParallelUsage = request.AllowParallelUsage,
            CreatesOccupancy = request.CreatesOccupancy,
            ResourceGroupId = request.ResourceGroupId,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CustomerActionText = NormalizeCustomerActionText(request.CustomerActionText),
            UpdatedAtUtc = DateTime.UtcNow
        };

        DbContext.Resources.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        entity = await DbContext.Resources
            .AsNoTracking()
            .Include(x => x.ResourceGroup)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ResourceDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Resources
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Resurs ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv resursa.");

        if (request.Capacity.HasValue && request.Capacity.Value <= 0)
            return BadRequest("Kapacitet mora biti veći od 0.");

        var groupValidationResult = await ValidateResourceGroupAsync(
            entity.BusinessId,
            request.ResourceGroupId,
            cancellationToken);

        if (groupValidationResult is not null)
            return groupValidationResult;

        entity.Name = request.Name.Trim();
        entity.ResourceType = (ResourceType)request.ResourceType;
        entity.Capacity = request.Capacity;
        entity.AllowParallelUsage = request.AllowParallelUsage;
        entity.CreatesOccupancy = request.CreatesOccupancy;
        entity.ResourceGroupId = request.ResourceGroupId;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.CustomerActionText = NormalizeCustomerActionText(request.CustomerActionText);

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.Resources
            .AsNoTracking()
            .Include(x => x.ResourceGroup)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<ResourceDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Resources
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Resurs ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!entity.IsActive)
            return BadRequest("Resurs je već neaktivan.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.Resources
            .AsNoTracking()
            .Include(x => x.ResourceGroup)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<ResourceDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Resources
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Resurs ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.IsActive)
            return BadRequest("Resurs je već aktivan.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.Resources
            .AsNoTracking()
            .Include(x => x.ResourceGroup)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToDto(dtoEntity));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Resources
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Resurs ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var hasDependencies =
            await DbContext.StaffResourceAssignments.AnyAsync(x => x.ResourceId == id, cancellationToken) ||
            await DbContext.ServiceResourceRequirements.AnyAsync(x => x.ResourceId == id, cancellationToken) ||
            await DbContext.ServiceResourceUsages.AnyAsync(x => x.ResourceId == id, cancellationToken) ||
            await DbContext.Appointments.AnyAsync(x => x.ResourceId == id, cancellationToken);

        if (hasDependencies)
        {
            return BadRequest("Resurs ne može da se obriše jer je povezan sa drugim podacima. Prvo uklonite te veze ili ga deaktivirajte.");
        }

        DbContext.Resources.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult?> ValidateResourceGroupAsync(
        long businessId,
        long? resourceGroupId,
        CancellationToken cancellationToken)
    {
        if (!resourceGroupId.HasValue)
            return null;

        var groupExists = await DbContext.ResourceGroups
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == resourceGroupId.Value &&
                x.BusinessId == businessId &&
                x.IsActive,
                cancellationToken);

        if (!groupExists)
            return BadRequest("Izabrana grupa resursa ne postoji ili ne pripada ovoj radnji.");

        return null;
    }

    private static string? NormalizeCustomerActionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length > 200
            ? trimmed[..200]
            : trimmed;
    }

    private static ResourceDto ToDto(Resource entity)
    {
        return new ResourceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            ResourceType = (int)entity.ResourceType,
            Capacity = entity.Capacity,
            AllowParallelUsage = entity.AllowParallelUsage,
            CreatesOccupancy = entity.CreatesOccupancy,
            IsActive = entity.IsActive,
            ResourceGroupId = entity.ResourceGroupId,
            CustomerActionText = entity.CustomerActionText,
            ResourceGroupName = entity.ResourceGroup?.Name
        };
    }
}