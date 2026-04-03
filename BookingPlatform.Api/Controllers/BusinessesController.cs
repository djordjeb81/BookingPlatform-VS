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
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<BusinessDto>> Create(
        [FromBody] CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var entity = new Business
        {
            Name = request.Name.Trim(),
            BusinessType = (BusinessType)request.BusinessType,
            Description = request.Description?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
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
            IsActive = entity.IsActive
        };

        return CreatedAtAction(nameof(GetAll), new { id = entity.Id }, dto);
    }
}