using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Customers;
using BookingPlatform.Domain.Customers;
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
public sealed class BusinessCustomersController : ApiControllerBase
{
    public BusinessCustomersController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<BusinessCustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<BusinessCustomerDto>>> GetAll(
        [FromQuery] long businessId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var items = await DbContext.BusinessCustomers
            .AsNoTracking()
            .Include(x => x.CustomerProfile)
            .Where(x => x.BusinessId == businessId)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.CustomerProfile != null ? x.CustomerProfile.FullName : x.FullName)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(List<BusinessCustomerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<List<BusinessCustomerDto>>> Search(
        [FromQuery] long businessId,
        [FromQuery] string? q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var searchText = q?.Trim();

        if (string.IsNullOrWhiteSpace(searchText))
            return Ok(new List<BusinessCustomerDto>());

        limit = Math.Clamp(limit, 1, 20);

        var pattern = $"%{searchText}%";
        var normalizedPhoneQuery = NormalizePhone(searchText);
        var phonePattern = $"%{normalizedPhoneQuery}%";

        var query = DbContext.BusinessCustomers
            .AsNoTracking()
            .Include(x => x.CustomerProfile)
            .Where(x => x.BusinessId == businessId);

        query = query.Where(x =>
            (x.CustomerProfile != null && EF.Functions.ILike(x.CustomerProfile.FullName, pattern)) ||
            (x.CustomerProfile != null && x.CustomerProfile.Email != null && EF.Functions.ILike(x.CustomerProfile.Email, pattern)) ||
            (x.CustomerProfile != null && x.CustomerProfile.Phone != null && EF.Functions.ILike(x.CustomerProfile.Phone, pattern)) ||

            // fallback za stare zapise dok ne završimo potpuno prebacivanje na CustomerProfile
            EF.Functions.ILike(x.FullName, pattern) ||
            (x.Email != null && EF.Functions.ILike(x.Email, pattern)) ||
            (x.Phone != null && EF.Functions.ILike(x.Phone, pattern)) ||

            (
                normalizedPhoneQuery.Length >= 3 &&
                x.CustomerProfile != null &&
                x.CustomerProfile.Phone != null &&
                EF.Functions.ILike(
                    x.CustomerProfile.Phone
                        .Replace(" ", "")
                        .Replace("-", "")
                        .Replace("/", "")
                        .Replace("(", "")
                        .Replace(")", "")
                        .Replace("+", ""),
                    phonePattern)
            ) ||
            (
                normalizedPhoneQuery.Length >= 3 &&
                x.Phone != null &&
                EF.Functions.ILike(
                    x.Phone
                        .Replace(" ", "")
                        .Replace("-", "")
                        .Replace("/", "")
                        .Replace("(", "")
                        .Replace(")", "")
                        .Replace("+", ""),
                    phonePattern)
            ));

        var items = await query
            .OrderBy(x => x.CustomerProfile != null ? x.CustomerProfile.FullName : x.FullName)
            .Take(limit)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("lookup-by-email")]
    [ProducesResponseType(typeof(BusinessCustomerEmailLookupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessCustomerEmailLookupResponse>> LookupByEmail(
    [FromQuery] long businessId,
    [FromQuery] string? email,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var normalizedEmail = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return BadRequest("Email je obavezan.");

        var profile = await DbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email != null &&
                     x.Email.Trim().ToLower() == normalizedEmail,
                cancellationToken);

        if (profile is null)
        {
            return Ok(new BusinessCustomerEmailLookupResponse
            {
                Email = normalizedEmail,
                CustomerProfileExists = false,
                BusinessCustomerExists = false
            });
        }

        var businessCustomer = await DbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        return Ok(new BusinessCustomerEmailLookupResponse
        {
            Email = normalizedEmail,
            CustomerProfileExists = true,
            CustomerProfileId = profile.Id,
            BusinessCustomerExists = businessCustomer is not null,
            BusinessCustomerId = businessCustomer?.Id,
            AppUserId = profile.AppUserId,
            FullName = profile.FullName,
            Phone = profile.Phone
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(BusinessCustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessCustomerDto>> Create(
        [FromBody] CreateBusinessCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationError = ValidateCustomerInput(
            request.FullName,
            request.Phone,
            request.Email);

        if (validationError is not null)
            return BadRequest(validationError);

        var businessExists = await DbContext.Businesses
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.BusinessId, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji.");

        if (request.AppUserId.HasValue)
        {
            var appUserExists = await DbContext.AppUsers
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.AppUserId.Value, cancellationToken);

            if (!appUserExists)
                return BadRequest("Izabrani korisnik aplikacije ne postoji.");
        }

        var profile = await GetOrCreateCustomerProfileAsync(
            request.FullName,
            request.Phone,
            request.Email,
            request.AppUserId,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        var alreadyExistsForBusiness = await DbContext.BusinessCustomers
            .AsNoTracking()
            .AnyAsync(
                x => x.BusinessId == request.BusinessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        if (alreadyExistsForBusiness)
            return BadRequest("Ovaj klijent već postoji u izabranom biznisu.");

        var now = DateTime.UtcNow;

        var entity = new BusinessCustomer
        {
            BusinessId = request.BusinessId,
            CustomerProfileId = profile.Id,

            // Privremeno držimo i stare kolone sinhronizovane,
            // da postojeći Desktop DTO i stariji delovi sistema nastave da rade.
            AppUserId = profile.AppUserId,
            FullName = profile.FullName,
            Phone = profile.Phone,
            Email = profile.Email,

            Notes = NormalizeOptionalText(request.Notes),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.BusinessCustomers.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        entity.CustomerProfile = profile;

        return Ok(ToDto(entity));
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(BusinessCustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessCustomerDto>> Update(
        [FromRoute] long id,
        [FromBody] UpdateBusinessCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.BusinessCustomers
            .Include(x => x.CustomerProfile)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Klijent ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationError = ValidateCustomerInput(
            request.FullName,
            request.Phone,
            request.Email);

        if (validationError is not null)
            return BadRequest(validationError);

        if (request.AppUserId.HasValue)
        {
            var appUserExists = await DbContext.AppUsers
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.AppUserId.Value, cancellationToken);

            if (!appUserExists)
                return BadRequest("Izabrani korisnik aplikacije ne postoji.");
        }

        if (entity.CustomerProfile is null)
        {
            entity.CustomerProfile = await GetOrCreateCustomerProfileAsync(
                entity.FullName,
                entity.Phone,
                entity.Email,
                entity.AppUserId,
                cancellationToken);

            await DbContext.SaveChangesAsync(cancellationToken);

            entity.CustomerProfileId = entity.CustomerProfile.Id;
        }

        var now = DateTime.UtcNow;
        var currentProfile = entity.CustomerProfile;

        var profileIsLinkedToAppUser = currentProfile.AppUserId.HasValue;

        var requestedFullName = request.FullName.Trim();
        var requestedPhone = NormalizeOptionalText(request.Phone);
        var requestedEmail = NormalizeEmail(request.Email);
        var currentPhone = NormalizeOptionalText(currentProfile.Phone);
        var currentEmail = NormalizeEmail(currentProfile.Email);

        if (profileIsLinkedToAppUser)
        {
            var globalDataChanged =
                requestedFullName != currentProfile.FullName ||
                requestedPhone != currentPhone ||
                requestedEmail != currentEmail ||
                request.AppUserId != currentProfile.AppUserId;

            if (globalDataChanged)
            {
                return BadRequest(
                    "Ovaj klijent je povezan sa korisničkim nalogom. " +
                    "Iz Desk aplikacije možete menjati samo napomenu za svoj biznis.");
            }

            entity.Notes = NormalizeOptionalText(request.Notes);
            entity.IsActive = request.IsActive;
            entity.UpdatedAtUtc = now;

            // Privremena sinhronizacija starih kolona.
            entity.AppUserId = currentProfile.AppUserId;
            entity.FullName = currentProfile.FullName;
            entity.Phone = currentProfile.Phone;
            entity.Email = currentProfile.Email;

            await DbContext.SaveChangesAsync(cancellationToken);

            return Ok(ToDto(entity));
        }

        if (!string.IsNullOrWhiteSpace(requestedEmail))
        {
            var existingProfileWithEmail = await DbContext.CustomerProfiles
                .FirstOrDefaultAsync(
                    x => x.Id != currentProfile.Id &&
                         x.Email != null &&
                         x.Email.Trim().ToLower() == requestedEmail,
                    cancellationToken);

            if (existingProfileWithEmail is not null)
            {
                var alreadyExistsForBusiness = await DbContext.BusinessCustomers
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.Id != entity.Id &&
                             x.BusinessId == entity.BusinessId &&
                             x.CustomerProfileId == existingProfileWithEmail.Id,
                        cancellationToken);

                if (alreadyExistsForBusiness)
                    return BadRequest("Ovaj email već pripada klijentu u ovom biznisu.");

                entity.CustomerProfileId = existingProfileWithEmail.Id;
                entity.CustomerProfile = existingProfileWithEmail;
                currentProfile = existingProfileWithEmail;
            }
        }

        currentProfile.FullName = requestedFullName;
        currentProfile.Phone = requestedPhone;
        currentProfile.Email = requestedEmail;
        currentProfile.AppUserId = request.AppUserId;
        currentProfile.UpdatedAtUtc = now;

        entity.Notes = NormalizeOptionalText(request.Notes);
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = now;

        // Privremena sinhronizacija starih kolona.
        entity.AppUserId = currentProfile.AppUserId;
        entity.FullName = currentProfile.FullName;
        entity.Phone = currentProfile.Phone;
        entity.Email = currentProfile.Email;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }
    private static BusinessCustomerDto ToDto(BusinessCustomer entity)
    {
        var profile = entity.CustomerProfile;

        return new BusinessCustomerDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            AppUserId = profile?.AppUserId ?? entity.AppUserId,
            FullName = profile?.FullName ?? entity.FullName,
            Phone = profile?.Phone ?? entity.Phone,
            Email = profile?.Email ?? entity.Email,
            Notes = entity.Notes,
            IsActive = entity.IsActive,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static string? ValidateCustomerInput(
        string? fullName,
        string? phone,
        string? email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Unesite ime i prezime klijenta.";

        if (fullName.Trim().Length > 200)
            return "Ime i prezime ne sme biti duže od 200 karaktera.";

        if (!string.IsNullOrWhiteSpace(phone) && phone.Trim().Length > 50)
            return "Telefon ne sme biti duži od 50 karaktera.";

        if (!string.IsNullOrWhiteSpace(email) && email.Trim().Length > 256)
            return "Email ne sme biti duži od 256 karaktera.";

        return null;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : trimmed;
    }

    private static string NormalizePhone(string value)
    {
        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        return email.Trim().ToLowerInvariant();
    }

    private async Task<CustomerProfile> GetOrCreateCustomerProfileAsync(
        string fullName,
        string? phone,
        string? email,
        long? appUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);

        if (!string.IsNullOrWhiteSpace(normalizedEmail))
        {
            var existingProfile = await DbContext.CustomerProfiles
                .FirstOrDefaultAsync(
                    x => x.Email != null &&
                         x.Email.Trim().ToLower() == normalizedEmail,
                    cancellationToken);

            if (existingProfile is not null)
            {
                var changed = false;

                if (string.IsNullOrWhiteSpace(existingProfile.FullName) && !string.IsNullOrWhiteSpace(fullName))
                {
                    existingProfile.FullName = fullName.Trim();
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(existingProfile.Phone) && !string.IsNullOrWhiteSpace(phone))
                {
                    existingProfile.Phone = phone.Trim();
                    changed = true;
                }

                if (!existingProfile.AppUserId.HasValue && appUserId.HasValue)
                {
                    existingProfile.AppUserId = appUserId;
                    changed = true;
                }

                if (changed)
                    existingProfile.UpdatedAtUtc = DateTime.UtcNow;

                return existingProfile;
            }
        }

        var now = DateTime.UtcNow;

        var profile = new CustomerProfile
        {
            AppUserId = appUserId,
            FullName = fullName.Trim(),
            Phone = NormalizeOptionalText(phone),
            Email = normalizedEmail,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.CustomerProfiles.Add(profile);

        return profile;
    }
}