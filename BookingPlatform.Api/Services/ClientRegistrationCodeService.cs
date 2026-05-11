using BookingPlatform.Domain.Auth;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BookingPlatform.Api.Services;

public sealed class ClientRegistrationCodeService : IClientRegistrationCodeService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly BookingDbContext _dbContext;

    public ClientRegistrationCodeService(BookingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClientRegistrationCodeResult> CreateCodeAsync(
        string email,
        string purpose,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPurpose = NormalizePurpose(purpose);

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            throw new InvalidOperationException("Email je obavezan.");

        if (string.IsNullOrWhiteSpace(normalizedPurpose))
            throw new InvalidOperationException("Namena koda je obavezna.");

        var now = DateTime.UtcNow;

        var activeOldCodes = await _dbContext.EmailVerificationCodes
            .Where(x =>
                x.NormalizedEmail == normalizedEmail &&
                x.Purpose == normalizedPurpose &&
                x.UsedAtUtc == null &&
                x.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var oldCode in activeOldCodes)
            oldCode.UsedAtUtc = now;

        var code = GenerateCode();
        var expiresAtUtc = now.Add(CodeLifetime);

        var entity = new EmailVerificationCode
        {
            Email = email.Trim(),
            NormalizedEmail = normalizedEmail,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            Purpose = normalizedPurpose,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now
        };

        _dbContext.EmailVerificationCodes.Add(entity);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ClientRegistrationCodeResult
        {
            Email = email.Trim(),
            Code = code,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public async Task<bool> VerifyCodeAsync(
        string email,
        string code,
        string purpose,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedPurpose = NormalizePurpose(purpose);

        if (string.IsNullOrWhiteSpace(normalizedEmail) ||
            string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(normalizedPurpose))
        {
            return false;
        }

        var now = DateTime.UtcNow;

        var candidates = await _dbContext.EmailVerificationCodes
            .Where(x =>
                x.NormalizedEmail == normalizedEmail &&
                x.Purpose == normalizedPurpose &&
                x.UsedAtUtc == null &&
                x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (!BCrypt.Net.BCrypt.Verify(code.Trim(), candidate.CodeHash))
                continue;

            candidate.UsedAtUtc = now;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return true;
        }

        return false;
    }

    private static string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(100000, 1000000);
        return value.ToString();
    }

    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToUpperInvariant();
    }

    private static string NormalizePurpose(string? purpose)
    {
        return string.IsNullOrWhiteSpace(purpose)
            ? string.Empty
            : purpose.Trim();
    }
}