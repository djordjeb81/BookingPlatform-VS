using BookingPlatform.Contracts.Services;
using BookingPlatform.Domain.Services;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ServicesController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public ServicesController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAll(
        [FromQuery] long? businessId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Services.AsNoTracking();

        if (businessId.HasValue)
            query = query.Where(x => x.BusinessId == businessId.Value);

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

    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var businessExists = await _dbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Business ne postoji.");

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

        _dbContext.Services.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new ServiceDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            BasePrice = entity.BasePrice,
            EstimatedDurationMin = entity.EstimatedDurationMin,
            BookingStrategyType = (int)entity.BookingStrategyType,
            IsActive = entity.IsActive
        };

        return Ok(dto);
    }
}