using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookingPlatform.Infrastructure.Persistence;

public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BookingDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=booking_platform;Username=postgres;Password=020581");

        return new BookingDbContext(optionsBuilder.Options);
    }
}