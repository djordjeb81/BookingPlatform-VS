using BCrypt.Net;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly BookingDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IBusinessCustomerLinkingService _businessCustomerLinkingService;
    private readonly IClientRegistrationCodeService _clientRegistrationCodeService;

    public AuthController(
        BookingDbContext dbContext,
        IConfiguration configuration,
        IBusinessCustomerLinkingService businessCustomerLinkingService,
        IClientRegistrationCodeService clientRegistrationCodeService)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _businessCustomerLinkingService = businessCustomerLinkingService;
        _clientRegistrationCodeService = clientRegistrationCodeService;
    }

    [HttpPost("client-register/request-code")]
    [ProducesResponseType(typeof(RequestClientRegisterCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestClientRegisterCodeResponse>> RequestClientRegisterCode(
    [FromBody] RequestClientRegisterCodeRequest request,
    CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        var normalizedEmail = email.ToUpperInvariant();
        var normalizedEmailLower = email.ToLowerInvariant();

        var existingAppUser = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        var existingCustomerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email != null &&
                     x.Email.Trim().ToLower() == normalizedEmailLower,
                cancellationToken);

        if (existingAppUser is not null)
        {
            return Ok(new RequestClientRegisterCodeResponse
            {
                Email = email,
                ExistingAppUserFound = true,
                ExistingCustomerProfileFound = existingCustomerProfile is not null,
                Message = "Nalog sa ovim emailom već postoji. Prijavite se postojećom šifrom ili koristite opciju „Zaboravljena lozinka”.",
                DevelopmentCode = null
            });
        }

        var codeResult = await _clientRegistrationCodeService.CreateCodeAsync(
            email,
            EmailVerificationPurposes.ClientRegister,
            cancellationToken);

        return Ok(new RequestClientRegisterCodeResponse
        {
            Email = email,
            ExistingAppUserFound = false,
            ExistingCustomerProfileFound = existingCustomerProfile is not null,
            Message = existingCustomerProfile is not null
                ? "Pronašli smo vaše podatke u sistemu. Poslali smo kod za potvrdu na email. Posle potvrde napravićete šifru za aplikaciju i nalog će biti povezan sa postojećim podacima."
                : "Poslali smo kod za potvrdu na email. Unesite kod da završite registraciju.",
            DevelopmentCode = codeResult.Code
        });
    }

    [HttpPost("client-register/complete")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> CompleteClientRegister(
     [FromBody] CompleteClientRegisterRequest request,
     CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        var code = request.Code?.Trim();
        var fullName = request.FullName?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Kod za potvrdu je obavezan.");

        if (string.IsNullOrWhiteSpace(fullName))
            return BadRequest("Ime i prezime su obavezni.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Lozinka je obavezna.");

        if (request.Password.Length < 6)
            return BadRequest("Lozinka mora imati najmanje 6 karaktera.");

        var normalizedEmail = email.ToUpperInvariant();

        var exists = await _dbContext.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
            return BadRequest("Nalog sa ovim emailom već postoji. Prijavite se postojećom šifrom ili koristite opciju „Zaboravljena lozinka”.");

        var codeIsValid = await _clientRegistrationCodeService.VerifyCodeAsync(
            email,
            code,
            EmailVerificationPurposes.ClientRegister,
            cancellationToken);

        if (!codeIsValid)
            return BadRequest("Kod nije ispravan ili je istekao. Zatražite novi kod.");

        var now = DateTime.UtcNow;

        var user = new AppUser
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _businessCustomerLinkingService.LinkByEmailAsync(
            user.Id,
            user.Email,
            cancellationToken);

        return Ok(await BuildAuthResponseAsync(user.Id, cancellationToken));
    }

    [HttpPost("password-reset/request-code")]
    [ProducesResponseType(typeof(RequestPasswordResetCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestPasswordResetCodeResponse>> RequestPasswordResetCode(
    [FromBody] RequestPasswordResetCodeRequest request,
    CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        var normalizedEmail = email.ToUpperInvariant();

        var existingAppUser = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingAppUser is null)
        {
            return Ok(new RequestPasswordResetCodeResponse
            {
                Email = email,
                ExistingAppUserFound = false,
                Message = "Nalog sa ovim emailom ne postoji. Prvo napravite nalog.",
                DevelopmentCode = null
            });
        }

        var codeResult = await _clientRegistrationCodeService.CreateCodeAsync(
            email,
            EmailVerificationPurposes.PasswordReset,
            cancellationToken);

        return Ok(new RequestPasswordResetCodeResponse
        {
            Email = email,
            ExistingAppUserFound = true,
            Message = "Poslali smo kod za obnovu lozinke na email.",
            DevelopmentCode = codeResult.Code
        });
    }

    [HttpPost("password-reset/complete")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> CompletePasswordReset(
        [FromBody] CompletePasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        var code = request.Code?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Kod za potvrdu je obavezan.");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest("Nova lozinka je obavezna.");

        if (request.NewPassword.Length < 6)
            return BadRequest("Lozinka mora imati najmanje 6 karaktera.");

        var normalizedEmail = email.ToUpperInvariant();

        var user = await _dbContext.AppUsers
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null)
            return BadRequest("Nalog sa ovim emailom ne postoji.");

        if (!user.IsActive)
            return BadRequest("Nalog sa ovim emailom nije aktivan.");

        var codeIsValid = await _clientRegistrationCodeService.VerifyCodeAsync(
            email,
            code,
            EmailVerificationPurposes.PasswordReset,
            cancellationToken);

        if (!codeIsValid)
            return BadRequest("Kod nije ispravan ili je istekao. Zatražite novi kod.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _businessCustomerLinkingService.LinkByEmailAsync(
            user.Id,
            user.Email,
            cancellationToken);

        return Ok(await BuildAuthResponseAsync(user.Id, cancellationToken));
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var fullName = request.FullName.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        if (string.IsNullOrWhiteSpace(fullName))
            return BadRequest("Ime i prezime su obavezni.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Lozinka je obavezna.");

        if (request.Password.Length < 6)
            return BadRequest("Lozinka mora imati najmanje 6 karaktera.");

        var exists = await _dbContext.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
            return BadRequest("Korisnik sa tim email-om već postoji.");

        var now = DateTime.UtcNow;

        var user = new AppUser
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.InitialBusinessId.HasValue)
        {
            var business = await _dbContext.Businesses
                .FirstOrDefaultAsync(x => x.Id == request.InitialBusinessId.Value, cancellationToken);

            if (business is null)
                return BadRequest("Business ne postoji.");

            var businessAlreadyHasMemberships = await _dbContext.BusinessUserMemberships
                .AnyAsync(x => x.BusinessId == business.Id, cancellationToken);

            if (businessAlreadyHasMemberships)
                return BadRequest("Business već ima dodeljenog korisnika.");

            _dbContext.BusinessUserMemberships.Add(new BusinessUserMembership
            {
                AppUserId = user.Id,
                BusinessId = business.Id,
                Role = BusinessUserRole.Owner,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _businessCustomerLinkingService.LinkByEmailAsync(
            user.Id,
            user.Email,
            cancellationToken);

        return Ok(await BuildAuthResponseAsync(user.Id, cancellationToken));
    }

    [HttpPost("register-owner")]
    public async Task<ActionResult<AuthResponse>> RegisterOwner(
        [FromBody] RegisterOwnerRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        var fullName = request.FullName.Trim();
        var businessName = request.BusinessName.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
            return BadRequest("Ime i prezime su obavezni.");

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Email je obavezan.");

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Lozinka je obavezna.");

        if (request.Password.Length < 6)
            return BadRequest("Lozinka mora imati najmanje 6 karaktera.");

        if (string.IsNullOrWhiteSpace(businessName))
            return BadRequest("Naziv biznisa je obavezan.");

        if (request.SlotIntervalMin <= 0)
            return BadRequest("Razmak između početaka termina mora biti veći od 0 minuta.");

        if (!Enum.IsDefined(typeof(BusinessType), request.BusinessType))
            return BadRequest("Izabrana vrsta biznisa nije ispravna.");

        if (!Enum.IsDefined(typeof(BookingMode), request.BookingMode))
            return BadRequest("Izabrani režim rada nije ispravan.");

        var exists = await _dbContext.AppUsers
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (exists)
            return BadRequest("Korisnik sa tim email-om već postoji.");

        var now = DateTime.UtcNow;

        var user = new AppUser
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var bookingMode = (BookingMode)request.BookingMode;

        var business = new Business
        {
            Name = businessName,
            BusinessType = (BusinessType)request.BusinessType,
            BookingMode = bookingMode,
            FeatureSettings = CreateDefaultFeatureSettings(bookingMode),
            Description = request.Description?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.BusinessEmail?.Trim(),
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
            UpdatedAtUtc = now
        };

        _dbContext.Businesses.Add(business);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.BusinessUserMemberships.Add(new BusinessUserMembership
        {
            AppUserId = user.Id,
            BusinessId = business.Id,
            Role = BusinessUserRole.Owner,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _businessCustomerLinkingService.LinkByEmailAsync(
            user.Id,
            user.Email,
            cancellationToken);

        return Ok(await BuildAuthResponseAsync(user.Id, cancellationToken));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email i lozinka su obavezni.");

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == email, cancellationToken);

        if (user is null || !user.IsActive)
            return Unauthorized("Neispravan email ili lozinka.");

        var validPassword = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!validPassword)
            return Unauthorized("Neispravan email ili lozinka.");

        await _businessCustomerLinkingService.LinkByEmailAsync(
            user.Id,
            user.Email,
            cancellationToken);

        return Ok(await BuildAuthResponseAsync(user.Id, cancellationToken));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = TryGetUserId();

        if (!userId.HasValue)
            return Unauthorized("Token nije validan.");

        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return Unauthorized("Korisnik ne postoji.");

        var memberships = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.AppUserId == user.Id)
            .Join(
                _dbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
(membership, business) => new AuthBusinessMembershipDto
{
    BusinessId = business.Id,
    BusinessName = business.Name,
    Role = membership.Role.ToString(),
    IsActive = membership.IsActive,
    BookingMode = (int)business.BookingMode
})
            .OrderBy(x => x.BusinessName)
            .ToListAsync(cancellationToken);

        return Ok(new MeResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            IsActive = user.IsActive,
            Memberships = memberships
        });
    }

    private long? TryGetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return long.TryParse(raw, out var userId) ? userId : null;
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .FirstAsync(x => x.Id == userId, cancellationToken);

        var memberships = await _dbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.AppUserId == user.Id)
            .Join(
                _dbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
(membership, business) => new AuthBusinessMembershipDto
{
    BusinessId = business.Id,
    BusinessName = business.Name,
    Role = membership.Role.ToString(),
    IsActive = membership.IsActive,
    BookingMode = (int)business.BookingMode
})
            .OrderBy(x => x.BusinessName)
            .ToListAsync(cancellationToken);

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("Jwt:ExpirationMinutes"));

        var token = GenerateJwtToken(user, expiresAtUtc);

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            Memberships = memberships
        };
    }

    private string GenerateJwtToken(AppUser user, DateTime expiresAtUtc)
    {
        var key = _configuration["Jwt:Key"]
                  ?? throw new InvalidOperationException("Jwt:Key nije podešen.");

        var issuer = _configuration["Jwt:Issuer"]
                     ?? throw new InvalidOperationException("Jwt:Issuer nije podešen.");

        var audience = _configuration["Jwt:Audience"]
                       ?? throw new InvalidOperationException("Jwt:Audience nije podešen.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static BusinessFeatureSettings CreateDefaultFeatureSettings(BookingMode bookingMode)
    {
        var settings = new BusinessFeatureSettings();

        switch (bookingMode)
        {
            case BookingMode.Hospitality:
                settings.ServiceAppointmentsEnabled = false;
                settings.TableReservationsEnabled = true;
                settings.HasCustomerSeating = true;
                settings.FoodOrdersEnabled = true;
                settings.DrinkOrdersEnabled = true;
                settings.TakeawayOrdersEnabled = true;
                settings.DeliveryOrdersEnabled = false;
                settings.EventHallReservationsEnabled = false;
                settings.AccommodationEnabled = false;
                settings.ReviewsEnabled = true;
                break;

            case BookingMode.Accommodation:
                settings.ServiceAppointmentsEnabled = false;
                settings.TableReservationsEnabled = false;
                settings.HasCustomerSeating = false;
                settings.FoodOrdersEnabled = false;
                settings.DrinkOrdersEnabled = false;
                settings.TakeawayOrdersEnabled = false;
                settings.DeliveryOrdersEnabled = false;
                settings.EventHallReservationsEnabled = false;
                settings.AccommodationEnabled = true;
                settings.ReviewsEnabled = true;
                break;

            case BookingMode.Fitness:
                settings.ServiceAppointmentsEnabled = false;
                settings.TableReservationsEnabled = false;
                settings.HasCustomerSeating = false;
                settings.FoodOrdersEnabled = false;
                settings.DrinkOrdersEnabled = false;
                settings.TakeawayOrdersEnabled = false;
                settings.DeliveryOrdersEnabled = false;
                settings.EventHallReservationsEnabled = false;
                settings.AccommodationEnabled = false;
                settings.ReviewsEnabled = true;
                break;

            default:
                settings.ServiceAppointmentsEnabled = true;
                settings.TableReservationsEnabled = false;
                settings.HasCustomerSeating = false;
                settings.FoodOrdersEnabled = false;
                settings.DrinkOrdersEnabled = false;
                settings.TakeawayOrdersEnabled = false;
                settings.DeliveryOrdersEnabled = false;
                settings.EventHallReservationsEnabled = false;
                settings.AccommodationEnabled = false;
                settings.ReviewsEnabled = true;
                break;
        }

        return settings;
    }
}