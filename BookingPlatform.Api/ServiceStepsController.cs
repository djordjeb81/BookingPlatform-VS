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

    [HttpPost]
    public async Task<ActionResult<ServiceStepDto>> Create(
        [FromBody] CreateServiceStepRequest request,
        CancellationToken cancellationToken)
    {
        var serviceExists = await _dbContext.Services
            .AnyAsync(x => x.Id == request.ServiceId, cancellationToken);

        if (!serviceExists)
            return BadRequest("Service ne postoji.");

        var orderExists = await _dbContext.ServiceSteps
            .AnyAsync(x => x.ServiceId == request.ServiceId && x.StepOrder == request.StepOrder, cancellationToken);

        if (orderExists)
            return BadRequest("StepOrder već postoji za ovu uslugu.");

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
}