using BookingPlatform.Contracts.BusinessPortal;
using BookingPlatform.Contracts.CustomerPortal;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Globalization;
using System.Text;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
public sealed class CustomerPortalController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public CustomerPortalController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(CustomerPortalMeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerPortalMeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return Unauthorized("Korisnik ne postoji.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);

        return Ok(new CustomerPortalMeResponse
        {
            AppUserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            HasCustomerProfile = profile is not null,
            CustomerProfileId = profile?.Id,
            CustomerName = profile?.FullName,
            Phone = profile?.Phone
        });
    }

    [HttpGet("my-businesses")]
    [ProducesResponseType(typeof(List<CustomerPortalBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CustomerPortalBusinessDto>>> MyBusinesses(
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Ok(new List<CustomerPortalBusinessDto>());

        var items = await _dbContext.BusinessCustomers
            .AsNoTracking()
.Where(x => x.CustomerProfileId == profile.Id && x.IsActive)
            .Join(
                _dbContext.Businesses.AsNoTracking(),
                customer => customer.BusinessId,
                business => business.Id,
                (customer, business) => new CustomerPortalBusinessDto
                {
                    BusinessId = business.Id,
                    BusinessName = business.Name,
                    BusinessCustomerId = customer.Id,
                    CustomerProfileId = profile.Id,
                    CustomerName = profile.FullName,
                    BusinessPhone = business.Phone,
                    BusinessEmail = business.Email,
                    City = business.City
                })
            .OrderBy(x => x.BusinessName)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpPost("remove-saved-business")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RemoveSavedBusiness(
    [FromBody] RemoveSavedBusinessRequest request,
    CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        if (profile is null)
            return NotFound("Klijent profil ne postoji.");

        var businessCustomer = await _dbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.BusinessId == request.BusinessId &&
                     x.CustomerProfileId == profile.Id &&
                     x.IsActive,
                cancellationToken);

        if (businessCustomer is null)
            return NotFound("Nije pronađeno u vašoj listi.");

        var now = DateTime.UtcNow;

        businessCustomer.IsActive = false;
        businessCustomer.RemovedFromCustomerListAtUtc = now;
        businessCustomer.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Uklonjeno je iz sačuvanih."
        });
    }

    [HttpGet("business-search")]
    [ProducesResponseType(typeof(List<CustomerBusinessSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CustomerBusinessSearchResultDto>>> BusinessSearch(
    [FromQuery] string? q,
    [FromQuery] string? city,
    [FromQuery] int? businessType,
    [FromQuery] string? service,
    [FromQuery] double? latitude,
    [FromQuery] double? longitude,
    [FromQuery] double? radiusKm,
    CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        var normalizedQuery = q?.Trim();
        var normalizedCity = city?.Trim();
        var normalizedService = service?.Trim();

        var searchQuery = NormalizeSearchText(normalizedQuery);
        var searchCity = NormalizeSearchText(normalizedCity);
        var searchService = NormalizeSearchText(normalizedService);

        var query = _dbContext.Businesses
            .AsNoTracking()
            .Where(x => x.IsActive);


        if (businessType.HasValue)
        {
            query = query.Where(x => (int)x.BusinessType == businessType.Value);
        }

        var businesses = await query
            .OrderBy(x => x.Name)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Phone,
                x.Email,
                x.Street,
                x.StreetNumber,
                x.City,
                x.PostalCode,
                x.Country,
                BusinessType = (int)x.BusinessType,
                x.Latitude,
                x.Longitude
            })
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            businesses = businesses
                .Where(x =>
                    NormalizeSearchText(x.Name).Contains(searchQuery) ||
                    NormalizeSearchText(x.City).Contains(searchQuery) ||
                    NormalizeSearchText(x.Phone).Contains(searchQuery) ||
                    NormalizeSearchText(x.Email).Contains(searchQuery))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(searchCity))
        {
            businesses = businesses
                .Where(x => NormalizeSearchText(x.City).Contains(searchCity))
                .ToList();
        }

        var businessIds = businesses.Select(x => x.Id).ToList();

        if (!string.IsNullOrWhiteSpace(searchService))
        {
            var servicesForSearch = await _dbContext.Services
                .AsNoTracking()
                .Where(x =>
                    businessIds.Contains(x.BusinessId) &&
                    x.IsActive)
                .Select(x => new
                {
                    x.BusinessId,
                    x.Name
                })
                .ToListAsync(cancellationToken);

            var businessIdsWithService = servicesForSearch
                .Where(x => NormalizeSearchText(x.Name).Contains(searchService))
                .Select(x => x.BusinessId)
                .Distinct()
                .ToList();

            businesses = businesses
                .Where(x => businessIdsWithService.Contains(x.Id))
                .ToList();

            businessIds = businesses.Select(x => x.Id).ToList();
        }

        var matchingServicesRaw = await _dbContext.Services
            .AsNoTracking()
            .Where(x =>
                businessIds.Contains(x.BusinessId) &&
                x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.BusinessId,
                x.Name
            })
            .ToListAsync(cancellationToken);

        var matchingServices = string.IsNullOrWhiteSpace(searchService)
            ? matchingServicesRaw
            : matchingServicesRaw
                .Where(x => NormalizeSearchText(x.Name).Contains(searchService))
                .ToList();

        var connectedCustomers = profile is null
            ? new List<CustomerBusinessConnectionRow>()
            : await _dbContext.BusinessCustomers
                .AsNoTracking()
                .Where(x =>
                    x.CustomerProfileId == profile.Id &&
                    businessIds.Contains(x.BusinessId) &&
                    x.IsActive)
                .Select(x => new CustomerBusinessConnectionRow
                {
                    BusinessId = x.BusinessId,
                    BusinessCustomerId = x.Id
                })
                .ToListAsync(cancellationToken);

        var connectedByBusinessId = connectedCustomers
            .ToDictionary(x => x.BusinessId, x => x.BusinessCustomerId);

        var results = businesses
            .Select(x =>
            {
                var businessLatitude = x.Latitude.HasValue
                    ? (double?)x.Latitude.Value
                    : null;

                var businessLongitude = x.Longitude.HasValue
                    ? (double?)x.Longitude.Value
                    : null;

                var distanceKm = CalculateDistanceKm(
                    latitude,
                    longitude,
                    businessLatitude,
                    businessLongitude);

                connectedByBusinessId.TryGetValue(x.Id, out var businessCustomerId);

                return new CustomerBusinessSearchResultDto
                {
                    BusinessId = x.Id,
                    BusinessName = x.Name,
                    BusinessPhone = x.Phone,
                    BusinessEmail = x.Email,
                    Street = x.Street,
                    StreetNumber = x.StreetNumber,
                    City = x.City,
                    PostalCode = x.PostalCode,
                    Country = x.Country,
                    BusinessType = x.BusinessType,
                    Latitude = businessLatitude,
                    Longitude = businessLongitude,
                    DistanceKm = distanceKm,
                    IsAlreadyConnected = businessCustomerId > 0,
                    BusinessCustomerId = businessCustomerId > 0 ? businessCustomerId : null,
                    MatchingServices = matchingServices
                        .Where(s => s.BusinessId == x.Id)
                        .Select(s => s.Name)
                        .Take(5)
                        .ToList()
                };
            })
.Where(x =>
    !radiusKm.HasValue ||
    !x.DistanceKm.HasValue ||
    x.DistanceKm.Value <= radiusKm.GetValueOrDefault())
            .OrderBy(x => x.DistanceKm ?? double.MaxValue)
            .ThenBy(x => x.BusinessName)
            .Take(50)
            .ToList();

        return Ok(results);
    }

    [HttpGet("business-profile")]
    [ProducesResponseType(typeof(CustomerBusinessProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerBusinessProfileDto>> BusinessProfile(
    [FromQuery] long businessId,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var business = await _dbContext.Businesses
            .AsNoTracking()
            .Where(x => x.Id == businessId && x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Phone,
                x.Email,
                x.Street,
                x.StreetNumber,
                x.City,
                x.PostalCode,
                x.Country,
                BusinessType = (int)x.BusinessType,
                x.Latitude,
                x.Longitude
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
            return NotFound("Mesto ne postoji ili nije aktivno.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        long? businessCustomerId = null;

        if (profile is not null)
        {
            businessCustomerId = await _dbContext.BusinessCustomers
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.CustomerProfileId == profile.Id &&
                    x.IsActive)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var services = await _dbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .Take(20)
            .ToListAsync(cancellationToken);

        var latitude = business.Latitude.HasValue
            ? (double?)business.Latitude.Value
            : null;

        var longitude = business.Longitude.HasValue
            ? (double?)business.Longitude.Value
            : null;

        return Ok(new CustomerBusinessProfileDto
        {
            BusinessId = business.Id,
            BusinessName = business.Name,
            BusinessPhone = business.Phone,
            BusinessEmail = business.Email,
            Street = business.Street,
            StreetNumber = business.StreetNumber,
            City = business.City,
            PostalCode = business.PostalCode,
            Country = business.Country,
            BusinessType = business.BusinessType,
            Latitude = latitude,
            Longitude = longitude,
            IsAlreadyConnected = businessCustomerId.HasValue,
            BusinessCustomerId = businessCustomerId,
            Services = services
        });
    }

    [HttpPost("connect-business")]
    [ProducesResponseType(typeof(ConnectCustomerToBusinessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ConnectCustomerToBusinessResponse>> ConnectBusiness(
    [FromBody] ConnectCustomerToBusinessRequest request,
    CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return Unauthorized("Korisnik ne postoji.");

        var business = await _dbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.BusinessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return BadRequest("Izabrani biznis ne postoji ili nije aktivan.");

        var profile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new BookingPlatform.Domain.Customers.CustomerProfile
            {
                AppUserId = user.Id,
                FullName = string.IsNullOrWhiteSpace(user.FullName)
                    ? user.Email
                    : user.FullName,
                Phone = null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _dbContext.CustomerProfiles.Add(profile);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingBusinessCustomer = await _dbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.BusinessId == request.BusinessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        if (existingBusinessCustomer is not null)
        {
            if (!existingBusinessCustomer.IsActive)
            {
                existingBusinessCustomer.IsActive = true;
                existingBusinessCustomer.RemovedFromCustomerListAtUtc = null;
                existingBusinessCustomer.UpdatedAtUtc = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(new ConnectCustomerToBusinessResponse
            {
                BusinessId = request.BusinessId,
                BusinessCustomerId = existingBusinessCustomer.Id,
                WasAlreadyConnected = true,
                Message = "Već ste povezani sa ovim biznisom."
            });
        }

        var now = DateTime.UtcNow;

        var businessCustomer = new BookingPlatform.Domain.Customers.BusinessCustomer
        {
            BusinessId = request.BusinessId,
            CustomerProfileId = profile.Id,
            AppUserId = user.Id,
            FullName = profile.FullName,
            Phone = profile.Phone,
            Email = user.Email,
            Notes = "Klijent se povezao preko Android aplikacije.",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.BusinessCustomers.Add(businessCustomer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ConnectCustomerToBusinessResponse
        {
            BusinessId = request.BusinessId,
            BusinessCustomerId = businessCustomer.Id,
            WasAlreadyConnected = false,
            Message = "Uspešno ste se povezali sa biznisom."
        });
    }

    [HttpGet("services")]
    [ProducesResponseType(typeof(List<BusinessPortalServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BusinessPortalServiceDto>>> Services(
       [FromQuery] long businessId,
       CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        if (profile is null)
            return Forbid();

        var hasCustomerAccess = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id &&
                     x.IsActive,
                cancellationToken);

        if (!hasCustomerAccess)
            return Forbid();

        var items = await _dbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new BusinessPortalServiceDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                Name = x.Name,
                Description = x.Description,
                EstimatedDurationMin = x.EstimatedDurationMin,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);

    }

    [HttpGet("staff-members")]
    [ProducesResponseType(typeof(List<BusinessPortalStaffMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BusinessPortalStaffMemberDto>>> StaffMembers(
    [FromQuery] long businessId,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AppUserId == userId.Value,
                cancellationToken);

        if (profile is null)
            return Forbid();

        var hasCustomerAccess = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id &&
                     x.IsActive,
                cancellationToken);

        if (!hasCustomerAccess)
            return Forbid();

        var items = await _dbContext.StaffMembers
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                x.IsBookable)
            .OrderBy(x => x.DisplayName)
            .Select(x => new BusinessPortalStaffMemberDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                DisplayName = x.DisplayName,
                Title = x.Title,
                ScheduleMode = (int)x.ScheduleMode,
                IsBookable = x.IsBookable,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("resources")]
    [ProducesResponseType(typeof(List<BusinessPortalResourceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BusinessPortalResourceDto>>> Resources(
    [FromQuery] long businessId,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await _dbContext.Resources
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.Name)
.Select(x => new BusinessPortalResourceDto
{
    Id = x.Id,
    BusinessId = x.BusinessId,
    Name = x.Name,
    ResourceType = (int)x.ResourceType,
    Capacity = x.Capacity,
    AllowParallelUsage = x.AllowParallelUsage,
    CreatesOccupancy = x.CreatesOccupancy,
    IsActive = x.IsActive
})
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("my-appointments")]
    [ProducesResponseType(typeof(List<CustomerPortalAppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CustomerPortalAppointmentDto>>> MyAppointments(
    CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Ok(new List<CustomerPortalAppointmentDto>());

        var businessCustomerIds = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .Where(x => x.CustomerProfileId == profile.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (businessCustomerIds.Count == 0)
            return Ok(new List<CustomerPortalAppointmentDto>());

        var nowUtc = DateTime.UtcNow;

        var baseQuery =
            from appointment in _dbContext.Appointments.AsNoTracking()
            join business in _dbContext.Businesses.AsNoTracking()
                on appointment.BusinessId equals business.Id
            join service in _dbContext.Services.AsNoTracking()
                on appointment.ServiceId equals service.Id into serviceJoin
            from service in serviceJoin.DefaultIfEmpty()
            join staff in _dbContext.StaffMembers.AsNoTracking()
                on appointment.PrimaryStaffMemberId equals staff.Id into staffJoin
            from staff in staffJoin.DefaultIfEmpty()
            where appointment.BusinessCustomerId != null &&
      businessCustomerIds.Contains(appointment.BusinessCustomerId ?? 0)
            select new CustomerPortalAppointmentDto
            {
                Id = appointment.Id,
                BusinessId = business.Id,
                BusinessName = business.Name,
                BusinessCustomerId = appointment.BusinessCustomerId ?? 0,
                ServiceId = appointment.ServiceId,
                ServiceName = service != null ? service.Name : null,
                PrimaryStaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staff != null ? staff.DisplayName : null,
                ResourceId = appointment.ResourceId,
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                Status = appointment.Status.ToString(),
                Notes = appointment.Notes
            };

        var upcoming = await baseQuery
            .Where(x => x.StartAtUtc >= nowUtc)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var past = await baseQuery
            .Where(x => x.StartAtUtc < nowUtc)
            .OrderByDescending(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(upcoming.Concat(past).ToList());
    }

    [HttpGet("appointments/{appointmentId:long}/timeline")]
    [ProducesResponseType(typeof(CustomerAppointmentTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerAppointmentTimelineDto>> AppointmentTimeline(
    [FromRoute] long appointmentId,
    CancellationToken cancellationToken)
    {
        if (appointmentId <= 0)
            return BadRequest("appointmentId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Forbid();

        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .Where(x => x.Id == appointmentId)
            .Select(x => new
            {
                x.Id,
                x.BusinessId,
                x.BusinessCustomerId,
                x.ServiceId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
            return NotFound("Termin nije pronađen.");

        if (!appointment.BusinessCustomerId.HasValue)
            return Forbid();

        var hasCustomerAccess = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == appointment.BusinessCustomerId.Value &&
                     x.BusinessId == appointment.BusinessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        if (!hasCustomerAccess)
            return Forbid();

        var steps = await (
            from usage in _dbContext.ServiceResourceUsages.AsNoTracking()
            join resource in _dbContext.Resources.AsNoTracking()
                on usage.ResourceId equals resource.Id
            where usage.ServiceId == appointment.ServiceId
            orderby usage.StartMinute, usage.DurationMin, resource.Name

            select new CustomerAppointmentTimelineStepDto
            {
                StartMinute = usage.StartMinute,
                DurationMin = usage.DurationMin,
                Title = !string.IsNullOrWhiteSpace(usage.CustomerDisplayText)
                    ? usage.CustomerDisplayText
                    : !string.IsNullOrWhiteSpace(resource.CustomerActionText)
                        ? resource.CustomerActionText
                        : resource.Name
            })

            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            return Ok(new CustomerAppointmentTimelineDto
            {
                AppointmentId = appointment.Id,
                Text =
                    "Tok usluge još nije podešen za ovu uslugu.\n\n" +
                    "Radnja može naknadno da podesi korake usluge."
            });
        }

        var visibleSteps = MergeTimelineStepsForCustomer(steps);

        var text = "Tok usluge:\n\n" +
                   string.Join(
                       "\n",
                       visibleSteps.Select(x =>
                           $"{x.StartMinute}-{x.EndMinute} min: {NormalizeCustomerTimelineTitle(x.Title)}"));

        return Ok(new CustomerAppointmentTimelineDto
        {
            AppointmentId = appointment.Id,
            Text = text,
            Steps = visibleSteps
        });
    }

    [HttpGet("appointments")]
    [ProducesResponseType(typeof(List<CustomerPortalAppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CustomerPortalAppointmentDto>>> Appointments(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Ok(new List<CustomerPortalAppointmentDto>());

        var businessCustomer = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        if (businessCustomer is null)
            return Ok(new List<CustomerPortalAppointmentDto>());

        var items = await (
            from appointment in _dbContext.Appointments.AsNoTracking()
            join business in _dbContext.Businesses.AsNoTracking()
                on appointment.BusinessId equals business.Id
            join service in _dbContext.Services.AsNoTracking()
                on appointment.ServiceId equals service.Id into serviceJoin
            from service in serviceJoin.DefaultIfEmpty()
            join staff in _dbContext.StaffMembers.AsNoTracking()
                on appointment.PrimaryStaffMemberId equals staff.Id into staffJoin
            from staff in staffJoin.DefaultIfEmpty()
            where appointment.BusinessId == businessId &&
                  appointment.BusinessCustomerId == businessCustomer.Id
            orderby appointment.StartAtUtc descending
            select new CustomerPortalAppointmentDto
            {
                Id = appointment.Id,
                BusinessId = business.Id,
                BusinessName = business.Name,
                BusinessCustomerId = businessCustomer.Id,
                ServiceId = appointment.ServiceId,
                ServiceName = service != null ? service.Name : null,
                PrimaryStaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staff != null ? staff.DisplayName : null,
                ResourceId = appointment.ResourceId,
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                Status = appointment.Status.ToString(),
                Notes = appointment.Notes
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    private async Task<ActionResult?> EnsureBusinessAccessAsync(
    long businessId,
    CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var hasAccess = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .AnyAsync(
                x => x.AppUserId == userId.Value &&
                     x.BusinessId == businessId &&
                     x.IsActive,
                cancellationToken);

        if (!hasAccess)
            return Forbid();

        return null;
    }
    private static string NormalizeSearchText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value
            .Trim()
            .ToLowerInvariant()
            .Replace("đ", "dj")
            .Replace("Đ", "dj");

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);

            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }


    private static double? CalculateDistanceKm(
    double? fromLatitude,
    double? fromLongitude,
    double? toLatitude,
    double? toLongitude)
    {
        if (!fromLatitude.HasValue ||
            !fromLongitude.HasValue ||
            !toLatitude.HasValue ||
            !toLongitude.HasValue)
        {
            return null;
        }

        const double earthRadiusKm = 6371.0;

        var lat1 = DegreesToRadians(fromLatitude.Value);
        var lon1 = DegreesToRadians(fromLongitude.Value);
        var lat2 = DegreesToRadians(toLatitude.Value);
        var lon2 = DegreesToRadians(toLongitude.Value);

        var deltaLat = lat2 - lat1;
        var deltaLon = lon2 - lon1;

        var a =
            Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
            Math.Cos(lat1) * Math.Cos(lat2) *
            Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(earthRadiusKm * c, 2);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static List<CustomerAppointmentTimelineStepDto> MergeTimelineStepsForCustomer(
    List<CustomerAppointmentTimelineStepDto> steps)
    {
        var result = new List<CustomerAppointmentTimelineStepDto>();

        foreach (var group in steps
                     .Where(x => x.DurationMin > 0)
                     .GroupBy(x => new
                     {
                         x.StartMinute,
                         x.DurationMin
                     })
                     .OrderBy(x => x.Key.StartMinute)
                     .ThenBy(x => x.Key.DurationMin))
        {
            var titles = group
                .Select(x => NormalizeCustomerTimelineTitle(x.Title))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => !x.Equals("Priprema / rad sa klijentom", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (titles.Count == 0)
                continue;

            result.Add(new CustomerAppointmentTimelineStepDto
            {
                StartMinute = group.Key.StartMinute,
                DurationMin = group.Key.DurationMin,
                Title = string.Join(" / ", titles)
            });
        }

        return result;
    }

    private static string NormalizeCustomerTimelineTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var value = title.Trim();

        if (value.Contains("stolica", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return value;
    }
    private long? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private sealed class CustomerBusinessConnectionRow
    {
        public long BusinessId { get; set; }

        public long BusinessCustomerId { get; set; }
    }
}