using System.Security.Claims;
using BookingPlatform.Contracts.Staff;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Staff;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Resources;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class StaffController : ApiControllerBase
{
    

    public StaffController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    public async Task<ActionResult<List<StaffMemberDto>>> GetAll(
        [FromQuery] long? businessId,
        CancellationToken cancellationToken)
    {
        IQueryable<StaffMember> query = DbContext.StaffMembers.AsNoTracking();

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
            .OrderBy(x => x.DisplayName)
            .Select(x => new StaffMemberDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                DisplayName = x.DisplayName,
                ScheduleMode = (int)x.ScheduleMode,
                Title = x.Title,
                IsBookable = x.IsBookable,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<StaffMemberDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(new StaffMemberDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DisplayName = entity.DisplayName,
            Title = entity.Title,
            ScheduleMode = (int)entity.ScheduleMode,
            IsBookable = entity.IsBookable,
            IsActive = entity.IsActive
        });
    }

    [HttpGet("{staffId:long}/services")]
    public async Task<ActionResult<List<StaffServiceSelectionItemDto>>> GetServices(
    [FromRoute] long staffId,
    CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var assignedServiceIds = await DbContext.StaffServiceAssignments
            .AsNoTracking()
            .Where(x => x.StaffMemberId == staffId)
            .Select(x => x.ServiceId)
            .ToListAsync(cancellationToken);

        var items = await DbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == staff.BusinessId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new StaffServiceSelectionItemDto
            {
                ServiceId = x.Id,
                ServiceName = x.Name,
                IsAssigned = assignedServiceIds.Contains(x.Id)
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("{staffId:long}/services")]
    public async Task<ActionResult<List<StaffServiceAssignmentDto>>> UpdateServices(
    [FromRoute] long staffId,
    [FromBody] UpdateStaffServicesRequest request,
    CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var requestedServiceIds = request.ServiceIds
            .Distinct()
            .ToList();

        var validServiceIds = await DbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == staff.BusinessId && requestedServiceIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validServiceIds.Count != requestedServiceIds.Count)
            return BadRequest("Jedna ili više usluga ne postoje ili ne pripadaju istoj radnji kao zaposleni.");

        var existingAssignments = await DbContext.StaffServiceAssignments
            .Where(x => x.StaffMemberId == staffId)
            .ToListAsync(cancellationToken);

        DbContext.StaffServiceAssignments.RemoveRange(existingAssignments);

        var newAssignments = requestedServiceIds
            .Select(serviceId => new StaffServiceAssignment
            {
                StaffMemberId = staffId,
                ServiceId = serviceId
            })
            .ToList();

        DbContext.StaffServiceAssignments.AddRange(newAssignments);
        await DbContext.SaveChangesAsync(cancellationToken);

        var result = newAssignments
            .Select(x => new StaffServiceAssignmentDto
            {
                StaffMemberId = x.StaffMemberId,
                ServiceId = x.ServiceId
            })
            .ToList();

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<StaffMemberDto>> Create(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Unesite ime zaposlenog.");

        var businessExists = await DbContext.Businesses
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        var entity = new StaffMember
        {
            BusinessId = request.BusinessId,
            DisplayName = request.DisplayName.Trim(),
            Title = request.Title?.Trim(),
            ScheduleMode = (StaffScheduleMode)request.ScheduleMode,
            IsBookable = request.IsBookable,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };


        DbContext.StaffMembers.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffMemberDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DisplayName = entity.DisplayName,
            Title = entity.Title,
            ScheduleMode = (int)entity.ScheduleMode,
            IsBookable = entity.IsBookable,
            IsActive = entity.IsActive
        });
    }

    [HttpGet("{staffId:long}/resources")]
    public async Task<ActionResult<List<StaffResourceSelectionItemDto>>> GetResources(
    [FromRoute] long staffId,
    CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var assignedResourceIds = await DbContext.StaffResourceAssignments
            .AsNoTracking()
            .Where(x => x.StaffMemberId == staffId)
            .Select(x => x.ResourceId)
            .ToListAsync(cancellationToken);

        var items = await DbContext.Resources
            .AsNoTracking()
            .Where(x => x.BusinessId == staff.BusinessId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new StaffResourceSelectionItemDto
            {
                ResourceId = x.Id,
                ResourceName = x.Name,
                ResourceType = (int)x.ResourceType,
                IsAssigned = assignedResourceIds.Contains(x.Id)
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("{staffId:long}/resources")]
    public async Task<ActionResult<List<StaffResourceSelectionItemDto>>> UpdateResources(
    [FromRoute] long staffId,
    [FromBody] UpdateStaffResourcesRequest request,
    CancellationToken cancellationToken)
    {
        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffId, cancellationToken);

        if (staff is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(staff.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var requestedResourceIds = request.ResourceIds
            .Distinct()
            .ToList();

        var validResourceIds = await DbContext.Resources
            .AsNoTracking()
            .Where(x => x.BusinessId == staff.BusinessId && requestedResourceIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validResourceIds.Count != requestedResourceIds.Count)
            return BadRequest("Jedan ili više resursa ne postoje ili ne pripadaju istoj radnji kao zaposleni.");

        var existingAssignments = await DbContext.StaffResourceAssignments
            .Where(x => x.StaffMemberId == staffId)
            .ToListAsync(cancellationToken);

        DbContext.StaffResourceAssignments.RemoveRange(existingAssignments);

        var newAssignments = requestedResourceIds
            .Select(resourceId => new StaffResourceAssignment
            {
                StaffMemberId = staffId,
                ResourceId = resourceId
            })
            .ToList();

        DbContext.StaffResourceAssignments.AddRange(newAssignments);
        await DbContext.SaveChangesAsync(cancellationToken);

        var items = await DbContext.Resources
            .AsNoTracking()
            .Where(x => x.BusinessId == staff.BusinessId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new StaffResourceSelectionItemDto
            {
                ResourceId = x.Id,
                ResourceName = x.Name,
                ResourceType = (int)x.ResourceType,
                IsAssigned = requestedResourceIds.Contains(x.Id)
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<StaffMemberDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            return BadRequest("Unesite ime zaposlenog.");

        entity.DisplayName = request.DisplayName.Trim();
        entity.Title = request.Title?.Trim();
        entity.ScheduleMode = (StaffScheduleMode)request.ScheduleMode;
        entity.IsBookable = request.IsBookable;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffMemberDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DisplayName = entity.DisplayName,
            Title = entity.Title,
            ScheduleMode = (int)entity.ScheduleMode,
            IsBookable = entity.IsBookable,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/deactivate")]
    public async Task<ActionResult<StaffMemberDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!entity.IsActive)
            return BadRequest("Zaposleni je već neaktivan.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffMemberDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DisplayName = entity.DisplayName,
            Title = entity.Title,
            ScheduleMode = (int)entity.ScheduleMode,
            IsBookable = entity.IsBookable,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/activate")]
    public async Task<ActionResult<StaffMemberDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.IsActive)
            return BadRequest("Zaposleni je već aktivan.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new StaffMemberDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            DisplayName = entity.DisplayName,
            Title = entity.Title,
            ScheduleMode = (int)entity.ScheduleMode,
            IsBookable = entity.IsBookable,
            IsActive = entity.IsActive
        });
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(
    [FromRoute] long id,
    CancellationToken cancellationToken)
    {
        var entity = await DbContext.StaffMembers
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Zaposleni ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var hasDependencies =
            await DbContext.Appointments.AnyAsync(x => x.PrimaryStaffMemberId == id, cancellationToken) ||
            await DbContext.StaffServiceAssignments.AnyAsync(x => x.StaffMemberId == id, cancellationToken) ||
            await DbContext.StaffResourceAssignments.AnyAsync(x => x.StaffMemberId == id, cancellationToken) ||
            await DbContext.StaffScheduleRules.AnyAsync(x => x.StaffMemberId == id, cancellationToken) ||
            await DbContext.StaffScheduleOverrides.AnyAsync(x => x.StaffMemberId == id, cancellationToken) ||
            await DbContext.TimeOffBlocks.AnyAsync(x => x.StaffMemberId == id, cancellationToken);

        if (hasDependencies)
        {
            return BadRequest("Radnik ne može da se obriše jer je povezan sa drugim podacima. Prvo uklonite te veze ili ga deaktivirajte.");
        }

        DbContext.StaffMembers.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

}