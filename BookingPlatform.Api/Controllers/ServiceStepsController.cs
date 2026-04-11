using BookingPlatform.Contracts.Services;
using BookingPlatform.Domain.Services;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ServiceStepsController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public ServiceStepsController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<ServiceStepDto>>> GetAll(
        [FromQuery] long serviceId,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.ServiceSteps
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
    public async Task<ActionResult<ServiceStepDto>> GetById(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var item = await _dbContext.ServiceSteps
            .AsNoTracking()
            .Where(x => x.Id == id)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Korak usluge ne postoji.");

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceStepDto>> Create(
        [FromBody] CreateServiceStepRequest request,
        CancellationToken cancellationToken)
    {
        var serviceExists = await _dbContext.Services
            .AnyAsync(x => x.Id == request.ServiceId, cancellationToken);

        if (!serviceExists)
            return BadRequest("Izabrana usluga ne postoji.");

        var orderExists = await _dbContext.ServiceSteps
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

        _dbContext.ServiceSteps.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ServiceStepDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            StepOrder = entity.StepOrder,
            Name = entity.Name,
            DurationMin = entity.DurationMin,
            ClientPresenceRequired = entity.ClientPresenceRequired,
            SameStaffAsPrevious = entity.SameStaffAsPrevious
        };

        return Ok(dto);
    }
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ServiceStepDto>> Update(
    [FromRoute] long id,
    [FromBody] UpdateServiceStepRequest request,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ServiceSteps
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Korak usluge ne postoji.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv koraka.");

        if (request.DurationMin <= 0)
            return BadRequest("Trajanje koraka mora biti veće od 0 minuta.");

        var orderExists = await _dbContext.ServiceSteps
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

        await _dbContext.SaveChangesAsync(cancellationToken);

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
    public async Task<ActionResult> Delete(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ServiceSteps
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Korak usluge ne postoji.");

        _dbContext.ServiceSteps.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}