using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BusinessesController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public BusinessesController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessDto>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Businesses
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new BusinessDto
            {
                Id = x.Id,
                Name = x.Name,
                BusinessType = (int)x.BusinessType,
                Description = x.Description,
                Phone = x.Phone,
                Email = x.Email,
                SlotIntervalMin = x.SlotIntervalMin,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BusinessDto>> GetById(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var item = await _dbContext.Businesses
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new BusinessDto
            {
                Id = x.Id,
                Name = x.Name,
                BusinessType = (int)x.BusinessType,
                Description = x.Description,
                Phone = x.Phone,
                Email = x.Email,
                SlotIntervalMin = x.SlotIntervalMin,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Radnja ne postoji.");

        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<BusinessDto>> Create(
        [FromBody] CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SlotIntervalMin <= 0)
            return BadRequest("Razmak između početaka termina mora biti veći od 0 minuta.");

        if (request.SlotIntervalMin > 180)
            return BadRequest("Razmak između početaka termina je prevelik.");

        var entity = new Business
        {
            Name = request.Name.Trim(),
            BusinessType = (BusinessType)request.BusinessType,
            Description = request.Description?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            SlotIntervalMin = request.SlotIntervalMin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Businesses.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        };

        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<BusinessDto>> Update(
    [FromRoute] long id,
    [FromBody] UpdateBusinessRequest request,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Businesses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv radnje.");

        if (request.SlotIntervalMin <= 0)
            return BadRequest("Razmak između početaka termina mora biti veći od 0 minuta.");

        if (request.SlotIntervalMin > 180)
            return BadRequest("Razmak između početaka termina je prevelik.");

        entity.Name = request.Name.Trim();
        entity.BusinessType = (BusinessType)request.BusinessType;
        entity.Description = request.Description?.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.Email = request.Email?.Trim();
        entity.SlotIntervalMin = request.SlotIntervalMin;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        });
    }
    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<BusinessDto>> Deactivate(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Businesses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (!entity.IsActive)
            return BadRequest("Radnja je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<BusinessDto>> Activate(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Businesses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (entity.IsActive)
            return BadRequest("Radnja je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        });
    }
}