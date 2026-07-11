using System.Security.Cryptography;
using System.Text;
using BookingPlatform.Contracts.AdminAccess;
using BookingPlatform.Domain.Platform;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class AdminAccessService
{
    private const string AdminAccessEmailSettingKey = "AdminAccessEmail";
    private const int CodeExpiresMinutes = 10;
    private const int SessionExpiresMinutes = 30;

    private readonly BookingDbContext _db;
    private readonly PlatformSettingsService _settings;
    private readonly IEmailSender _emailSender;

    public AdminAccessService(
        BookingDbContext db,
        PlatformSettingsService settings,
        IEmailSender emailSender)
    {
        _db = db;
        _settings = settings;
        _emailSender = emailSender;
    }

    public async Task<RequestAdminAccessCodeResponse> RequestCodeAsync(
        string email,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return new RequestAdminAccessCodeResponse
            {
                Succeeded = false,
                Message = "Unesite admin email."
            };
        }

        var configuredAdminEmail = NormalizeEmail(
            await _settings.GetRequiredValueAsync(AdminAccessEmailSettingKey, cancellationToken));

        if (!string.Equals(email, configuredAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new RequestAdminAccessCodeResponse
            {
                Succeeded = false,
                Message = "Nemate pravo pristupa admin sekciji."
            };
        }

        var nowUtc = DateTime.UtcNow;
        var code = GenerateSixDigitCode();

        var accessCode = new AdminAccessCode
        {
            Email = email,
            CodeHash = HashCode(email, code),
            ExpiresAtUtc = nowUtc.AddMinutes(CodeExpiresMinutes),
            UsedAtUtc = null,
            CreatedAtUtc = nowUtc,
            CreatedFromIp = ipAddress
        };

        _db.AdminAccessCodes.Add(accessCode);
        await _db.SaveChangesAsync(cancellationToken);

        var subject = "SmartBooking admin kod";
        var body =
            $"Vaš SmartBooking admin kod je: {code}\n\n" +
            $"Kod važi {CodeExpiresMinutes} minuta.\n\n" +
            "Ako vi niste tražili ovaj kod, ignorišite ovu poruku.";

        await _emailSender.SendAsync(
            email,
            subject,
            body,
            cancellationToken);

        return new RequestAdminAccessCodeResponse
        {
            Succeeded = true,
            Message = $"Kod je poslat na {email}. Važi {CodeExpiresMinutes} minuta."
        };
    }

    public async Task<VerifyAdminAccessCodeResponse> VerifyCodeAsync(
        string email,
        string code,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        email = NormalizeEmail(email);
        code = (code ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            return new VerifyAdminAccessCodeResponse
            {
                IsAllowed = false,
                Message = "Unesite email i kod."
            };
        }

        var configuredAdminEmail = NormalizeEmail(
            await _settings.GetRequiredValueAsync(AdminAccessEmailSettingKey, cancellationToken));

        if (!string.Equals(email, configuredAdminEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new VerifyAdminAccessCodeResponse
            {
                IsAllowed = false,
                Message = "Nemate pravo pristupa admin sekciji."
            };
        }

        var nowUtc = DateTime.UtcNow;
        var codeHash = HashCode(email, code);

        var accessCode = await _db.AdminAccessCodes
            .Where(x =>
                x.Email == email &&
                x.CodeHash == codeHash &&
                x.UsedAtUtc == null &&
                x.ExpiresAtUtc > nowUtc)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (accessCode is null)
        {
            return new VerifyAdminAccessCodeResponse
            {
                IsAllowed = false,
                Message = "Kod nije ispravan ili je istekao."
            };
        }

        accessCode.UsedAtUtc = nowUtc;

        var token = GenerateToken();

        var session = new AdminAccessSession
        {
            Email = email,
            TokenHash = HashToken(token),
            ExpiresAtUtc = nowUtc.AddMinutes(SessionExpiresMinutes),
            RevokedAtUtc = null,
            CreatedAtUtc = nowUtc,
            CreatedFromIp = ipAddress
        };

        _db.AdminAccessSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        return new VerifyAdminAccessCodeResponse
        {
            IsAllowed = true,
            AdminAccessToken = token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Message = $"Admin pristup je odobren. Sesija važi {SessionExpiresMinutes} minuta."
        };
    }

    public async Task<bool> IsTokenValidAsync(
        string? token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var tokenHash = HashToken(token.Trim());
        var nowUtc = DateTime.UtcNow;

        return await _db.AdminAccessSessions
            .AsNoTracking()
            .AnyAsync(x =>
                x.TokenHash == tokenHash &&
                x.RevokedAtUtc == null &&
                x.ExpiresAtUtc > nowUtc,
                cancellationToken);
    }

    private static string NormalizeEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static string GenerateSixDigitCode()
    {
        var number = RandomNumberGenerator.GetInt32(100000, 1000000);
        return number.ToString();
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static string HashCode(string email, string code)
    {
        return Sha256($"{email}|{code}");
    }

    private static string HashToken(string token)
    {
        return Sha256(token);
    }

    private static string Sha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}