using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class PlatformSettingsService
{
    private readonly BookingDbContext _db;

    public PlatformSettingsService(BookingDbContext db)
    {
        _db = db;
    }

    public async Task<string?> GetValueAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return await _db.PlatformSettings
            .AsNoTracking()
            .Where(x => x.Key == key)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> GetRequiredValueAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Platform setting nije podešen: {key}");

        return value;
    }
}