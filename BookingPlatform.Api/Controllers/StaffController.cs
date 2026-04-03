using BookingPlatform.Contracts.Staff;
using BookingPlatform.Domain.Staff;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class StaffController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public StaffController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffMemberDto>>> GetAll(
        [FromQuery] long? businessId,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.StaffMembers.AsNoTracking();

        if (businessId.HasValue)
            query = query.Where(x => x.BusinessId == businessId.Value);

        var items = await query
            .OrderBy(x => x.DisplayName)
            .Select(x => new StaffMemberDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                DisplayName = x.DisplayName,
                Title = x.Title,
                IsBookable = x.IsBookable,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<StaffMemberDto>> Create(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        var businessExists = await _dbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Business ne postoji.");

        var entity = new StaffMember
        {
            BusinessId = request.BusinessId,
            DisplayName = request.DisplayName.Trim(),
            Title = request.Title?.Trim(),
            IsBookable = request.IsBookable,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.StaffMembers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new StaffMemberDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DisplayName = entity.DisplayName,
            Title = entity.Title,
            IsBookable = entity.IsBookable,
            IsActive = entity.IsActive
        };

        return Ok(dto);
    }
}