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
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceDto>> GetById(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var item = await _dbContext.Services
            .AsNoTracking()
            .Where(x => x.Id == id)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Usluga ne postoji.");

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        var businessExists = await _dbContext.Businesses
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
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ServiceDto>> Update(
    [FromRoute] long id,
    [FromBody] UpdateServiceRequest request,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

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

        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var entity = await _dbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        if (!entity.IsActive)
            return BadRequest("Usluga je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var entity = await _dbContext.Services
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Usluga ne postoji.");

        if (entity.IsActive)
            return BadRequest("Usluga je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

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
}