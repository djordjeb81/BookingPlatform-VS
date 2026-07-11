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
using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Chat;
using BookingPlatform.Api.Hubs;
using BookingPlatform.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
public sealed class CustomerPortalController : ControllerBase
{
    private const int ImmediateOrderDefaultOffsetMinutes = 1;
    private const int ScheduledOrderMinimumLeadTimeMin = 5;

    private readonly BookingDbContext _dbContext;
    private readonly ISystemAlarmService _systemAlarmService;
    private readonly IHubContext<BusinessActivityHub> _businessActivityHub;

    public CustomerPortalController(
        BookingDbContext dbContext,
        ISystemAlarmService systemAlarmService,
        IHubContext<BusinessActivityHub> businessActivityHub)
    {
        _dbContext = dbContext;
        _systemAlarmService = systemAlarmService;
        _businessActivityHub = businessActivityHub;
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
            Phone = profile?.Phone,
            Nickname = profile?.Nickname,
            AvatarUrl = profile?.AvatarUrl,
            DisplayName = BuildCustomerDisplayName(profile, user),
            AllowUserSearch = profile?.AllowUserSearch ?? false,
            AllowChatDiscovery = profile?.AllowChatDiscovery ?? false,
            DefaultDeliveryAddress = profile?.DefaultDeliveryAddress,
            DefaultDeliveryCity = profile?.DefaultDeliveryCity,
            DefaultDeliveryStreet = profile?.DefaultDeliveryStreet,
            DefaultDeliveryStreetNumber = profile?.DefaultDeliveryStreetNumber,
            DefaultDeliveryApartment = profile?.DefaultDeliveryApartment,
            DefaultDeliveryNote = profile?.DefaultDeliveryNote,
            DefaultDeliveryLatitude = profile?.DefaultDeliveryLatitude,
            DefaultDeliveryLongitude = profile?.DefaultDeliveryLongitude
        });
    }

    [HttpPut("me/profile")]
    [ProducesResponseType(typeof(CustomerPortalMeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CustomerPortalMeResponse>> UpdateProfile(
        [FromBody] CustomerPortalUpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var user = await _dbContext.AppUsers
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return Unauthorized("Korisnik ne postoji.");

        if (request.DefaultDeliveryLatitude is < -90 or > 90)
            return BadRequest("Latitude nije validan.");

        if (request.DefaultDeliveryLongitude is < -180 or > 180)
            return BadRequest("Longitude nije validan.");

        var now = DateTime.UtcNow;
        var fullName = NormalizeText(request.FullName, 200)
            ?? NormalizeText(user.FullName, 200)
            ?? NormalizeText(user.Email, 200)
            ?? "Klijent";

        var profile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);

        if (profile is null)
        {
            profile = new BookingPlatform.Domain.Customers.CustomerProfile
            {
                AppUserId = user.Id,
                Email = user.Email,
                CreatedAtUtc = now
            };

            _dbContext.CustomerProfiles.Add(profile);
        }

        profile.FullName = fullName;
        profile.Phone = NormalizeText(request.Phone, 50);
        profile.Nickname = NormalizeText(request.Nickname, 80);
        profile.AllowUserSearch = request.AllowUserSearch;
        profile.AllowChatDiscovery = request.AllowChatDiscovery;
        profile.DefaultDeliveryCity = NormalizeText(request.DefaultDeliveryCity, 120);
        profile.DefaultDeliveryStreet = NormalizeText(request.DefaultDeliveryStreet, 200);
        profile.DefaultDeliveryStreetNumber = NormalizeText(request.DefaultDeliveryStreetNumber, 40);
        profile.DefaultDeliveryApartment = NormalizeText(request.DefaultDeliveryApartment, 120);
        profile.DefaultDeliveryNote = NormalizeText(request.DefaultDeliveryNote, 500);
        profile.DefaultDeliveryAddress =
            NormalizeText(request.DefaultDeliveryAddress, 500) ??
            BuildDefaultDeliveryAddress(request);
        profile.DefaultDeliveryLatitude = request.DefaultDeliveryLatitude;
        profile.DefaultDeliveryLongitude = request.DefaultDeliveryLongitude;
        profile.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CustomerPortalMeResponse
        {
            AppUserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            HasCustomerProfile = true,
            CustomerProfileId = profile.Id,
            CustomerName = profile.FullName,
            Phone = profile.Phone,
            Nickname = profile.Nickname,
            AvatarUrl = profile.AvatarUrl,
            DisplayName = BuildCustomerDisplayName(profile, user),
            AllowUserSearch = profile.AllowUserSearch,
            AllowChatDiscovery = profile.AllowChatDiscovery,
            DefaultDeliveryAddress = profile.DefaultDeliveryAddress,
            DefaultDeliveryCity = profile.DefaultDeliveryCity,
            DefaultDeliveryStreet = profile.DefaultDeliveryStreet,
            DefaultDeliveryStreetNumber = profile.DefaultDeliveryStreetNumber,
            DefaultDeliveryApartment = profile.DefaultDeliveryApartment,
            DefaultDeliveryNote = profile.DefaultDeliveryNote,
            DefaultDeliveryLatitude = profile.DefaultDeliveryLatitude,
            DefaultDeliveryLongitude = profile.DefaultDeliveryLongitude
        });
    }

    [HttpGet("users/search")]
    [ProducesResponseType(typeof(List<CustomerUserSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CustomerUserSearchResultDto>>> SearchUsers(
        [FromQuery] string? q,
        CancellationToken cancellationToken,
        [FromQuery] bool forChat = false)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var query = NormalizeText(q, 100);

        if (query is null || query.Length < 2)
            return BadRequest("Unesite najmanje 2 karaktera za pretragu.");

        var normalizedQuery = NormalizeSearchText(query);
        var queryPhoneDigits = NormalizePhoneDigits(query);
        var phoneSearchVariants = BuildPhoneSearchVariants(queryPhoneDigits);

        var candidates = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .Where(x =>
                x.AppUserId != userId.Value &&
                (forChat ? x.AllowChatDiscovery && x.AppUserId.HasValue : x.AllowUserSearch) &&
                (
                    (x.Nickname != null && EF.Functions.ILike(x.Nickname, $"%{query}%")) ||
                    EF.Functions.ILike(x.FullName, $"%{query}%") ||
                    (x.Email != null && EF.Functions.ILike(x.Email, $"%{query}%")) ||
                    (x.Phone != null && EF.Functions.ILike(x.Phone, $"%{query}%")) ||
                    (queryPhoneDigits.Length >= 3 && x.Phone != null)
                ))
            .OrderBy(x => x.Nickname ?? x.FullName)
            .Take(100)
            .ToListAsync(cancellationToken);

        var filtered = candidates
            .Where(x =>
                ContainsNormalized(x.Nickname, normalizedQuery) ||
                ContainsNormalized(x.FullName, normalizedQuery) ||
                ContainsNormalized(x.Email, normalizedQuery) ||
                ContainsNormalized(x.Phone, normalizedQuery) ||
                PhoneMatches(x.Phone, phoneSearchVariants))
            .Take(10)
            .Select(x => new CustomerUserSearchResultDto
            {
                CustomerProfileId = x.Id,
                AppUserId = x.AppUserId,
                DisplayName = BuildCustomerDisplayName(x),
                FullName = NormalizeText(x.FullName, 200),
                Nickname = NormalizeText(x.Nickname, 80),
                PhoneMasked = MaskPhone(x.Phone),
                EmailMasked = MaskEmail(x.Email),
                AvatarUrl = x.AvatarUrl
            })
            .ToList();

        return Ok(filtered);
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

        var customerLinks = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .Where(x => x.CustomerProfileId == profile.Id && x.IsActive)
            .Select(x => new
            {
                x.BusinessId,
                BusinessCustomerId = x.Id
            })
            .ToListAsync(cancellationToken);

        if (customerLinks.Count == 0)
            return Ok(new List<CustomerPortalBusinessDto>());

        var businessCustomerIdByBusinessId = customerLinks
            .ToDictionary(x => x.BusinessId, x => x.BusinessCustomerId);

        var businessIds = customerLinks
            .Select(x => x.BusinessId)
            .ToList();

        var businesses = await _dbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .Where(x => businessIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var items = businesses
            .Select(business => new CustomerPortalBusinessDto
            {
                BusinessId = business.Id,
                BusinessName = business.Name,
                BusinessCustomerId = businessCustomerIdByBusinessId[business.Id],
                CustomerProfileId = profile.Id,
                CustomerName = profile.FullName,
                BusinessType = (int)business.BusinessType,
                BookingMode = (int)business.BookingMode,
                FeatureSettings = ToFeatureSettingsDto(business.FeatureSettings, business.BookingMode),
                BusinessPhone = business.Phone,
                BusinessEmail = business.Email,
                City = business.City
            })
            .ToList();

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
            .Include(x => x.FeatureSettings)
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
                BookingMode = (int)x.BookingMode,
                x.FeatureSettings,
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
                    BookingMode = x.BookingMode,
                    FeatureSettings = ToFeatureSettingsDto(x.FeatureSettings, (BookingMode)x.BookingMode),
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
            .Include(x => x.FeatureSettings)
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
                BookingMode = (int)x.BookingMode,
                x.FeatureSettings,
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

        var workingHours = await _dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DayOfWeek)
            .Select(x => new CustomerBusinessWorkingHourDto
            {
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsClosed = x.IsClosed
            })
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
            BookingMode = business.BookingMode,
            FeatureSettings = ToFeatureSettingsDto(
                business.FeatureSettings,
                (BookingMode)business.BookingMode),
            Latitude = latitude,
            Longitude = longitude,
            IsAlreadyConnected = businessCustomerId.HasValue,
            BusinessCustomerId = businessCustomerId,
            Services = services,
            WorkingHours = workingHours
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

    [HttpGet("restaurants/{businessId:long}/profile")]
    [ProducesResponseType(typeof(CustomerRestaurantProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerRestaurantProfileDto>> RestaurantProfile(
    [FromRoute] long businessId,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var business = await _dbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(
                x => x.Id == businessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return NotFound("Restoran ne postoji ili nije aktivan.");

        if (business.BusinessType is not (BusinessType.Restaurant or BusinessType.Cafe or BusinessType.FastFood))
            return BadRequest("Izabrani biznis nije restoran, kafić ili brza hrana.");

        var workingHours = await _dbContext.BusinessWorkingHours
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DayOfWeek)
            .Select(x => new CustomerRestaurantWorkingHourDto
            {
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime.ToString(@"hh\:mm"),
                EndTime = x.EndTime.ToString(@"hh\:mm"),
                IsClosed = x.IsClosed
            })
            .ToListAsync(cancellationToken);

        var operationUnits = await _dbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Include(x => x.WorkingHours)
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var categories = await _dbContext.RestaurantMenuCategories
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.OptionGroups)
                    .ThenInclude(x => x.Options)
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var addonGroups = await _dbContext.RestaurantAddonGroups
            .AsNoTracking()
            .Include(x => x.Addons)
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var restaurantSettings = await _dbContext.RestaurantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken)
            ?? CreateDefaultRestaurantSettings(businessId);

        var deliveryZones = await _dbContext.RestaurantDeliveryZones
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .Select(x => ToDeliveryZoneDto(x))
            .ToListAsync(cancellationToken);

        var latitude = business.Latitude.HasValue
            ? (double?)business.Latitude.Value
            : null;

        var longitude = business.Longitude.HasValue
            ? (double?)business.Longitude.Value
            : null;

        return Ok(new CustomerRestaurantProfileDto
        {
            BusinessId = business.Id,
            BusinessName = business.Name,
            Description = business.Description,
            BusinessPhone = business.Phone,
            BusinessEmail = business.Email,
            Street = business.Street,
            StreetNumber = business.StreetNumber,
            City = business.City,
            PostalCode = business.PostalCode,
            Country = business.Country,
            BusinessType = (int)business.BusinessType,
            BookingMode = (int)business.BookingMode,
            FeatureSettings = ToFeatureSettingsDto(business.FeatureSettings, business.BookingMode),
            RestaurantSettings = ToRestaurantSettingsDto(restaurantSettings),
            DeliveryZones = deliveryZones,
            Latitude = latitude,
            Longitude = longitude,
            WorkingHours = workingHours,
            OperationUnits = operationUnits
                .Select(ToCustomerRestaurantOperationUnitDto)
                .ToList(),
            MenuCategories = categories
                .Select(ToCustomerRestaurantMenuCategoryDto)
                .Where(x => x.Items.Count > 0)
                .ToList(),
            AddonGroups = addonGroups
                .Select(ToCustomerRestaurantAddonGroupDto)
                .Where(x => x.Addons.Count > 0)
                .ToList()
        });
    }

    [HttpPost("restaurants/{businessId:long}/orders")]
    [ProducesResponseType(typeof(RestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestaurantOrderDto>> CreateRestaurantOrder(
        [FromRoute] long businessId,
        [FromBody] CreateCustomerRestaurantOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("Podaci porudžbine su obavezni.");

        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        if (!Enum.IsDefined(typeof(RestaurantOrderType), request.OrderType))
            return BadRequest("Nepoznat tip porudžbine.");

        var orderType = (RestaurantOrderType)request.OrderType;

        if (orderType is not (RestaurantOrderType.Takeaway or RestaurantOrderType.Delivery))
            return BadRequest("Klijent može da pošalje samo porudžbinu za poneti ili dostavu.");

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest("Porudžbina mora imati bar jednu stavku.");

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

        if (profile is null)
            return Forbid();

        var businessCustomer = await _dbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        if (businessCustomer is null)
        {
            var nowForCustomer = DateTime.UtcNow;

            businessCustomer = new BookingPlatform.Domain.Customers.BusinessCustomer
            {
                BusinessId = businessId,
                CustomerProfileId = profile.Id,
                AppUserId = user.Id,
                FullName = NormalizeText(profile.FullName, 200)
                    ?? NormalizeText(user.FullName, 200)
                    ?? user.Email,
                Phone = NormalizeText(profile.Phone, 50),
                Email = NormalizeText(profile.Email, 256)
                    ?? user.Email,
                Notes = "Klijent je poslao porudžbinu preko Android aplikacije.",
                IsActive = true,
                CreatedAtUtc = nowForCustomer,
                UpdatedAtUtc = nowForCustomer
            };

            _dbContext.BusinessCustomers.Add(businessCustomer);
        }
        else if (!businessCustomer.IsActive)
        {
            businessCustomer.IsActive = true;
            businessCustomer.RemovedFromCustomerListAtUtc = null;
            businessCustomer.AppUserId = user.Id;
            businessCustomer.FullName = NormalizeText(profile.FullName, 200)
                ?? NormalizeText(user.FullName, 200)
                ?? user.Email;
            businessCustomer.Phone = NormalizeText(profile.Phone, 50);
            businessCustomer.Email = NormalizeText(profile.Email, 256)
                ?? user.Email;
            businessCustomer.UpdatedAtUtc = DateTime.UtcNow;
        }

        var business = await _dbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(
                x => x.Id == businessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return NotFound("Restoran ne postoji ili nije aktivan.");

        if (business.BusinessType is not (BusinessType.Restaurant or BusinessType.Cafe or BusinessType.FastFood))
            return BadRequest("Izabrani biznis nije restoran, kafić ili brza hrana.");

        var featureSettings = ToFeatureSettingsDto(business.FeatureSettings, business.BookingMode);

        if (!featureSettings.FoodOrdersEnabled && !featureSettings.DrinkOrdersEnabled)
            return BadRequest("Restoran trenutno ne prima porudžbine hrane ili pića.");

        if (orderType == RestaurantOrderType.Takeaway && !featureSettings.TakeawayOrdersEnabled)
            return BadRequest("Porudžbine za lično preuzimanje trenutno nisu uključene.");

        if (orderType == RestaurantOrderType.Delivery && !featureSettings.DeliveryOrdersEnabled)
            return BadRequest("Dostava trenutno nije uključena za ovaj restoran.");

        var restaurantSettings = await _dbContext.RestaurantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken)
            ?? CreateDefaultRestaurantSettings(businessId);

        if (orderType == RestaurantOrderType.Delivery)
        {
            if (!restaurantSettings.IsDeliveryEnabled)
                return BadRequest("Dostava trenutno nije uključena za ovaj restoran.");

            if (string.IsNullOrWhiteSpace(request.DeliveryAddress))
                return BadRequest("Za dostavu unesite adresu.");

            if (restaurantSettings.IsDeliveryLocationRequired &&
                (!request.DeliveryLatitude.HasValue || !request.DeliveryLongitude.HasValue))
            {
                return BadRequest("Za dostavu je obavezno poslati lokaciju.");
            }
        }

        var now = DateTime.UtcNow;
        DateTime requestedPickupUtc;

        if (request.IsScheduledOrder)
        {
            if (!request.RequestedPickupAtUtc.HasValue)
                return BadRequest("Za zakazanu porudžbinu izaberite datum i vreme.");

            requestedPickupUtc = EnsureUtc(request.RequestedPickupAtUtc.Value);

            if (!restaurantSettings.IsScheduledOrderingEnabled)
                return BadRequest("Zakazane porudžbine trenutno nisu uključene za ovaj restoran.");

            if (requestedPickupUtc < now.AddMinutes(ScheduledOrderMinimumLeadTimeMin))
            {
                return BadRequest(
                    $"Zakazana porudžbina mora biti najmanje {ScheduledOrderMinimumLeadTimeMin} minuta unapred.");
            }

            if (restaurantSettings.ScheduledOrderMaxDaysAhead > 0 &&
                requestedPickupUtc > now.AddDays(restaurantSettings.ScheduledOrderMaxDaysAhead))
            {
                return BadRequest(
                    $"Zakazana porudžbina može najviše {restaurantSettings.ScheduledOrderMaxDaysAhead} dana unapred.");
            }
        }
        else
        {
            requestedPickupUtc = now.AddMinutes(ImmediateOrderDefaultOffsetMinutes);
        }

        RestaurantDeliveryZone? deliveryZone = null;

        if (orderType == RestaurantOrderType.Delivery)
        {
            if (!request.DeliveryZoneId.HasValue || request.DeliveryZoneId.Value <= 0)
                return BadRequest("Za dostavu izaberite zonu dostave.");

            deliveryZone = await _dbContext.RestaurantDeliveryZones
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.DeliveryZoneId.Value &&
                    x.BusinessId == businessId &&
                    x.IsActive,
                    cancellationToken);

            if (deliveryZone is null)
                return BadRequest("Izabrana zona dostave ne postoji ili nije aktivna.");
        }

        var orderDateLocal = GetRestaurantOrderLocalDate(now);
        var dailyOrderNumber = await GetNextDailyOrderNumberAsync(
            businessId,
            orderDateLocal,
            cancellationToken);

        var customerName = NormalizeText(request.CustomerName, 200)
            ?? NormalizeText(profile.FullName, 200)
            ?? NormalizeText(user.FullName, 200)
            ?? user.Email;

        var customerPhone = NormalizeText(request.CustomerPhone, 50)
            ?? NormalizeText(profile.Phone, 50)
            ?? NormalizeText(businessCustomer.Phone, 50);

        var order = new RestaurantOrder
        {
            BusinessId = businessId,
            OrderDateLocal = orderDateLocal,
            DailyOrderNumber = dailyOrderNumber,
            OrderType = orderType,
            OrderSource = RestaurantOrderSource.AndroidCustomer,
            RequestedPickupAtUtc = requestedPickupUtc,
            IsScheduledOrder = request.IsScheduledOrder,
            DeliveryAddress = NormalizeText(request.DeliveryAddress, 500),
            DeliveryZoneId = deliveryZone?.Id,
            DeliveryLatitude = request.DeliveryLatitude,
            DeliveryLongitude = request.DeliveryLongitude,
            DeliveryZoneNameSnapshot = deliveryZone?.Name,
            DeliveryFeeAmount = deliveryZone?.DeliveryFeeAmount ?? 0m,
            DeliveryMinimumOrderAmountSnapshot = deliveryZone?.MinimumOrderAmount ?? 0m,
            DeliveryNote = NormalizeText(request.DeliveryNote, 1000),
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            Note = NormalizeText(request.Note, 1000),
            Status = RestaurantOrderStatus.Draft,
            SubtotalAmount = 0m,
            TotalAmount = 0m,
            Currency = "RSD",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.RestaurantOrders.Add(order);

        foreach (var requestItem in request.Items)
        {
            var itemResult = await BuildCustomerOrderItemAsync(
                order,
                requestItem,
                cancellationToken);

            if (itemResult.Error is not null)
                return BadRequest(itemResult.Error);

            order.Items.Add(itemResult.Item!);
        }

        RecalculateOrderTotals(order);

        if (order.OrderType == RestaurantOrderType.Delivery &&
            order.DeliveryMinimumOrderAmountSnapshot > 0 &&
            order.SubtotalAmount < order.DeliveryMinimumOrderAmountSnapshot)
        {
            var missingAmount = order.DeliveryMinimumOrderAmountSnapshot - order.SubtotalAmount;

            return BadRequest(
                $"Minimalna porudžbina za dostavu u zoni {order.DeliveryZoneNameSnapshot} je {order.DeliveryMinimumOrderAmountSnapshot:0.##} {order.Currency}. " +
                $"Dodajte još {missingAmount:0.##} {order.Currency} ili izaberite lično preuzimanje.");
        }

        if (order.IsScheduledOrder)
        {
            var maxPreparationTimeMin = await GetOrderMaxPreparationTimeMinAsync(order, cancellationToken);
            var earliestReadyUtc = now.AddMinutes(maxPreparationTimeMin + 1);

            if (requestedPickupUtc < earliestReadyUtc)
            {
                var earliestReadyLocal = earliestReadyUtc.ToLocalTime();

                return BadRequest(
                    $"Ne može za izabrano vreme. " +
                    $"Najduža priprema traje {maxPreparationTimeMin} min. " +
                    $"Može najbrže u {earliestReadyLocal:HH:mm}.");
            }
        }

        order.Status = RestaurantOrderStatus.Submitted;
        order.SubmittedAtUtc = now;
        order.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await AddCustomerOrderMessageAsync(
            order,
            "Klijent je poslao porudžbinu iz aplikacije.",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedOrder = await LoadCustomerRestaurantOrderAsync(
            order.Id,
            cancellationToken);

        return Ok(ToCustomerRestaurantOrderDto(savedOrder!));
    }

    [HttpPost("restaurant-orders/{orderId:long}/waiting-accepted")]
    [ProducesResponseType(typeof(RestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestaurantOrderDto>> AcceptRestaurantOrderWaiting(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadCustomerRestaurantOrderForActionAsync(
            orderId,
            cancellationToken);

        if (order is null)
            return NotFound("Porudžbina ne postoji.");

        var ownershipResult = await EnsureCurrentCustomerOwnsRestaurantOrderAsync(
            order,
            cancellationToken);

        if (ownershipResult is not null)
            return ownershipResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Čekanje može da se potvrdi samo za poslatu porudžbinu.");

        if (order.KitchenDecisionStatus != RestaurantKitchenDecisionStatus.AcceptedLater)
            return BadRequest("Kuhinja nije predložila čekanje za ovu porudžbinu.");

        var now = DateTime.UtcNow;
        var delayText = FormatDelayMinutes(order.KitchenAcceptLaterMinutes ?? 0);

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.WaitingAcceptedByCustomer;
        order.UpdatedAtUtc = now;

        await AddCustomerOrderMessageAsync(
            order,
            $"Klijent je prihvatio čekanje: {delayText}.",
            RestaurantOrderMessageType.CustomerAcceptedWaiting,
            cancellationToken);

        MarkRestaurantOrderWaitingChatActionsCompleted(order.Id, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _systemAlarmService.CancelRelatedRestaurantOrderAlarmsAsync(
            order.BusinessId,
            order.Id,
            cancellationToken);

        await NotifyCustomerRestaurantOrderBusinessSideAsync(
            order,
            "Porudžbina otkazana",
            $"Klijent je otkazao porudžbinu {FormatDisplayOrderNumber(order.DailyOrderNumber)} pre početka pripreme.",
            "RestaurantOrderCancelledByCustomer",
            "restaurant_order_cancelled_by_customer",
            cancellationToken);

        var savedOrder = await LoadCustomerRestaurantOrderAsync(order.Id, cancellationToken);

        return Ok(ToCustomerRestaurantOrderDto(savedOrder!));
    }

    [HttpGet("restaurant-orders")]
    [ProducesResponseType(typeof(List<RestaurantOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<RestaurantOrderDto>>> MyRestaurantOrders(
        [FromQuery] bool includePast = false,
        CancellationToken cancellationToken = default)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Ok(new List<RestaurantOrderDto>());

        var businessCustomers = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (
                    x.CustomerProfileId == profile.Id ||
                    x.AppUserId == userId.Value
                ))
            .ToListAsync(cancellationToken);

        var customerPhones = businessCustomers
            .Select(x => NormalizePhoneForScheduleMatch(x.Phone))
            .Append(NormalizePhoneForScheduleMatch(profile.Phone))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (customerPhones.Count == 0)
            return Ok(new List<RestaurantOrderDto>());

        var businessIds = businessCustomers
            .Select(x => x.BusinessId)
            .Distinct()
            .ToList();

        var query = _dbContext.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Guests)
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x =>
                x.OrderSource == RestaurantOrderSource.AndroidCustomer &&
                businessIds.Contains(x.BusinessId));

        if (!includePast)
        {
            query = query.Where(x =>
                x.Status != RestaurantOrderStatus.Ready &&
                x.Status != RestaurantOrderStatus.Served &&
                x.Status != RestaurantOrderStatus.Cancelled);
        }

        var orders = await query
            .OrderByDescending(x => x.RequestedPickupAtUtc ?? x.SubmittedAtUtc ?? x.CreatedAtUtc)
            .Take(includePast ? 100 : 50)
            .ToListAsync(cancellationToken);

        orders = orders
            .Where(x => customerPhones.Contains(NormalizePhoneForScheduleMatch(x.CustomerPhone)))
            .ToList();

        var orderBusinessIds = orders
            .Select(x => x.BusinessId)
            .Distinct()
            .ToList();

        var businessNames = await _dbContext.Businesses
            .AsNoTracking()
            .Where(x => orderBusinessIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return Ok(orders.Select(order =>
        {
            businessNames.TryGetValue(order.BusinessId, out var businessName);

            return ToCustomerRestaurantOrderDto(order, businessName);
        }).ToList());
    }

    [HttpPost("restaurant-orders/{orderId:long}/cancel")]
    [ProducesResponseType(typeof(RestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestaurantOrderDto>> CancelRestaurantOrder(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadCustomerRestaurantOrderForActionAsync(orderId, cancellationToken);

        if (order is null)
            return NotFound("Porudžbina ne postoji.");

        var ownershipResult = await EnsureCurrentCustomerOwnsRestaurantOrderAsync(
            order,
            cancellationToken);

        if (ownershipResult is not null)
            return ownershipResult;

        if (!CanCustomerCancelRestaurantOrder(order))
            return BadRequest("Porudžbina ne može da se otkaže jer je priprema počela ili je već završena.");

        var now = DateTime.UtcNow;

        order.Status = RestaurantOrderStatus.Cancelled;
        order.CompletedAtUtc = now;
        order.UpdatedAtUtc = now;

        AppendNote(order, "Klijent je otkazao porudžbinu pre početka pripreme.");

        await AddCustomerOrderMessageAsync(
            order,
            "Klijent je otkazao porudžbinu pre početka pripreme.",
            RestaurantOrderMessageType.OrderCancelled,
            cancellationToken);

        MarkRestaurantOrderWaitingChatActionsCompleted(order.Id, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedOrder = await LoadCustomerRestaurantOrderAsync(order.Id, cancellationToken);

        return Ok(ToCustomerRestaurantOrderDto(savedOrder!));
    }

    [HttpPut("restaurant-orders/{orderId:long}/items")]
    [ProducesResponseType(typeof(RestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestaurantOrderDto>> UpdateRestaurantOrderItems(
        [FromRoute] long orderId,
        [FromBody] UpdateCustomerRestaurantOrderItemsRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("Stavke porudžbine su obavezne.");

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest("Porudžbina mora imati bar jednu stavku.");

        var order = await LoadCustomerRestaurantOrderForActionAsync(orderId, cancellationToken);

        if (order is null)
            return NotFound("Porudžbina ne postoji.");

        var ownershipResult = await EnsureCurrentCustomerOwnsRestaurantOrderAsync(
            order,
            cancellationToken);

        if (ownershipResult is not null)
            return ownershipResult;

        if (!CanCustomerEditRestaurantOrder(order))
            return BadRequest("Porudžbina ne može da se izmeni jer je priprema počela ili je već završena.");

        var newItems = new List<RestaurantOrderItem>();

        foreach (var requestItem in request.Items)
        {
            var itemResult = await BuildCustomerOrderItemAsync(
                order,
                requestItem,
                cancellationToken);

            if (itemResult.Error is not null)
                return BadRequest(itemResult.Error);

            newItems.Add(itemResult.Item!);
        }

        _dbContext.RestaurantOrderItemOptions.RemoveRange(
            order.Items.SelectMany(x => x.Options));
        _dbContext.RestaurantOrderItems.RemoveRange(order.Items);

        order.Items.Clear();

        foreach (var item in newItems)
            order.Items.Add(item);

        var now = DateTime.UtcNow;

        RecalculateOrderTotals(order);
        order.UpdatedAtUtc = now;

        await AddCustomerOrderMessageAsync(
            order,
            "Klijent je izmenio stavke porudžbine pre početka pripreme.",
            RestaurantOrderMessageType.Text,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await NotifyCustomerRestaurantOrderBusinessSideAsync(
            order,
            "Porudžbina izmenjena",
            $"Klijent je izmenio stavke porudžbine {FormatDisplayOrderNumber(order.DailyOrderNumber)} pre početka pripreme.",
            "RestaurantOrderEditedByCustomer",
            "restaurant_order_edited_by_customer",
            cancellationToken);

        var savedOrder = await LoadCustomerRestaurantOrderAsync(order.Id, cancellationToken);

        return Ok(ToCustomerRestaurantOrderDto(savedOrder!));
    }

    [HttpPost("restaurant-orders/{orderId:long}/waiting-rejected")]
    [ProducesResponseType(typeof(RestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestaurantOrderDto>> RejectRestaurantOrderWaiting(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadCustomerRestaurantOrderForActionAsync(
            orderId,
            cancellationToken);

        if (order is null)
            return NotFound("Porudžbina ne postoji.");

        var ownershipResult = await EnsureCurrentCustomerOwnsRestaurantOrderAsync(
            order,
            cancellationToken);

        if (ownershipResult is not null)
            return ownershipResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Čekanje može da se odbije samo za poslatu porudžbinu.");

        if (order.KitchenDecisionStatus != RestaurantKitchenDecisionStatus.AcceptedLater)
            return BadRequest("Kuhinja nije predložila čekanje za ovu porudžbinu.");

        var now = DateTime.UtcNow;
        var delayText = FormatDelayMinutes(order.KitchenAcceptLaterMinutes ?? 0);

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.WaitingRejectedByCustomer;
        order.Status = RestaurantOrderStatus.Cancelled;
        order.CompletedAtUtc = now;
        order.UpdatedAtUtc = now;

        AppendNote(order, $"Klijent nije prihvatio čekanje: {delayText}.");

        await AddCustomerOrderMessageAsync(
            order,
            $"Klijent nije prihvatio čekanje: {delayText}. Porudžbina je otkazana.",
            RestaurantOrderMessageType.CustomerRejectedWaiting,
            cancellationToken);

        MarkRestaurantOrderWaitingChatActionsCompleted(order.Id, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var savedOrder = await LoadCustomerRestaurantOrderAsync(order.Id, cancellationToken);

        return Ok(ToCustomerRestaurantOrderDto(savedOrder!));
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

    [HttpPost("restaurants/{businessId:long}/table-reservation-requests")]
    [ProducesResponseType(typeof(CustomerPortalScheduleItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerPortalScheduleItemDto>> CreateRestaurantTableReservationRequest(
    [FromRoute] long businessId,
    [FromBody] CreateCustomerRestaurantTableReservationRequest request,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        if (request.RestaurantAreaId <= 0)
            return BadRequest("Izaberite salu.");

        if (request.PartySize <= 0)
            return BadRequest("Broj osoba mora biti veći od 0.");

        if (request.ExpectedDurationMin.HasValue && request.ExpectedDurationMin.Value <= 0)
            return BadRequest("Trajanje mora biti veće od 0 minuta.");

        var reservationAtUtc = EnsureUtc(request.ReservationAtUtc);

        if (reservationAtUtc < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest("Vreme zahteva ne može biti u prošlosti.");

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

        if (profile is null)
            return Forbid();

        var businessCustomer = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id &&
                     x.IsActive,
                cancellationToken);

        if (businessCustomer is null)
            return Forbid();

        var business = await _dbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(
                x => x.Id == businessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return NotFound("Restoran ne postoji ili nije aktivan.");

        var featureSettings = ToFeatureSettingsDto(
            business.FeatureSettings,
            business.BookingMode);

        if (!featureSettings.TableReservationsEnabled)
            return BadRequest("Ovaj restoran trenutno ne prima rezervacije stolova.");

        var areaExists = await _dbContext.RestaurantAreas
                    .AsNoTracking()
            .AnyAsync(
                x => x.Id == request.RestaurantAreaId &&
                     x.BusinessId == businessId &&
                     x.IsActive,
                cancellationToken);

        if (!areaExists)
            return BadRequest("Izabrana sala ne postoji ili nije aktivna.");

        string? tableName = null;

        if (request.TableResourceId.HasValue)
        {
            var table = await _dbContext.Resources
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == request.TableResourceId.Value &&
                         x.BusinessId == businessId &&
                         x.RestaurantAreaId == request.RestaurantAreaId &&
                         x.IsActive,
                    cancellationToken);

            if (table is null)
                return BadRequest("Izabrani sto ne postoji ili ne pripada izabranoj sali.");

            if (table.ResourceType != BookingPlatform.Domain.Resources.ResourceType.Table &&
                table.ResourceType != BookingPlatform.Domain.Resources.ResourceType.DiningTable)
                return BadRequest("Izabrani resurs nije sto.");

            if (table.Capacity.HasValue && request.PartySize > table.Capacity.Value)
                return BadRequest("Broj osoba je veći od kapaciteta izabranog stola.");

            tableName = table.Name;
        }

        var customerName = !string.IsNullOrWhiteSpace(profile.FullName)
            ? profile.FullName.Trim()
            : !string.IsNullOrWhiteSpace(user.FullName)
                ? user.FullName.Trim()
                : user.Email;

        var customerPhone = profile.Phone?.Trim();

        if (string.IsNullOrWhiteSpace(customerPhone))
            return BadRequest("U profilu nije unet telefon. Dodajte telefon pre slanja zahteva za sto.");

        var customerEmail = !string.IsNullOrWhiteSpace(profile.Email)
            ? profile.Email.Trim()
            : user.Email;

        var now = DateTime.UtcNow;

        var entity = new BookingPlatform.Domain.Restaurants.RestaurantTableReservation
        {
            BusinessId = businessId,
            RestaurantAreaId = request.RestaurantAreaId,
            TableResourceId = request.TableResourceId,

            CustomerProfileId = profile.Id,
            AppUserId = user.Id,
            BusinessCustomerId = businessCustomer.Id,

            PartySize = request.PartySize,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            CustomerEmail = customerEmail,

            ReservationAtUtc = reservationAtUtc,
            ExpectedDurationMin = request.ExpectedDurationMin.GetValueOrDefault(120),
            Status = BookingPlatform.Domain.Restaurants.RestaurantTableReservationStatus.PendingApproval,
            Note = NormalizeText(request.Note, 1000),
            InternalNote = "Zahtev poslat iz klijentske Android aplikacije.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.RestaurantTableReservations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var title = request.TableResourceId.HasValue
            ? $"Zahtev za sto • {tableName ?? "Sto"} • {request.PartySize} osoba"
            : $"Zahtev za sto • {request.PartySize} osoba";

        return Ok(new CustomerPortalScheduleItemDto
        {
            Id = entity.Id,
            ItemType = "RestaurantTableReservation",

            BusinessId = business.Id,
            BusinessName = business.Name,

            Title = title,
            Subtitle = "Restoran treba da potvrdi zahtev.",

            StartAtUtc = entity.ReservationAtUtc,
            EndAtUtc = entity.ReservationAtUtc.AddMinutes(entity.ExpectedDurationMin.GetValueOrDefault(120)),

            Status = entity.Status.ToString(),
            StatusText = GetCustomerScheduleStatusText(entity.Status.ToString()),

            AppointmentId = null,
            RestaurantTableReservationId = entity.Id,

            DetailsText = entity.Note
        });
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

    [HttpGet("me/schedule")]
    [ProducesResponseType(typeof(List<CustomerPortalScheduleItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<CustomerPortalScheduleItemDto>>> MySchedule(
        CancellationToken cancellationToken)
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
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Ok(new List<CustomerPortalScheduleItemDto>());

        var businessCustomerLinks = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .Where(x => x.CustomerProfileId == profile.Id)
            .Select(x => new
            {
                x.Id,
                x.BusinessId
            })
            .ToListAsync(cancellationToken);

        if (businessCustomerLinks.Count == 0)
            return Ok(new List<CustomerPortalScheduleItemDto>());

        var businessCustomerIds = businessCustomerLinks
            .Select(x => x.Id)
            .ToList();

        var connectedBusinessIds = businessCustomerLinks
            .Select(x => x.BusinessId)
            .Distinct()
            .ToList();

        var nowUtc = DateTime.UtcNow;

        var appointmentBaseQuery =
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
            select new CustomerPortalScheduleItemDto
            {
                Id = appointment.Id,
                ItemType = "Appointment",

                BusinessId = business.Id,
                BusinessName = business.Name,

                Title = service != null ? service.Name : "Termin",
                Subtitle = staff != null
                    ? $"Glavni radnik: {staff.DisplayName}"
                    : "Salon / termin",

                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,

                Status = appointment.Status.ToString(),
                StatusText = GetCustomerScheduleStatusText(appointment.Status.ToString()),

                AppointmentId = appointment.Id,
                RestaurantTableReservationId = null,

                DetailsText = appointment.Notes
            };

        var appointmentUpcoming = await appointmentBaseQuery
            .Where(x => x.StartAtUtc >= nowUtc)
            .OrderBy(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var appointmentPast = await appointmentBaseQuery
            .Where(x => x.StartAtUtc < nowUtc)
            .OrderByDescending(x => x.StartAtUtc)
            .ToListAsync(cancellationToken);

        var scheduleItems = appointmentUpcoming
            .Concat(appointmentPast)
            .ToList();

        var restaurantReservations = await (
            from reservation in _dbContext.RestaurantTableReservations.AsNoTracking()
            join business in _dbContext.Businesses.AsNoTracking()
                on reservation.BusinessId equals business.Id
            join table in _dbContext.Resources.AsNoTracking()
                on reservation.TableResourceId equals table.Id into tableJoin
            from table in tableJoin.DefaultIfEmpty()
            where
                (
                    reservation.CustomerProfileId == profile.Id ||
                    reservation.AppUserId == user.Id ||
                    (
                        reservation.BusinessCustomerId.HasValue &&
                        businessCustomerIds.Contains(reservation.BusinessCustomerId.Value)
                    )
                )
            select new
            {
                reservation.Id,
                reservation.BusinessId,
                BusinessName = business.Name,
                reservation.TableResourceId,
                TableName = table != null ? table.Name : null,
                reservation.PartySize,
                reservation.ReservationAtUtc,
                reservation.ExpectedDurationMin,
                reservation.Status,
                reservation.Note
            })
            .ToListAsync(cancellationToken);

        var restaurantScheduleItems = restaurantReservations
            .Select(x =>
            {
                var startAtUtc = EnsureUtc(x.ReservationAtUtc);
                var durationMin = x.ExpectedDurationMin.GetValueOrDefault(120);

                if (durationMin <= 0)
                    durationMin = 120;

                var endAtUtc = startAtUtc.AddMinutes(durationMin);

                var tableText = !string.IsNullOrWhiteSpace(x.TableName)
                    ? $" • {x.TableName}"
                    : string.Empty;

                return new CustomerPortalScheduleItemDto
                {
                    Id = x.Id,
                    ItemType = "RestaurantTableReservation",

                    BusinessId = x.BusinessId,
                    BusinessName = x.BusinessName,

                    Title = x.Status == RestaurantTableReservationStatus.PendingApproval
                        ? $"Zahtev za sto{tableText} • {x.PartySize} osoba"
                        : $"Rezervacija stola{tableText} • {x.PartySize} osoba",

                    Subtitle = x.Status == RestaurantTableReservationStatus.PendingApproval
                        ? "Restoran treba da potvrdi zahtev."
                        : "Restoran / rezervacija stola",

                    StartAtUtc = startAtUtc,
                    EndAtUtc = endAtUtc,

                    Status = x.Status.ToString(),
                    StatusText = GetCustomerScheduleStatusText(x.Status.ToString()),

                    AppointmentId = null,
                    RestaurantTableReservationId = x.Id,

                    DetailsText = x.Note
                };
            })
            .ToList();

        scheduleItems.AddRange(restaurantScheduleItems);

        var result = scheduleItems
            .OrderBy(x => x.StartAtUtc.HasValue && x.StartAtUtc.Value >= nowUtc ? 0 : 1)
            .ThenBy(x => x.StartAtUtc.HasValue && x.StartAtUtc.Value >= nowUtc
                ? x.StartAtUtc.Value
                : DateTime.MaxValue)
            .ThenByDescending(x => x.StartAtUtc.HasValue && x.StartAtUtc.Value < nowUtc
                ? x.StartAtUtc.Value
                : DateTime.MinValue)
            .ToList();

        return Ok(result);
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

    private static string GetCustomerScheduleStatusText(string? status)
    {
        return status switch
        {
            "PendingApproval" => "Čeka odobrenje",
            "Confirmed" => "Potvrđeno",
            "Rejected" => "Odbijeno",
            "Cancelled" => "Otkazano",
            "Completed" => "Završeno",
            "NoShow" => "Nije došao",
            "Arrived" => "Pristigli",
            _ => status ?? "Status nije unet"
        };
    }

    private static string NormalizePhoneForScheduleMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        if (digits.StartsWith("00381"))
            digits = "381" + digits[5..];

        if (digits.StartsWith("381"))
            return digits;

        if (digits.StartsWith("0") && digits.Length > 1)
            return "381" + digits[1..];

        return digits;
    }

    private static string NormalizeEmailForScheduleMatch(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }

    private async Task<CustomerOrderItemBuildResult> BuildCustomerOrderItemAsync(
        RestaurantOrder order,
        CreateCustomerRestaurantOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MenuItemId <= 0)
            return CustomerOrderItemBuildResult.Fail("menuItemId je obavezan.");

        if (request.Quantity <= 0)
            return CustomerOrderItemBuildResult.Fail("Količina mora biti veća od 0.");

        var menuItem = await _dbContext.RestaurantMenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.MenuItemId &&
                x.BusinessId == order.BusinessId &&
                x.IsActive &&
                x.IsAvailable,
                cancellationToken);

        if (menuItem is null)
            return CustomerOrderItemBuildResult.Fail("Artikal ne postoji ili trenutno nije dostupan.");

        var addons = request.Addons ?? new List<RestaurantOrderItemAddonSelectionDto>();

        var normalizedAddonSelections = addons
            .Where(x => x.AddonId > 0)
            .GroupBy(x => x.AddonId)
            .Select(x => x.Last())
            .ToList();

        foreach (var addonSelection in normalizedAddonSelections)
        {
            if (!Enum.IsDefined(typeof(RestaurantAddonAmountMode), addonSelection.AmountMode))
                return CustomerOrderItemBuildResult.Fail("Nepoznata mera dodatka.");
        }

        var addonIds = normalizedAddonSelections
            .Select(x => x.AddonId)
            .Distinct()
            .ToList();

        var selectedAddons = addonIds.Count == 0
            ? new List<RestaurantAddon>()
            : await _dbContext.RestaurantAddons
                .AsNoTracking()
                .Where(x =>
                    addonIds.Contains(x.Id) &&
                    x.BusinessId == order.BusinessId &&
                    x.IsActive &&
                    x.IsAvailable &&
                    x.AddonGroup.IsActive)
                .ToListAsync(cancellationToken);

        if (selectedAddons.Count != addonIds.Count)
            return CustomerOrderItemBuildResult.Fail("Jedan ili više dodataka nisu dostupni.");

        var addonById = selectedAddons.ToDictionary(x => x.Id);
        var addonTotal = selectedAddons.Sum(x => x.PriceDelta);
        var unitPrice = menuItem.Price + addonTotal;
        var now = DateTime.UtcNow;

        var orderItem = new RestaurantOrderItem
        {
            OrderId = order.Id,
            MenuItemId = menuItem.Id,
            MenuItemNameSnapshot = menuItem.Name,
            UnitPriceSnapshot = unitPrice,
            Quantity = request.Quantity,
            LineSubtotal = unitPrice * request.Quantity,
            SendToKitchenSnapshot = menuItem.SendToKitchen,
            Note = NormalizeText(request.Note, 1000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Options = normalizedAddonSelections
                .Select(selection =>
                {
                    var addon = addonById[selection.AddonId];

                    return new RestaurantOrderItemOption
                    {
                        RestaurantAddonId = addon.Id,
                        MenuItemOptionId = null,
                        OptionNameSnapshot = addon.Name,
                        PriceDeltaSnapshot = addon.PriceDelta,
                        AmountMode = (RestaurantAddonAmountMode)selection.AmountMode,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                })
                .ToList()
        };

        return CustomerOrderItemBuildResult.Ok(orderItem);
    }

    private async Task AddCustomerOrderMessageAsync(
        RestaurantOrder order,
        string text,
        RestaurantOrderMessageType messageType,
        CancellationToken cancellationToken)
    {
        var normalizedText = NormalizeText(text, 2000);

        if (string.IsNullOrWhiteSpace(normalizedText))
            return;

        var now = DateTime.UtcNow;

        var recipientOperationUnitIds = await _dbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == order.BusinessId &&
                x.IsActive &&
                x.ReceivesCustomerChat)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (recipientOperationUnitIds.Count == 0)
        {
            var kitchenOperationUnitId = await GetDefaultOperationUnitIdAsync(
                order.BusinessId,
                RestaurantOperationUnitType.Kitchen,
                cancellationToken);

            if (kitchenOperationUnitId.HasValue)
                recipientOperationUnitIds.Add(kitchenOperationUnitId.Value);
        }

        var message = new RestaurantOrderMessage
        {
            BusinessId = order.BusinessId,
            OrderId = order.Id,
            SenderType = RestaurantOrderMessageSenderType.Customer,
            SenderOperationUnitId = null,
            MessageType = messageType,
            Text = normalizedText,
            ActionKey = null,
            IsActionRequired = false,
            IsActionCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var recipientOperationUnitId in recipientOperationUnitIds.Distinct())
        {
            message.Recipients.Add(new RestaurantOrderMessageRecipient
            {
                BusinessId = order.BusinessId,
                RecipientType = RestaurantOrderMessageRecipientType.OperationUnit,
                RecipientOperationUnitId = recipientOperationUnitId,
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        if (message.Recipients.Count == 0)
        {
            message.Recipients.Add(new RestaurantOrderMessageRecipient
            {
                BusinessId = order.BusinessId,
                RecipientType = RestaurantOrderMessageRecipientType.Business,
                RecipientOperationUnitId = null,
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        _dbContext.RestaurantOrderMessages.Add(message);
    }

    private Task AddCustomerOrderMessageAsync(
        RestaurantOrder order,
        string text,
        CancellationToken cancellationToken)
    {
        return AddCustomerOrderMessageAsync(
            order,
            text,
            RestaurantOrderMessageType.OrderSubmitted,
            cancellationToken);
    }

    private async Task<long?> GetDefaultOperationUnitIdAsync(
        long businessId,
        RestaurantOperationUnitType unitType,
        CancellationToken cancellationToken)
    {
        return await _dbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.UnitType == unitType &&
                x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<RestaurantOrder?> LoadCustomerRestaurantOrderAsync(
        long orderId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Guests)
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    private async Task<RestaurantOrder?> LoadCustomerRestaurantOrderForActionAsync(
        long orderId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.RestaurantOrders
            .Include(x => x.Guests)
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    }

    private async Task<ActionResult?> EnsureCurrentCustomerOwnsRestaurantOrderAsync(
        RestaurantOrder order,
        CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        if (order.OrderSource != RestaurantOrderSource.AndroidCustomer)
            return Forbid();

        var profile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == userId.Value, cancellationToken);

        if (profile is null)
            return Forbid();

        var businessCustomer = await _dbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == order.BusinessId &&
                     x.IsActive &&
                     (
                         x.CustomerProfileId == profile.Id ||
                         x.AppUserId == userId.Value
                     ),
                cancellationToken);

        if (businessCustomer is null)
            return Forbid();

        var orderPhone = NormalizePhoneForScheduleMatch(order.CustomerPhone);
        var profilePhone = NormalizePhoneForScheduleMatch(profile.Phone);
        var businessCustomerPhone = NormalizePhoneForScheduleMatch(businessCustomer.Phone);

        var hasPhoneMatch =
            !string.IsNullOrWhiteSpace(orderPhone) &&
            (
                orderPhone == profilePhone ||
                orderPhone == businessCustomerPhone
            );

        if (!hasPhoneMatch)
            return Forbid();

        return null;
    }

    private void MarkRestaurantOrderWaitingChatActionsCompleted(
        long orderId,
        DateTime completedAtUtc)
    {
        var messages = _dbContext.ChatMessages
            .Where(x =>
                x.RestaurantOrderId == orderId &&
                x.ActionType == "RestaurantOrderWaitingProposal" &&
                !x.IsActionCompleted)
            .ToList();

        foreach (var message in messages)
        {
            message.IsActionCompleted = true;
            message.UpdatedAtUtc = completedAtUtc;
        }
    }

    private static bool CanCustomerCancelRestaurantOrder(RestaurantOrder order)
    {
        if (!CanCustomerEditRestaurantOrder(order))
            return false;

        return order.KitchenDecisionStatus is not
            (RestaurantKitchenDecisionStatus.Rejected or
            RestaurantKitchenDecisionStatus.WaitingRejectedByCustomer);
    }

    private static bool CanCustomerEditRestaurantOrder(RestaurantOrder order)
    {
        return order.Status == RestaurantOrderStatus.Submitted;
    }

    private async Task NotifyCustomerRestaurantOrderBusinessSideAsync(
        RestaurantOrder order,
        string title,
        string message,
        string activityType,
        string soundKey,
        CancellationToken cancellationToken)
    {
        await _systemAlarmService.CreateRestaurantOrderNotificationAlarmAsync(
            order.BusinessId,
            order.Id,
            title,
            message,
            soundKey,
            "open_restaurant_order",
            cancellationToken);

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(order.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = order.BusinessId,
                    orderId = order.Id,
                    tableResourceId = order.TableResourceId,
                    tableSessionId = order.TableSessionId,
                    restaurantAreaId = order.RestaurantAreaId,
                    orderType = (int)order.OrderType,
                    orderSource = (int)order.OrderSource,
                    orderSourceText = GetOrderSourceText(order.OrderSource),
                    status = (int)order.Status,
                    statusText = GetRestaurantOrderStatusText(order.Status),
                    activityType
                },
                cancellationToken);
    }

    private async Task<int> GetOrderMaxPreparationTimeMinAsync(
        RestaurantOrder order,
        CancellationToken cancellationToken)
    {
        var menuItemIds = order.Items
            .Select(x => x.MenuItemId)
            .Distinct()
            .ToList();

        if (menuItemIds.Count == 0)
            return 0;

        var preparationByMenuItemId = await _dbContext.RestaurantMenuItems
            .AsNoTracking()
            .Where(x => menuItemIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.PreparationTimeMin,
                cancellationToken);

        return order.Items
            .Where(x => x.SendToKitchenSnapshot)
            .Select(x =>
                preparationByMenuItemId.TryGetValue(x.MenuItemId, out var preparationTimeMin)
                    ? preparationTimeMin
                    : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private async Task<int> GetNextDailyOrderNumberAsync(
        long businessId,
        DateOnly orderDateLocal,
        CancellationToken cancellationToken)
    {
        var lastNumber = await _dbContext.RestaurantOrders
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.OrderDateLocal == orderDateLocal)
            .MaxAsync(x => (int?)x.DailyOrderNumber, cancellationToken);

        return (lastNumber ?? 0) + 1;
    }

    private static RestaurantSettings CreateDefaultRestaurantSettings(long businessId)
    {
        return new RestaurantSettings
        {
            BusinessId = businessId,
            PreparationReminderBufferMin = 10,
            ScheduledOrderMinLeadTimeMin = 30,
            ScheduledOrderMaxDaysAhead = 7,
            IsScheduledOrderingEnabled = true,
            IsDeliveryEnabled = true,
            IsDeliveryLocationRequired = false
        };
    }

    private static RestaurantSettingsDto ToRestaurantSettingsDto(RestaurantSettings entity)
    {
        return new RestaurantSettingsDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            PreparationReminderBufferMin = entity.PreparationReminderBufferMin,
            ScheduledOrderMinLeadTimeMin = entity.ScheduledOrderMinLeadTimeMin,
            ScheduledOrderMaxDaysAhead = entity.ScheduledOrderMaxDaysAhead,
            IsScheduledOrderingEnabled = entity.IsScheduledOrderingEnabled,
            IsDeliveryEnabled = entity.IsDeliveryEnabled,
            IsDeliveryLocationRequired = entity.IsDeliveryLocationRequired
        };
    }

    private static RestaurantDeliveryZoneDto ToDeliveryZoneDto(RestaurantDeliveryZone entity)
    {
        return new RestaurantDeliveryZoneDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            DeliveryFeeAmount = entity.DeliveryFeeAmount,
            MinimumOrderAmount = entity.MinimumOrderAmount,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder
        };
    }

    private static void RecalculateOrderTotals(RestaurantOrder order)
    {
        order.SubtotalAmount = order.Items.Sum(x => x.LineSubtotal);

        var deliveryFee = order.OrderType == RestaurantOrderType.Delivery
            ? order.DeliveryFeeAmount
            : 0m;

        order.TotalAmount = order.SubtotalAmount + deliveryFee;
        order.Currency = "RSD";
    }

    private static RestaurantOrderDto ToCustomerRestaurantOrderDto(
        RestaurantOrder entity,
        string? businessName = null)
    {
        return new RestaurantOrderDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            BusinessName = businessName ?? entity.Business?.Name ?? string.Empty,
            OrderDateLocal = entity.OrderDateLocal,
            DailyOrderNumber = entity.DailyOrderNumber,
            DisplayOrderNumberText = FormatDisplayOrderNumber(entity.DailyOrderNumber),
            RestaurantAreaId = entity.RestaurantAreaId,
            TableResourceId = entity.TableResourceId,
            TableSessionId = entity.TableSessionId,
            OrderType = (int)entity.OrderType,
            OrderTypeText = GetOrderTypeText(entity.OrderType),
            OrderSource = (int)entity.OrderSource,
            OrderSourceText = GetOrderSourceText(entity.OrderSource),
            RequestedPickupAtUtc = entity.RequestedPickupAtUtc,
            IsScheduledOrder = entity.IsScheduledOrder,
            DeliveryAddress = entity.DeliveryAddress,
            DeliveryNote = entity.DeliveryNote,
            DeliveryLatitude = entity.DeliveryLatitude,
            DeliveryLongitude = entity.DeliveryLongitude,
            DeliveryZoneId = entity.DeliveryZoneId,
            DeliveryZoneNameSnapshot = entity.DeliveryZoneNameSnapshot,
            DeliveryFeeAmount = entity.DeliveryFeeAmount,
            DeliveryMinimumOrderAmountSnapshot = entity.DeliveryMinimumOrderAmountSnapshot,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Note = entity.Note,
            Status = (int)entity.Status,
            StatusText = GetRestaurantOrderStatusText(entity.Status),
            KitchenDecisionStatus = (int)entity.KitchenDecisionStatus,
            KitchenDecisionStatusText = GetKitchenDecisionStatusText(entity.KitchenDecisionStatus),
            KitchenAcceptedAtUtc = entity.KitchenAcceptedAtUtc,
            KitchenAcceptLaterMinutes = entity.KitchenAcceptLaterMinutes,
            KitchenRejectedAtUtc = entity.KitchenRejectedAtUtc,
            KitchenRejectReason = entity.KitchenRejectReason,
            KitchenRejectNote = entity.KitchenRejectNote,
            SubtotalAmount = entity.SubtotalAmount,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            SubmittedAtUtc = entity.SubmittedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Guests = entity.Guests
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(guest => new RestaurantOrderGuestDto
                {
                    Id = guest.Id,
                    OrderId = guest.OrderId,
                    Name = guest.Name,
                    DisplayOrder = guest.DisplayOrder,
                    Note = guest.Note,
                    Items = entity.Items
                        .Where(item => item.OrderGuestId == guest.Id)
                        .OrderBy(item => item.Id)
                        .Select(item => ToCustomerRestaurantOrderItemDto(item, guest.Name))
                        .ToList()
                })
                .ToList(),
            Items = entity.Items
                .OrderBy(x => x.Id)
                .Select(item =>
                {
                    var guestName = entity.Guests
                        .FirstOrDefault(x => x.Id == item.OrderGuestId)
                        ?.Name;

                    return ToCustomerRestaurantOrderItemDto(item, guestName);
                })
                .ToList()
        };
    }

    private static RestaurantOrderItemDto ToCustomerRestaurantOrderItemDto(
        RestaurantOrderItem item,
        string? guestName = null)
    {
        return new RestaurantOrderItemDto
        {
            Id = item.Id,
            OrderId = item.OrderId,
            OrderGuestId = item.OrderGuestId,
            OrderGuestName = guestName,
            MenuItemId = item.MenuItemId,
            MenuItemNameSnapshot = item.MenuItemNameSnapshot,
            UnitPriceSnapshot = item.UnitPriceSnapshot,
            Quantity = item.Quantity,
            LineSubtotal = item.LineSubtotal,
            SendToKitchenSnapshot = item.SendToKitchenSnapshot,
            IsReady = item.IsReady,
            ReadyAtUtc = item.ReadyAtUtc,
            Note = item.Note,
            Options = item.Options
                .OrderBy(x => x.Id)
                .Select(option => new RestaurantOrderItemOptionDto
                {
                    Id = option.Id,
                    OrderItemId = option.OrderItemId,
                    MenuItemOptionId = option.MenuItemOptionId,
                    RestaurantAddonId = option.RestaurantAddonId,
                    OptionNameSnapshot = option.OptionNameSnapshot,
                    PriceDeltaSnapshot = option.PriceDeltaSnapshot,
                    AmountMode = (int)option.AmountMode,
                    AmountModeText = GetAddonAmountModeText(option.AmountMode)
                })
                .ToList()
        };
    }

    private static DateOnly GetRestaurantOrderLocalDate(DateTime utcNow)
    {
        var utc = EnsureUtc(utcNow);
        var timeZone = GetRestaurantTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);

        return DateOnly.FromDateTime(local);
    }

    private static TimeZoneInfo GetRestaurantTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        }
    }

    private static string FormatDisplayOrderNumber(int dailyOrderNumber)
    {
        return dailyOrderNumber <= 0
            ? "-"
            : $"#{dailyOrderNumber}";
    }

    private static string FormatDelayMinutes(int minutes)
    {
        if (minutes <= 0)
            return "bez dodatnog čekanja";

        if (minutes < 60)
            return $"{minutes} min";

        var hours = minutes / 60;
        var remainingMinutes = minutes % 60;

        return remainingMinutes == 0
            ? $"{hours} h"
            : $"{hours} h {remainingMinutes} min";
    }

    private static void AppendNote(RestaurantOrder order, string note)
    {
        var normalizedNote = NormalizeText(note, 1000);

        if (string.IsNullOrWhiteSpace(normalizedNote))
            return;

        order.Note = string.IsNullOrWhiteSpace(order.Note)
            ? normalizedNote
            : $"{order.Note}\n{normalizedNote}";
    }

    private static string GetAddonAmountModeText(RestaurantAddonAmountMode amountMode)
    {
        return amountMode switch
        {
            RestaurantAddonAmountMode.Less => "malo",
            RestaurantAddonAmountMode.More => "više",
            _ => "normalno"
        };
    }

    private static string GetOrderTypeText(RestaurantOrderType orderType)
    {
        return orderType switch
        {
            RestaurantOrderType.DineIn => "U lokalu",
            RestaurantOrderType.Takeaway => "Za poneti",
            RestaurantOrderType.Delivery => "Dostava",
            _ => "Nepoznat tip"
        };
    }

    private static string GetOrderSourceText(RestaurantOrderSource source)
    {
        return source switch
        {
            RestaurantOrderSource.RestaurantDesk => "Restoran",
            RestaurantOrderSource.KitchenDesk => "Kuhinja",
            RestaurantOrderSource.AndroidCustomer => "Android klijent",
            RestaurantOrderSource.WebCustomer => "Web klijent",
            RestaurantOrderSource.Admin => "Admin",
            RestaurantOrderSource.Other => "Ostalo",
            _ => source.ToString()
        };
    }

    private static string GetKitchenDecisionStatusText(RestaurantKitchenDecisionStatus status)
    {
        return status switch
        {
            RestaurantKitchenDecisionStatus.None => "Čeka odluku kuhinje",
            RestaurantKitchenDecisionStatus.Accepted => "Kuhinja prihvatila",
            RestaurantKitchenDecisionStatus.AcceptedLater => "Kuhinja predložila čekanje",
            RestaurantKitchenDecisionStatus.Rejected => "Kuhinja odbila",
            RestaurantKitchenDecisionStatus.WaitingAcceptedByCustomer => "Klijent prihvatio čekanje",
            RestaurantKitchenDecisionStatus.WaitingRejectedByCustomer => "Klijent odbio čekanje",
            _ => "Nepoznata odluka kuhinje"
        };
    }

    private static string GetRestaurantOrderStatusText(RestaurantOrderStatus status)
    {
        return status switch
        {
            RestaurantOrderStatus.Draft => "Nacrt",
            RestaurantOrderStatus.Submitted => "Poslato",
            RestaurantOrderStatus.Preparing => "U pripremi",
            RestaurantOrderStatus.Ready => "Spremno",
            RestaurantOrderStatus.Served => "Posluženo",
            RestaurantOrderStatus.Cancelled => "Otkazano",
            _ => "Nepoznat status"
        };
    }

    private long? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(raw, out var userId) ? userId : null;
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
            HasCustomerSeating = settings.HasCustomerSeating,
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
                TakeawayOrdersEnabled = true,
                DeliveryOrdersEnabled = false,
                HasCustomerSeating = true,
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
                HasCustomerSeating = false,
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
                HasCustomerSeating = false,
                EventHallReservationsEnabled = false,
                AccommodationEnabled = false,
                ReviewsEnabled = true
            }
        };
    }

    private static CustomerRestaurantOperationUnitDto ToCustomerRestaurantOperationUnitDto(RestaurantOperationUnit entity)
    {
        return new CustomerRestaurantOperationUnitDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            UnitType = (int)entity.UnitType,
            UnitTypeText = GetRestaurantOperationUnitTypeText(entity.UnitType),
            Name = entity.Name,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder,
            ReceivesCustomerChat = entity.ReceivesCustomerChat,
            WorkingHours = entity.WorkingHours
                .OrderBy(x => x.DayOfWeek)
                .Select(x => new CustomerRestaurantOperationUnitWorkingHourDto
                {
                    Id = x.Id,
                    OperationUnitId = x.OperationUnitId,
                    DayOfWeek = x.DayOfWeek,
                    StartTime = x.StartTime.ToString(@"hh\:mm"),
                    EndTime = x.EndTime.ToString(@"hh\:mm"),
                    IsClosed = x.IsClosed
                })
                .ToList()
        };
    }

    private static RestaurantMenuCategoryDto ToCustomerRestaurantMenuCategoryDto(RestaurantMenuCategory category)
    {
        return new RestaurantMenuCategoryDto
        {
            Id = category.Id,
            BusinessId = category.BusinessId,
            Name = category.Name,
            Description = category.Description,
            DisplayOrder = category.DisplayOrder,
            IsActive = category.IsActive,
            Items = category.Items
                .Where(x => x.IsActive && x.IsAvailable)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(x => new RestaurantMenuItemDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    CategoryId = x.CategoryId,
                    CategoryName = category.Name,
                    Name = x.Name,
                    Description = x.Description,
                    Price = x.Price,
                    Currency = x.Currency,
                    IsAvailable = x.IsAvailable,
                    SendToKitchen = x.SendToKitchen,
                    PreparationTimeMin = x.PreparationTimeMin,
                    IsActive = x.IsActive,
                    DisplayOrder = x.DisplayOrder,
                    OptionGroups = x.OptionGroups
                        .Where(group => group.IsActive)
                        .OrderBy(group => group.DisplayOrder)
                        .ThenBy(group => group.Name)
                        .Select(group => new RestaurantMenuItemOptionGroupDto
                        {
                            Id = group.Id,
                            MenuItemId = group.MenuItemId,
                            MenuItemName = x.Name,
                            Name = group.Name,
                            IsRequired = group.IsRequired,
                            MinSelected = group.MinSelected,
                            MaxSelected = group.MaxSelected,
                            DisplayOrder = group.DisplayOrder,
                            IsActive = group.IsActive,
                            Options = group.Options
                                .Where(option => option.IsActive && option.IsAvailable)
                                .OrderBy(option => option.DisplayOrder)
                                .ThenBy(option => option.Name)
                                .Select(option => new RestaurantMenuItemOptionDto
                                {
                                    Id = option.Id,
                                    OptionGroupId = option.OptionGroupId,
                                    Name = option.Name,
                                    PriceDelta = option.PriceDelta,
                                    IsAvailable = option.IsAvailable,
                                    IsActive = option.IsActive,
                                    DisplayOrder = option.DisplayOrder
                                })
                                .ToList()
                        })
                        .Where(group => group.Options.Count > 0)
                        .ToList()
                })
                .ToList()
        };
    }

    private static RestaurantAddonGroupDto ToCustomerRestaurantAddonGroupDto(RestaurantAddonGroup group)
    {
        return new RestaurantAddonGroupDto
        {
            Id = group.Id,
            BusinessId = group.BusinessId,
            Name = group.Name,
            DisplayOrder = group.DisplayOrder,
            IsActive = group.IsActive,
            Addons = group.Addons
                .Where(x => x.IsActive && x.IsAvailable)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(x => new RestaurantAddonDto
                {
                    Id = x.Id,
                    BusinessId = x.BusinessId,
                    AddonGroupId = x.AddonGroupId,
                    Name = x.Name,
                    PriceDelta = x.PriceDelta,
                    DisplayOrder = x.DisplayOrder,
                    IsActive = x.IsActive,
                    IsAvailable = x.IsAvailable
                })
                .ToList()
        };
    }

    private static string GetRestaurantOperationUnitTypeText(RestaurantOperationUnitType unitType)
    {
        return unitType switch
        {
            RestaurantOperationUnitType.DiningRoom => "Sala",
            RestaurantOperationUnitType.Kitchen => "Kuhinja",
            RestaurantOperationUnitType.TakeawayCounter => "Šalter / za poneti",
            RestaurantOperationUnitType.Delivery => "Dostava",
            RestaurantOperationUnitType.Reception => "Recepcija / portir",
            RestaurantOperationUnitType.Bar => "Šank",
            RestaurantOperationUnitType.Other => "Ostalo",
            _ => unitType.ToString()
        };
    }

    private static string BuildCustomerDisplayName(
        BookingPlatform.Domain.Customers.CustomerProfile? profile,
        BookingPlatform.Domain.Auth.AppUser user)
    {
        return NormalizeText(profile?.Nickname, 80)
            ?? NormalizeText(profile?.FullName, 200)
            ?? NormalizeText(profile?.Phone, 50)
            ?? NormalizeText(profile?.Email, 256)
            ?? NormalizeText(user.FullName, 200)
            ?? NormalizeText(user.Email, 256)
            ?? "Klijent";
    }

    private static string BuildCustomerDisplayName(
        BookingPlatform.Domain.Customers.CustomerProfile profile)
    {
        return NormalizeText(profile.Nickname, 80)
            ?? NormalizeText(profile.FullName, 200)
            ?? NormalizeText(profile.Phone, 50)
            ?? NormalizeText(profile.Email, 256)
            ?? "Klijent";
    }

    private static bool ContainsNormalized(string? value, string normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;

        return NormalizeSearchText(value).Contains(normalizedQuery);
    }

    private static string NormalizePhoneDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static HashSet<string> BuildPhoneSearchVariants(string phoneDigits)
    {
        var variants = new HashSet<string>();

        if (phoneDigits.Length < 3)
            return variants;

        variants.Add(phoneDigits);

        if (phoneDigits.StartsWith("0", StringComparison.Ordinal))
            variants.Add("381" + phoneDigits[1..]);

        if (phoneDigits.StartsWith("381", StringComparison.Ordinal))
            variants.Add("0" + phoneDigits[3..]);

        return variants;
    }

    private static bool PhoneMatches(string? phone, HashSet<string> queryVariants)
    {
        if (queryVariants.Count == 0)
            return false;

        var phoneDigits = NormalizePhoneDigits(phone);

        if (phoneDigits.Length == 0)
            return false;

        var phoneVariants = BuildPhoneSearchVariants(phoneDigits);
        phoneVariants.Add(phoneDigits);

        return phoneVariants.Any(phoneVariant =>
            queryVariants.Any(queryVariant =>
                phoneVariant.Contains(queryVariant) ||
                queryVariant.Contains(phoneVariant)));
    }

    private static string? MaskPhone(string? phone)
    {
        var digits = NormalizePhoneDigits(phone);

        if (digits.Length == 0)
            return null;

        if (digits.Length <= 4)
            return new string('*', digits.Length);

        return $"{digits[..3]}***{digits[^2..]}";
    }

    private static string? MaskEmail(string? email)
    {
        var normalized = NormalizeText(email, 256);

        if (normalized is null)
            return null;

        var atIndex = normalized.IndexOf('@');

        if (atIndex <= 0)
            return "***";

        var name = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        var visibleName = name.Length <= 2
            ? name[0].ToString()
            : name[..2];

        return $"{visibleName}***@{domain}";
    }

    private static string? BuildDefaultDeliveryAddress(CustomerPortalUpdateProfileRequest request)
    {
        var streetParts = new[]
        {
            NormalizeText(request.DefaultDeliveryStreet, 200),
            NormalizeText(request.DefaultDeliveryStreetNumber, 40)
        }.Where(x => x is not null);

        var streetLine = string.Join(" ", streetParts);
        var city = NormalizeText(request.DefaultDeliveryCity, 120);

        var addressParts = new[]
        {
            string.IsNullOrWhiteSpace(streetLine) ? null : streetLine,
            city
        }.Where(x => x is not null);

        var address = string.Join(", ", addressParts);

        return string.IsNullOrWhiteSpace(address)
            ? null
            : NormalizeText(address, 500);
    }

    private sealed class CustomerOrderItemBuildResult
    {
        public RestaurantOrderItem? Item { get; private set; }

        public string? Error { get; private set; }

        public static CustomerOrderItemBuildResult Ok(RestaurantOrderItem item)
        {
            return new CustomerOrderItemBuildResult
            {
                Item = item
            };
        }

        public static CustomerOrderItemBuildResult Fail(string error)
        {
            return new CustomerOrderItemBuildResult
            {
                Error = error
            };
        }
    }

    public sealed class CustomerPortalUpdateProfileRequest
    {
        public string? FullName { get; set; }

        public string? Nickname { get; set; }

        public string? Phone { get; set; }

        public bool AllowUserSearch { get; set; }

        public bool AllowChatDiscovery { get; set; }

        public string? DefaultDeliveryAddress { get; set; }

        public string? DefaultDeliveryCity { get; set; }

        public string? DefaultDeliveryStreet { get; set; }

        public string? DefaultDeliveryStreetNumber { get; set; }

        public string? DefaultDeliveryApartment { get; set; }

        public string? DefaultDeliveryNote { get; set; }

        public double? DefaultDeliveryLatitude { get; set; }

        public double? DefaultDeliveryLongitude { get; set; }
    }

    private sealed class CustomerBusinessConnectionRow
    {
        public long BusinessId { get; set; }

        public long BusinessCustomerId { get; set; }
    }
}
