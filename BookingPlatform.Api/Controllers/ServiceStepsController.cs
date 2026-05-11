using BookingPlatform.Contracts.Services;
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
public sealed class ServiceStepsController : ApiControllerBase
{
    public ServiceStepsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceStepDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ServiceStepDto>>> GetAll(
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

        var items = await DbContext.ServiceSteps
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .OrderBy(x => x.StepOrder)
            .Select(x => new ServiceStepDto
            {
                Id = x.Id,
                ServiceId = x.ServiceId,
                StepOrder = x.StepOrder,
                Name = x.Name,
                DurationMin = x.DurationMin,
                ClientPresenceRequired = x.ClientPresenceRequired,
                SameStaffAsPrevious = x.SameStaffAsPrevious
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ServiceStepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceStepDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceSteps
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Korak usluge ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .Where(x => x.Id == entity.ServiceId)
            .Select(x => new { x.Id, x.BusinessId })
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(new ServiceStepDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            StepOrder = entity.StepOrder,
            Name = entity.Name,
            DurationMin = entity.DurationMin,
            ClientPresenceRequired = entity.ClientPresenceRequired,
            SameStaffAsPrevious = entity.SameStaffAsPrevious
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServiceStepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceStepDto>> Create(
        [FromBody] CreateServiceStepRequest request,
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

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv koraka.");

        if (request.DurationMin <= 0)
            return BadRequest("Trajanje koraka mora biti veće od 0 minuta.");

        if (request.StepOrder <= 0)
            return BadRequest("Redosled koraka mora biti veći od 0.");

        var orderExists = await DbContext.ServiceSteps
            .AnyAsync(x => x.ServiceId == request.ServiceId && x.StepOrder == request.StepOrder, cancellationToken);

        if (orderExists)
            return BadRequest("Redosled koraka već postoji za ovu uslugu.");

        var entity = new ServiceStep
        {
            ServiceId = request.ServiceId,
            StepOrder = request.StepOrder,
            Name = request.Name.Trim(),
            DurationMin = request.DurationMin,
            ClientPresenceRequired = request.ClientPresenceRequired,
            SameStaffAsPrevious = request.SameStaffAsPrevious
        };

        DbContext.ServiceSteps.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceStepDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            StepOrder = entity.StepOrder,
            Name = entity.Name,
            DurationMin = entity.DurationMin,
            ClientPresenceRequired = entity.ClientPresenceRequired,
            SameStaffAsPrevious = entity.SameStaffAsPrevious
        });
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ServiceStepDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceStepDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateServiceStepRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceSteps
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Korak usluge ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv koraka.");

        if (request.DurationMin <= 0)
            return BadRequest("Trajanje koraka mora biti veće od 0 minuta.");

        if (request.StepOrder <= 0)
            return BadRequest("Redosled koraka mora biti veći od 0.");

        var orderExists = await DbContext.ServiceSteps
            .AnyAsync(
                x => x.ServiceId == entity.ServiceId &&
                     x.Id != id &&
                     x.StepOrder == request.StepOrder,
                cancellationToken);

        if (orderExists)
            return BadRequest("Redosled koraka već postoji za ovu uslugu.");

        entity.StepOrder = request.StepOrder;
        entity.Name = request.Name.Trim();
        entity.DurationMin = request.DurationMin;
        entity.ClientPresenceRequired = request.ClientPresenceRequired;
        entity.SameStaffAsPrevious = request.SameStaffAsPrevious;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceStepDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            StepOrder = entity.StepOrder,
            Name = entity.Name,
            DurationMin = entity.DurationMin,
            ClientPresenceRequired = entity.ClientPresenceRequired,
            SameStaffAsPrevious = entity.SameStaffAsPrevious
        });
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceSteps
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Korak usluge ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.ServiceSteps.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}