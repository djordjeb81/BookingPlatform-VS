using System.Security.Claims;
using BookingPlatform.Contracts.Resources;
using BookingPlatform.Domain.Auth;
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
public sealed class ServiceResourceRequirementsController : ApiControllerBase
{


    public ServiceResourceRequirementsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceResourceRequirementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ServiceResourceRequirementDto>>> GetAll(
        [FromQuery] long serviceId,
        CancellationToken cancellationToken)
    {
        var service = await DbContext.Services
            .AsNoTracking()
            .Where(x => x.Id == serviceId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
            return NotFound("Usluga ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.ServiceResourceRequirements
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .Join(
                DbContext.Resources.AsNoTracking(),
                requirement => requirement.ResourceId,
                resource => resource.Id,
                (requirement, resource) => new ServiceResourceRequirementDto
                {
                    Id = requirement.Id,
                    ServiceId = requirement.ServiceId,
                    ResourceId = requirement.ResourceId,
                    ResourceName = resource.Name,
                    ResourceType = (int)resource.ResourceType,
                    IsRequired = requirement.IsRequired
                })
            .OrderBy(x => x.ResourceName)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ServiceResourceRequirementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceResourceRequirementDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var item = await DbContext.ServiceResourceRequirements
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Join(
                DbContext.Resources.AsNoTracking(),
                requirement => requirement.ResourceId,
                resource => resource.Id,
                (requirement, resource) => new
                {
                    Dto = new ServiceResourceRequirementDto
                    {
                        Id = requirement.Id,
                        ServiceId = requirement.ServiceId,
                        ResourceId = requirement.ResourceId,
                        ResourceName = resource.Name,
                        ResourceType = (int)resource.ResourceType,
                        IsRequired = requirement.IsRequired
                    },
                    resource.BusinessId
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Veza usluge i resursa ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(item.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(item.Dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServiceResourceRequirementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceResourceRequirementDto>> Create(
        [FromBody] CreateServiceResourceRequirementRequest request,
        CancellationToken cancellationToken)
    {
        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var resource = await DbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ResourceId, cancellationToken);

        if (resource is null)
            return BadRequest("Izabrani resurs ne postoji.");

        if (resource.BusinessId != service.BusinessId)
            return BadRequest("Resurs i usluga moraju pripadati istoj radnji.");

        var exists = await DbContext.ServiceResourceRequirements
            .AnyAsync(
                x => x.ServiceId == request.ServiceId && x.ResourceId == request.ResourceId,
                cancellationToken);

        if (exists)
            return BadRequest("Ovaj resurs je već povezan sa uslugom.");

        var entity = new ServiceResourceRequirement
        {
            ServiceId = request.ServiceId,
            ResourceId = request.ResourceId,
            IsRequired = request.IsRequired,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        DbContext.ServiceResourceRequirements.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceResourceRequirementDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            ResourceId = entity.ResourceId,
            ResourceName = resource.Name,
            ResourceType = (int)resource.ResourceType,
            IsRequired = entity.IsRequired
        });
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ServiceResourceRequirementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceResourceRequirementDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateServiceResourceRequirementRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceResourceRequirements
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Veza usluge i resursa ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        entity.IsRequired = request.IsRequired;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var resource = await DbContext.Resources
            .AsNoTracking()
            .FirstAsync(x => x.Id == entity.ResourceId, cancellationToken);

        return Ok(new ServiceResourceRequirementDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            ResourceId = entity.ResourceId,
            ResourceName = resource.Name,
            ResourceType = (int)resource.ResourceType,
            IsRequired = entity.IsRequired
        });
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceResourceRequirements
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Veza usluge i resursa ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.ServiceResourceRequirements.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

}