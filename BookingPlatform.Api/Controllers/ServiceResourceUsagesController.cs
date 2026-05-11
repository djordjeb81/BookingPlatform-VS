using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Resources;
using BookingPlatform.Domain.Services;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class ServiceResourceUsagesController : ApiControllerBase
{
    public ServiceResourceUsagesController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceResourceUsageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ServiceResourceUsageDto>>> GetAll(
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

        var items = await DbContext.ServiceResourceUsages
            .AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .Join(
                DbContext.Resources.AsNoTracking(),
                usage => usage.ResourceId,
                resource => resource.Id,
                (usage, resource) => new ServiceResourceUsageDto
                {
                    Id = usage.Id,
                    ServiceId = usage.ServiceId,
                    ResourceId = usage.ResourceId,
                    StaffId = usage.StaffId,
                    ResourceName = resource.Name,
                    ResourceType = (int)resource.ResourceType,
                    CustomerDisplayText = usage.CustomerDisplayText,
                    StartMinute = usage.StartMinute,
                    DurationMin = usage.DurationMin,
                    IsRequired = usage.IsRequired
                })
            .OrderBy(x => x.StartMinute)
            .ThenBy(x => x.ResourceName)
            .ToListAsync(cancellationToken);

        var staffIdsByUsageId = await LoadUsageStaffMemberIdsAsync(
            items.Select(x => x.Id).ToList(),
            cancellationToken);

        foreach (var item in items)
        {
            if (staffIdsByUsageId.TryGetValue(item.Id, out var staffIds))
            {
                item.StaffMemberIds = staffIds;
            }
            else if (item.StaffId.HasValue)
            {
                item.StaffMemberIds = new List<long> { item.StaffId.Value };
            }
        }

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ServiceResourceUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceResourceUsageDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var item = await DbContext.ServiceResourceUsages
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Join(
                DbContext.Resources.AsNoTracking(),
                usage => usage.ResourceId,
                resource => resource.Id,
                (usage, resource) => new
                {
                    Dto = new ServiceResourceUsageDto
                    {
                        Id = usage.Id,
                        ServiceId = usage.ServiceId,
                        ResourceId = usage.ResourceId,
                        StaffId = usage.StaffId,
                        ResourceName = resource.Name,
                        CustomerDisplayText = usage.CustomerDisplayText,
                        ResourceType = (int)resource.ResourceType,
                        StartMinute = usage.StartMinute,
                        DurationMin = usage.DurationMin,
                        IsRequired = usage.IsRequired
                    },
                    usage.ServiceId
                })
            .Join(
                DbContext.Services.AsNoTracking(),
                x => x.ServiceId,
                service => service.Id,
                (x, service) => new
                {
                    x.Dto,
                    service.BusinessId
                })
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Veza usluge i zauzeća resursa ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(item.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var staffIdsByUsageId = await LoadUsageStaffMemberIdsAsync(
    new List<long> { item.Dto.Id },
    cancellationToken);

        if (staffIdsByUsageId.TryGetValue(item.Dto.Id, out var staffIds))
        {
            item.Dto.StaffMemberIds = staffIds;
        }
        else if (item.Dto.StaffId.HasValue)
        {
            item.Dto.StaffMemberIds = new List<long> { item.Dto.StaffId.Value };
        }

        return Ok(item.Dto);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ServiceResourceUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceResourceUsageDto>> Create(
        [FromBody] CreateServiceResourceUsageRequest request,
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

        var resource = await DbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ResourceId, cancellationToken);

        if (resource is null)
            return BadRequest("Izabrani resurs ne postoji.");

        if (resource.BusinessId != service.BusinessId)
            return BadRequest("Resurs i usluga moraju pripadati istoj radnji.");

        var staffMemberIds = NormalizeStaffMemberIds(
            request.StaffId,
            request.StaffMemberIds);

        var staffValidationError = await ValidateRequestedStaffMembersAsync(
            service.BusinessId,
            request.ServiceId,
            request.ResourceId,
            staffMemberIds,
            cancellationToken);

        if (staffValidationError is not null)
            return BadRequest(staffValidationError);

        if (request.StartMinute < 0)
            return BadRequest("StartMinute ne sme biti manji od 0.");

        if (request.DurationMin <= 0)
            return BadRequest("DurationMin mora biti veći od 0.");

        if (request.StartMinute + request.DurationMin > service.EstimatedDurationMin)
            return BadRequest("StartMinute + DurationMin ne sme preći EstimatedDurationMin izabrane usluge.");

        var entity = new ServiceResourceUsage
        {
            ServiceId = request.ServiceId,
            ResourceId = request.ResourceId,
            StaffId = staffMemberIds.Count == 1 ? staffMemberIds[0] : null,
            StartMinute = request.StartMinute,
            DurationMin = request.DurationMin,
            IsRequired = request.IsRequired,
            CustomerDisplayText = NormalizeCustomerDisplayText(request.CustomerDisplayText),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        DbContext.ServiceResourceUsages.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        await ReplaceUsageStaffMembersAsync(entity, staffMemberIds, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceResourceUsageDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            ResourceId = entity.ResourceId,
            StaffId = entity.StaffId,
            StaffMemberIds = staffMemberIds,
            ResourceName = resource.Name,
            ResourceType = (int)resource.ResourceType,
            CustomerDisplayText = entity.CustomerDisplayText,
            StartMinute = entity.StartMinute,
            DurationMin = entity.DurationMin,
            IsRequired = entity.IsRequired
        });
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ServiceResourceUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceResourceUsageDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateServiceResourceUsageRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceResourceUsages
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Veza usluge i zauzeća resursa ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var resource = await DbContext.Resources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ResourceId, cancellationToken);

        if (resource is null)
            return BadRequest("Izabrani resurs ne postoji.");

        if (resource.BusinessId != service.BusinessId)
            return BadRequest("Resurs i usluga moraju pripadati istoj radnji.");

        var staffMemberIds = NormalizeStaffMemberIds(
            request.StaffId,
            request.StaffMemberIds);

        var staffValidationError = await ValidateRequestedStaffMembersAsync(
            service.BusinessId,
            request.ServiceId,
            request.ResourceId,
            staffMemberIds,
            cancellationToken);

        if (staffValidationError is not null)
            return BadRequest(staffValidationError);

        if (request.StartMinute < 0)
            return BadRequest("StartMinute ne sme biti manji od 0.");

        if (request.DurationMin <= 0)
            return BadRequest("DurationMin mora biti veći od 0.");

        if (request.StartMinute + request.DurationMin > service.EstimatedDurationMin)
            return BadRequest("StartMinute + DurationMin ne sme preći EstimatedDurationMin izabrane usluge.");

        entity.ServiceId = request.ServiceId;
        entity.ResourceId = request.ResourceId;
        entity.StaffId = staffMemberIds.Count == 1 ? staffMemberIds[0] : null;
        entity.StartMinute = request.StartMinute;
        entity.DurationMin = request.DurationMin;
        entity.IsRequired = request.IsRequired;
        entity.CustomerDisplayText = NormalizeCustomerDisplayText(request.CustomerDisplayText);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);
        await ReplaceUsageStaffMembersAsync(entity, staffMemberIds, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ServiceResourceUsageDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            ResourceId = entity.ResourceId,
            StaffId = entity.StaffId,
            StaffMemberIds = staffMemberIds,
            ResourceName = resource.Name,
            ResourceType = (int)resource.ResourceType,
            StartMinute = entity.StartMinute,
            DurationMin = entity.DurationMin,
            IsRequired = entity.IsRequired,
            CustomerDisplayText = entity.CustomerDisplayText
        });
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.ServiceResourceUsages
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Veza usluge i zauzeća resursa ne postoji.");

        var service = await DbContext.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.ServiceId, cancellationToken);

        if (service is null)
            return BadRequest("Izabrana usluga ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(service.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.ServiceResourceUsages.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string? NormalizeCustomerDisplayText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length > 200
            ? trimmed[..200]
            : trimmed;
    }

    private static List<long> NormalizeStaffMemberIds(
    long? legacyStaffId,
    List<long>? staffMemberIds)
    {
        var result = new List<long>();

        if (staffMemberIds is not null)
        {
            result.AddRange(staffMemberIds.Where(x => x > 0));
        }

        if (legacyStaffId.HasValue && legacyStaffId.Value > 0)
        {
            result.Add(legacyStaffId.Value);
        }

        return result
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    private async Task<string?> ValidateRequestedStaffMembersAsync(
        long businessId,
        long serviceId,
        long resourceId,
        List<long> staffMemberIds,
        CancellationToken cancellationToken)
    {
        if (staffMemberIds.Count == 0)
            return null;

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .Where(x => staffMemberIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.BusinessId
            })
            .ToListAsync(cancellationToken);

        if (staff.Count != staffMemberIds.Count)
            return "Jedan ili više izabranih radnika ne postoji.";

        if (staff.Any(x => x.BusinessId != businessId))
            return "Jedan ili više izabranih radnika ne pripada istoj radnji.";

        var serviceStaffIds = await DbContext.StaffServiceAssignments
            .AsNoTracking()
            .Where(x =>
                x.ServiceId == serviceId &&
                staffMemberIds.Contains(x.StaffMemberId))
            .Select(x => x.StaffMemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missingServiceStaffIds = staffMemberIds
            .Except(serviceStaffIds)
            .ToList();

        if (missingServiceStaffIds.Count > 0)
            return "Jedan ili više izabranih radnika ne radi ovu uslugu.";

        var resourceStaffIds = await DbContext.StaffResourceAssignments
            .AsNoTracking()
            .Where(x =>
                x.ResourceId == resourceId &&
                staffMemberIds.Contains(x.StaffMemberId))
            .Select(x => x.StaffMemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var missingResourceStaffIds = staffMemberIds
            .Except(resourceStaffIds)
            .ToList();

        if (missingResourceStaffIds.Count > 0)
            return "Jedan ili više izabranih radnika ne radi sa ovim resursom.";

        return null;
    }

    private async Task ReplaceUsageStaffMembersAsync(
        ServiceResourceUsage usage,
        List<long> staffMemberIds,
        CancellationToken cancellationToken)
    {
        var existing = await DbContext.ServiceResourceUsageStaffMembers
            .Where(x => x.ServiceResourceUsageId == usage.Id)
            .ToListAsync(cancellationToken);

        DbContext.ServiceResourceUsageStaffMembers.RemoveRange(existing);

        var now = DateTime.UtcNow;

        foreach (var staffMemberId in staffMemberIds.Distinct().OrderBy(x => x))
        {
            DbContext.ServiceResourceUsageStaffMembers.Add(new ServiceResourceUsageStaffMember
            {
                ServiceResourceUsageId = usage.Id,
                StaffMemberId = staffMemberId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
    }

    private async Task<Dictionary<long, List<long>>> LoadUsageStaffMemberIdsAsync(
        List<long> usageIds,
        CancellationToken cancellationToken)
    {
        if (usageIds.Count == 0)
            return new Dictionary<long, List<long>>();

        var items = await DbContext.ServiceResourceUsageStaffMembers
            .AsNoTracking()
            .Where(x => usageIds.Contains(x.ServiceResourceUsageId))
            .OrderBy(x => x.StaffMemberId)
            .Select(x => new
            {
                x.ServiceResourceUsageId,
                x.StaffMemberId
            })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(x => x.ServiceResourceUsageId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.StaffMemberId).Distinct().OrderBy(x => x).ToList());
    }

    private async Task<string?> ValidateRequestedStaffAsync(
        long businessId,
        long serviceId,
        long resourceId,
        long? staffId,
        CancellationToken cancellationToken)
    {
        if (!staffId.HasValue)
            return null;

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == staffId.Value, cancellationToken);

        if (staff is null)
            return "Izabrani radnik ne postoji.";

        if (staff.BusinessId != businessId)
            return "Izabrani radnik ne pripada istoj radnji.";

        var staffCanDoService = await DbContext.StaffServiceAssignments
            .AsNoTracking()
            .AnyAsync(
                x => x.StaffMemberId == staffId.Value &&
                     x.ServiceId == serviceId,
                cancellationToken);

        if (!staffCanDoService)
            return "Izabrani radnik ne radi ovu uslugu.";

        var staffHasResource = await DbContext.StaffResourceAssignments
            .AsNoTracking()
            .AnyAsync(
                x => x.StaffMemberId == staffId.Value &&
                     x.ResourceId == resourceId,
                cancellationToken);

        if (!staffHasResource)
            return "Izabrani radnik ne radi sa ovim resursom.";

        return null;
    }
}