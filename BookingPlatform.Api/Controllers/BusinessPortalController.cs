using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Contracts.BusinessPortal;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
public sealed class BusinessPortalController : ControllerBase
{
    private readonly BookingDbContext _dbContext;

    public BusinessPortalController(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(BusinessPortalMeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BusinessPortalMeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return Unauthorized("Korisnik ne postoji.");

        var businessCount = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .CountAsync(
                x => x.AppUserId == user.Id && x.IsActive,
                cancellationToken);

        return Ok(new BusinessPortalMeResponse
        {
            AppUserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            HasBusinessAccess = businessCount > 0,
            BusinessCount = businessCount
        });
    }

    [HttpGet("businesses")]
    [ProducesResponseType(typeof(List<BusinessPortalBusinessDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BusinessPortalBusinessDto>>> Businesses(
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var memberships = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value && x.IsActive)
            .Select(x => new
            {
                x.BusinessId,
                Role = x.Role.ToString()
            })
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
            return Ok(new List<BusinessPortalBusinessDto>());

        var roleByBusinessId = memberships
            .ToDictionary(x => x.BusinessId, x => x.Role);

        var businessIds = memberships
            .Select(x => x.BusinessId)
            .ToList();

        var businesses = await _dbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .Where(x => businessIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var items = businesses
            .Select(business => new BusinessPortalBusinessDto
            {
                BusinessId = business.Id,
                BusinessName = business.Name,
                BusinessType = (int)business.BusinessType,
                BookingMode = (int)business.BookingMode,
                FeatureSettings = ToFeatureSettingsDto(business.FeatureSettings, business.BookingMode),
                Role = roleByBusinessId.TryGetValue(business.Id, out var role) ? role : string.Empty,
                IsActive = business.IsActive,
                Phone = business.Phone,
                Email = business.Email,
                City = business.City
            })
            .ToList();

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

        var accessResult = await EnsureBusinessAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await _dbContext.StaffMembers
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
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

    [HttpGet("appointments")]
    [ProducesResponseType(typeof(List<BusinessPortalAppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<BusinessPortalAppointmentDto>>> Appointments(
       [FromQuery] long businessId,
       [FromQuery] long? staffMemberId,
       [FromQuery] DateTime? date,
       CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (staffMemberId.HasValue)
        {
            var staffExists = await _dbContext.StaffMembers
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == staffMemberId.Value &&
                         x.BusinessId == businessId,
                    cancellationToken);

            if (!staffExists)
                return BadRequest("Izabrani radnik ne postoji u ovom biznisu.");
        }

        DateTime? day = null;
        DateTime? nextDay = null;

        if (date.HasValue)
        {
            day = DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Utc);
            nextDay = day.Value.AddDays(1);
        }

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
            where appointment.BusinessId == businessId
                  && (!staffMemberId.HasValue || appointment.PrimaryStaffMemberId == staffMemberId.Value)
                  && (!day.HasValue || appointment.StartAtUtc >= day.Value)
                  && (!nextDay.HasValue || appointment.StartAtUtc < nextDay.Value)
            orderby appointment.StartAtUtc
            select new BusinessPortalAppointmentDto
            {
                Id = appointment.Id,
                BusinessId = business.Id,
                BusinessName = business.Name,
                ServiceId = appointment.ServiceId,
                ServiceName = service != null ? service.Name : null,
                PrimaryStaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staff != null ? staff.DisplayName : null,
                ResourceId = appointment.ResourceId,
                BusinessCustomerId = appointment.BusinessCustomerId,
                CustomerName = appointment.CustomerName,
                CustomerPhone = appointment.CustomerPhone,
                CustomerEmail = appointment.CustomerEmail,
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

    private static BusinessFeatureSettingsDto ToFeatureSettingsDto(
        BusinessFeatureSettings? settings,
        BookingMode bookingMode)
    {
        if (settings is null)
            return CreateDefaultFeatureSettingsDto(bookingMode);

        return new BusinessFeatureSettingsDto
        {
            ServiceAppointmentsEnabled = settings.ServiceAppointmentsEnabled,
            TableReservationsEnabled = settings.TableReservationsEnabled,
            FoodOrdersEnabled = settings.FoodOrdersEnabled,
            DrinkOrdersEnabled = settings.DrinkOrdersEnabled,
            TakeawayOrdersEnabled = settings.TakeawayOrdersEnabled,
            DeliveryOrdersEnabled = settings.DeliveryOrdersEnabled,
            EventHallReservationsEnabled = settings.EventHallReservationsEnabled,
            AccommodationEnabled = settings.AccommodationEnabled,
            ReviewsEnabled = settings.ReviewsEnabled
        };
    }

    private static BusinessFeatureSettingsDto CreateDefaultFeatureSettingsDto(BookingMode bookingMode)
    {
        return bookingMode switch
        {
            BookingMode.Hospitality => new BusinessFeatureSettingsDto
            {
                ServiceAppointmentsEnabled = false,
                TableReservationsEnabled = true,
                FoodOrdersEnabled = true,
                DrinkOrdersEnabled = true,
                TakeawayOrdersEnabled = false,
                DeliveryOrdersEnabled = false,
                EventHallReservationsEnabled = false,
                AccommodationEnabled = false,
                ReviewsEnabled = true
            },

            BookingMode.Accommodation => new BusinessFeatureSettingsDto
            {
                ServiceAppointmentsEnabled = false,
                TableReservationsEnabled = false,
                FoodOrdersEnabled = false,
                DrinkOrdersEnabled = false,
                TakeawayOrdersEnabled = false,
                DeliveryOrdersEnabled = false,
                EventHallReservationsEnabled = false,
                AccommodationEnabled = true,
                ReviewsEnabled = true
            },

            _ => new BusinessFeatureSettingsDto
            {
                ServiceAppointmentsEnabled = true,
                TableReservationsEnabled = false,
                FoodOrdersEnabled = false,
                DrinkOrdersEnabled = false,
                TakeawayOrdersEnabled = false,
                DeliveryOrdersEnabled = false,
                EventHallReservationsEnabled = false,
                AccommodationEnabled = false,
                ReviewsEnabled = true
            }
        };
    }

    private long? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(raw, out var userId) ? userId : null;
    }
}