using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WorkingHoursController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public WorkingHoursController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("business")]
    public async Task<ActionResult<List<BusinessWorkingHourDto>>> GetBusinessHours(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DayOfWeek)
            .Select(x => new BusinessWorkingHourDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsClosed = x.IsClosed
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("business")]
    public async Task<ActionResult<BusinessWorkingHourDto>> SetBusinessHour(
        [FromBody] SetBusinessWorkingHourRequest request,
        CancellationToken cancellationToken)
    {
        var businessExists = await _dbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
            return BadRequest("Dan u nedelji nije ispravno zadat.");

        if (request.IsClosed)
        {
            request.StartTime = "00:00";
            request.EndTime = "00:00";
        }

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
            return BadRequest("Vreme početka nije ispravno.");

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
            return BadRequest("Vreme završetka nije ispravno.");
        if (!request.IsClosed && endTime <= startTime)
            return BadRequest("Vreme završetka mora biti posle vremena početka.");

        var entity = await _dbContext.BusinessWorkingHours
            .FirstOrDefaultAsync(
                x => x.BusinessId == request.BusinessId && x.DayOfWeek == request.DayOfWeek,
                cancellationToken);

        if (entity is null)
        {
            entity = new BusinessWorkingHour
            {
                BusinessId = request.BusinessId,
                DayOfWeek = request.DayOfWeek
            };

            _dbContext.BusinessWorkingHours.Add(entity);
        }

        entity.StartTime = startTime;
        entity.EndTime = endTime;
        entity.IsClosed = request.IsClosed;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessWorkingHourDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DayOfWeek = entity.DayOfWeek,
            StartTime = entity.StartTime.ToString(@"hh\:mm"),
            EndTime = entity.EndTime.ToString(@"hh\:mm"),
            IsClosed = entity.IsClosed
        });
    }

    [HttpDelete("business")]
    public async Task<ActionResult> DeleteBusinessHour(
    [FromQuery] long businessId,
    [FromQuery] int dayOfWeek,
    CancellationToken cancellationToken)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            return BadRequest("Dan u nedelji nije ispravno zadat.");

        var entity = await _dbContext.BusinessWorkingHours
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (entity is null)
            return NotFound("Radno vreme za taj dan ne postoji.");

        _dbContext.BusinessWorkingHours.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("staff")]
    public async Task<ActionResult<List<StaffWorkingHourDto>>> GetStaffHours(
        [FromQuery] long staffMemberId,
        CancellationToken cancellationToken)
    {
        var items = await _dbContext.StaffWorkingHours
            .AsNoTracking()
            .Where(x => x.StaffMemberId == staffMemberId)
            .OrderBy(x => x.DayOfWeek)
            .Select(x => new StaffWorkingHourDto
            {
                Id = x.Id,
                StaffMemberId = x.StaffMemberId,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsClosed = x.IsClosed
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("staff")]
    public async Task<ActionResult<StaffWorkingHourDto>> SetStaffHour(
        [FromBody] SetStaffWorkingHourRequest request,
        CancellationToken cancellationToken)
    {
        var staffExists = await _dbContext.StaffMembers
            .AnyAsync(x => x.Id == request.StaffMemberId, cancellationToken);

        if (!staffExists)
            return BadRequest("Izabrani zaposleni ne postoji.");

        if (request.DayOfWeek < 0 || request.DayOfWeek > 6)
            return BadRequest("Dan u nedelji nije ispravno zadat.");

        if (request.IsClosed)
        {
            request.StartTime = "00:00";
            request.EndTime = "00:00";
        }

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
            return BadRequest("Vreme početka nije ispravno.");

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
            return BadRequest("Vreme završetka nije ispravno.");

        if (!request.IsClosed && endTime <= startTime)
            return BadRequest("Vreme završetka mora biti posle vremena početka.");

        var entity = await _dbContext.StaffWorkingHours
            .FirstOrDefaultAsync(
                x => x.StaffMemberId == request.StaffMemberId && x.DayOfWeek == request.DayOfWeek,
                cancellationToken);

        if (entity is null)
        {
            entity = new StaffWorkingHour
            {
                StaffMemberId = request.StaffMemberId,
                DayOfWeek = request.DayOfWeek
            };

            _dbContext.StaffWorkingHours.Add(entity);
        }

        entity.StartTime = startTime;
        entity.EndTime = endTime;
        entity.IsClosed = request.IsClosed;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffWorkingHourDto
        {
            Id = entity.Id,
            StaffMemberId = entity.StaffMemberId,
            DayOfWeek = entity.DayOfWeek,
            StartTime = entity.StartTime.ToString(@"hh\:mm"),
            EndTime = entity.EndTime.ToString(@"hh\:mm"),
            IsClosed = entity.IsClosed
        });
    }

    [HttpDelete("staff")]
    public async Task<ActionResult> DeleteStaffHour(
    [FromQuery] long staffMemberId,
    [FromQuery] int dayOfWeek,
    CancellationToken cancellationToken)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            return BadRequest("Dan u nedelji nije ispravno zadat.");

        var entity = await _dbContext.StaffWorkingHours
            .FirstOrDefaultAsync(
                x => x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek,
                cancellationToken);

        if (entity is null)
            return NotFound("Radno vreme za taj dan ne postoji.");

        _dbContext.StaffWorkingHours.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("blocks")]
    public async Task<ActionResult<List<TimeOffBlockDto>>> GetBlocks(
    [FromQuery] long businessId,
    [FromQuery] long? staffMemberId,
    [FromQuery] DateTime? fromUtc,
    [FromQuery] DateTime? toUtc,
    CancellationToken cancellationToken)
    {
        var query = _dbContext.TimeOffBlocks
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId);

        if (staffMemberId.HasValue)
            query = query.Where(x => x.StaffMemberId == null || x.StaffMemberId == staffMemberId.Value);

        if (fromUtc.HasValue)
            query = query.Where(x => x.EndAtUtc > fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.StartAtUtc < toUtc.Value);

        var items = await query
            .OrderBy(x => x.StartAtUtc)
            .Select(x => new TimeOffBlockDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                StaffMemberId = x.StaffMemberId,
                StartAtUtc = x.StartAtUtc,
                EndAtUtc = x.EndAtUtc,
                BlockType = (int)x.BlockType,
                Reason = x.Reason
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("blocks/{id:long}")]
    public async Task<ActionResult<TimeOffBlockDto>> GetBlockById(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var item = await _dbContext.TimeOffBlocks
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new TimeOffBlockDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                StaffMemberId = x.StaffMemberId,
                StartAtUtc = x.StartAtUtc,
                EndAtUtc = x.EndAtUtc,
                BlockType = (int)x.BlockType,
                Reason = x.Reason
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Blokirano vreme ne postoji.");

        return Ok(item);
    }

    [HttpPost("blocks")]
    public async Task<ActionResult<TimeOffBlockDto>> CreateBlock(
    [FromBody] CreateTimeOffBlockRequest request,
    CancellationToken cancellationToken)
    {
        var businessExists = await _dbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        if (request.StaffMemberId.HasValue)
        {
            var staffExists = await _dbContext.StaffMembers
                .AnyAsync(
                    x => x.Id == request.StaffMemberId.Value &&
                         x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (!staffExists)
                return BadRequest("Izabrani zaposleni ne postoji.");
        }

        if (request.EndAtUtc <= request.StartAtUtc)
            return BadRequest("Kraj perioda mora biti posle početka perioda.");

        var entity = new TimeOffBlock
        {
            BusinessId = request.BusinessId,
            StaffMemberId = request.StaffMemberId,
            StartAtUtc = request.StartAtUtc,
            EndAtUtc = request.EndAtUtc,
            BlockType = (TimeOffBlockType)request.BlockType,
            Reason = request.Reason?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _dbContext.TimeOffBlocks.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TimeOffBlockDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            StaffMemberId = entity.StaffMemberId,
            StartAtUtc = entity.StartAtUtc,
            EndAtUtc = entity.EndAtUtc,
            BlockType = (int)entity.BlockType,
            Reason = entity.Reason
        });
    }

    [HttpPut("blocks/{id:long}")]
    public async Task<ActionResult<TimeOffBlockDto>> UpdateBlock(
    [FromRoute] long id,
    [FromBody] CreateTimeOffBlockRequest request,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TimeOffBlocks
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Blokirano vreme ne postoji.");

        var businessExists = await _dbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        if (request.StaffMemberId.HasValue)
        {
            var staffExists = await _dbContext.StaffMembers
                .AnyAsync(
                    x => x.Id == request.StaffMemberId.Value &&
                         x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (!staffExists)
                return BadRequest("Izabrani zaposleni ne postoji.");
        }

        if (request.EndAtUtc <= request.StartAtUtc)
            return BadRequest("Kraj perioda mora biti posle početka perioda.");

        entity.BusinessId = request.BusinessId;
        entity.StaffMemberId = request.StaffMemberId;
        entity.StartAtUtc = request.StartAtUtc;
        entity.EndAtUtc = request.EndAtUtc;
        entity.BlockType = (TimeOffBlockType)request.BlockType;
        entity.Reason = request.Reason?.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TimeOffBlockDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            StaffMemberId = entity.StaffMemberId,
            StartAtUtc = entity.StartAtUtc,
            EndAtUtc = entity.EndAtUtc,
            BlockType = (int)entity.BlockType,
            Reason = entity.Reason
        });
    }

    [HttpDelete("blocks/{id:long}")]
    public async Task<ActionResult> DeleteBlock(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TimeOffBlocks
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Blokirano vreme ne postoji.");

        _dbContext.TimeOffBlocks.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

}