using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Resources;
using BookingPlatform.Domain.Resources;
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
public sealed class ResourceGroupsController : ApiControllerBase
{
    public ResourceGroupsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<ResourceGroupDto>>> GetAll(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("Radnja nije ispravno izabrana.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.ResourceGroups
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .Select(x => new ResourceGroupDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                Name = x.Name,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ResourceGroupDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ResourceGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Grupa resursa ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<ResourceGroupDto>> Create(
        [FromBody] CreateResourceGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("Radnja nije ispravno izabrana.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv grupe resursa.");

        var businessExists = await DbContext.Businesses
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        var exists = await DbContext.ResourceGroups
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == request.BusinessId &&
                x.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (exists)
            return BadRequest("Grupa resursa sa tim nazivom već postoji.");

        var entity = new ResourceGroup
        {
            BusinessId = request.BusinessId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        DbContext.ResourceGroups.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ResourceGroupDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateResourceGroupRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ResourceGroups
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Grupa resursa ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv grupe resursa.");

        var exists = await DbContext.ResourceGroups
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id != id &&
                x.BusinessId == entity.BusinessId &&
                x.Name.ToLower() == name.ToLower(),
                cancellationToken);

        if (exists)
            return BadRequest("Grupa resursa sa tim nazivom već postoji.");

        entity.Name = name;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<ResourceGroupDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ResourceGroups
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Grupa resursa ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!entity.IsActive)
            return BadRequest("Grupa resursa je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<ResourceGroupDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ResourceGroups
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Grupa resursa ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.IsActive)
            return BadRequest("Grupa resursa je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    private static ResourceGroupDto ToDto(ResourceGroup entity)
    {
        return new ResourceGroupDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            IsActive = entity.IsActive
        };
    }
}