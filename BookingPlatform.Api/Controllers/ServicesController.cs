using System.Security.Claims;
using BookingPlatform.Contracts.Services;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Services;
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
public sealed class ServicesController : ApiControllerBase
{
    

    public ServicesController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAll(
        [FromQuery] long? businessId,
        CancellationToken cancellationToken)
    {
        IQueryable<Service> query = DbContext.Services.AsNoTracking();

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
            .Select(x => new ServiceDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                Name = x.Name,
                Description = x.Description,
                BasePrice = x.BasePrice,
                EstimatedDurationMin = x.EstimatedDurationMin,
                BookingStrategyType = (int)x.BookingStrategyType,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(new ServiceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            BasePrice = entity.BasePrice,
            EstimatedDurationMin = entity.EstimatedDurationMin,
            BookingStrategyType = (int)entity.BookingStrategyType,
            IsActive = entity.IsActive
        });
    }

    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv usluge.");

        if (request.EstimatedDurationMin <= 0)
            return BadRequest("Trajanje usluge mora biti veće od 0 minuta.");

        var businessExists = await DbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        var entity = new Service
        {
            BusinessId = request.BusinessId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            BasePrice = request.BasePrice,
            EstimatedDurationMin = request.EstimatedDurationMin,
            BookingStrategyType = (BookingStrategyType)request.BookingStrategyType,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        DbContext.Services.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            BasePrice = entity.BasePrice,
            EstimatedDurationMin = entity.EstimatedDurationMin,
            BookingStrategyType = (int)entity.BookingStrategyType,
            IsActive = entity.IsActive
        });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ServiceDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv usluge.");

        if (request.EstimatedDurationMin <= 0)
            return BadRequest("Trajanje usluge mora biti veće od 0 minuta.");

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.BasePrice = request.BasePrice;
        entity.EstimatedDurationMin = request.EstimatedDurationMin;
        entity.BookingStrategyType = (BookingStrategyType)request.BookingStrategyType;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            BasePrice = entity.BasePrice,
            EstimatedDurationMin = entity.EstimatedDurationMin,
            BookingStrategyType = (int)entity.BookingStrategyType,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<ServiceDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!entity.IsActive)
            return BadRequest("Usluga je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            BasePrice = entity.BasePrice,
            EstimatedDurationMin = entity.EstimatedDurationMin,
            BookingStrategyType = (int)entity.BookingStrategyType,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<ServiceDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.IsActive)
            return BadRequest("Usluga je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            BasePrice = entity.BasePrice,
            EstimatedDurationMin = entity.EstimatedDurationMin,
            BookingStrategyType = (int)entity.BookingStrategyType,
            IsActive = entity.IsActive
        });
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var entity = await DbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var hasDependencies =
            await DbContext.Appointments.AnyAsync(x => x.ServiceId == id, cancellationToken) ||
            await DbContext.StaffServiceAssignments.AnyAsync(x => x.ServiceId == id, cancellationToken) ||
            await DbContext.ServiceResourceRequirements.AnyAsync(x => x.ServiceId == id, cancellationToken) ||
            await DbContext.ServiceResourceUsages.AnyAsync(x => x.ServiceId == id, cancellationToken) ||
            await DbContext.ServiceSteps.AnyAsync(x => x.ServiceId == id, cancellationToken);

        if (hasDependencies)
        {
            return BadRequest("Usluga ne može da se obriše jer je povezana sa drugim podacima. Prvo uklonite te veze ili je deaktivirajte.");
        }

        DbContext.Services.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

}