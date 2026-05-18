using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
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
public sealed class BusinessesController : ApiControllerBase
{
    public BusinessesController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<BusinessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BusinessDto>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var accessibleBusinessIds = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value && x.IsActive)
            .Select(x => x.BusinessId)
            .ToListAsync(cancellationToken);

        var businesses = await DbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .Where(x => accessibleBusinessIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var items = businesses
            .Select(ToDto)
            .ToList();

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        return Ok(ToDto(entity));
    }

    [HttpPost]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessDto>> Create(
        [FromBody] CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var userExists = await DbContext.AppUsers
            .AnyAsync(x => x.Id == userId.Value && x.IsActive, cancellationToken);

        if (!userExists)
            return BuildUnauthorized();

        var validationResult = ValidateBusinessRequest(
            request.Name,
            request.BusinessType,
            request.BookingMode,
            request.SlotIntervalMin);

        if (validationResult is not null)
            return validationResult;

        var now = DateTime.UtcNow;

        var entity = new Business
        {
            Name = request.Name.Trim(),
            BusinessType = (BusinessType)request.BusinessType,
            BookingMode = (BookingMode)request.BookingMode,
            Description = request.Description?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Street = request.Street?.Trim(),
            StreetNumber = request.StreetNumber?.Trim(),
            City = request.City?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = request.Country?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GooglePlaceId = request.GooglePlaceId?.Trim(),
            SlotIntervalMin = request.SlotIntervalMin,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            FeatureSettings = CreateFeatureSettings(request.FeatureSettings, (BookingMode)request.BookingMode)
        };

        DbContext.Businesses.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        DbContext.BusinessUserMemberships.Add(new BusinessUserMembership
        {
            AppUserId = userId.Value,
            BusinessId = entity.Id,
            Role = BusinessUserRole.Owner,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await DbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        var validationResult = ValidateBusinessRequest(
            request.Name,
            request.BusinessType,
            request.BookingMode,
            request.SlotIntervalMin);

        if (validationResult is not null)
            return validationResult;

        entity.Name = request.Name.Trim();
        entity.BusinessType = (BusinessType)request.BusinessType;
        entity.BookingMode = (BookingMode)request.BookingMode;
        entity.Description = request.Description?.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.Email = request.Email?.Trim();
        entity.Street = request.Street?.Trim();
        entity.StreetNumber = request.StreetNumber?.Trim();
        entity.City = request.City?.Trim();
        entity.PostalCode = request.PostalCode?.Trim();
        entity.Country = request.Country?.Trim();
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.GooglePlaceId = request.GooglePlaceId?.Trim();
        entity.SlotIntervalMin = request.SlotIntervalMin;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        if (entity.FeatureSettings is null)
        {
            entity.FeatureSettings = CreateFeatureSettings(request.FeatureSettings, entity.BookingMode);
        }
        else
        {
            ApplyFeatureSettings(entity.FeatureSettings, request.FeatureSettings, entity.BookingMode);
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/deactivate")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> Deactivate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (!entity.IsActive)
            return BadRequest("Radnja je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{id:long}/activate")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> Activate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (entity.IsActive)
            return BadRequest("Radnja je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    private ActionResult? ValidateBusinessRequest(
        string name,
        int businessType,
        int bookingMode,
        int slotIntervalMin)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv radnje.");

        if (!Enum.IsDefined(typeof(BusinessType), businessType))
            return BadRequest("Izabrana vrsta radnje nije ispravna.");

        if (!Enum.IsDefined(typeof(BookingMode), bookingMode))
            return BadRequest("Izabrani režim rada nije ispravan.");

        if (slotIntervalMin <= 0)
            return BadRequest("Razmak između početaka termina mora biti veći od 0 minuta.");

        if (slotIntervalMin > 180)
            return BadRequest("Razmak između početaka termina je prevelik.");

        return null;
    }

    private static BusinessFeatureSettings CreateFeatureSettings(
        BusinessFeatureSettingsDto? dto,
        BookingMode bookingMode)
    {
        var settings = new BusinessFeatureSettings();

        ApplyFeatureSettings(settings, dto, bookingMode);

        return settings;
    }

    private static void ApplyFeatureSettings(
        BusinessFeatureSettings settings,
        BusinessFeatureSettingsDto? dto,
        BookingMode bookingMode)
    {
        var effectiveDto = dto ?? CreateDefaultFeatureSettingsDto(bookingMode);

        settings.ServiceAppointmentsEnabled = effectiveDto.ServiceAppointmentsEnabled;
        settings.TableReservationsEnabled = effectiveDto.TableReservationsEnabled;
        settings.FoodOrdersEnabled = effectiveDto.FoodOrdersEnabled;
        settings.DrinkOrdersEnabled = effectiveDto.DrinkOrdersEnabled;
        settings.TakeawayOrdersEnabled = effectiveDto.TakeawayOrdersEnabled;
        settings.DeliveryOrdersEnabled = effectiveDto.DeliveryOrdersEnabled;
        settings.EventHallReservationsEnabled = effectiveDto.EventHallReservationsEnabled;
        settings.AccommodationEnabled = effectiveDto.AccommodationEnabled;
        settings.ReviewsEnabled = effectiveDto.ReviewsEnabled;
    }

    private static BusinessDto ToDto(Business entity)
    {
        return new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            BookingMode = (int)entity.BookingMode,
            FeatureSettings = ToFeatureSettingsDto(entity.FeatureSettings, entity.BookingMode),
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            Street = entity.Street,
            StreetNumber = entity.StreetNumber,
            City = entity.City,
            PostalCode = entity.PostalCode,
            Country = entity.Country,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            GooglePlaceId = entity.GooglePlaceId,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        };
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
}