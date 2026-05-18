using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantTableSessionConfiguration : IEntityTypeConfiguration<RestaurantTableSession>
{
    public void Configure(EntityTypeBuilder<RestaurantTableSession> builder)
    {
        builder.ToTable("restaurant_table_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200);

        builder.Property(x => x.CustomerPhone)
            .HasMaxLength(50);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.RestaurantAreaId);

        builder.HasIndex(x => x.TableResourceId);

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.TableResourceId,
            x.Status
        });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RestaurantArea)
            .WithMany()
            .HasForeignKey(x => x.RestaurantAreaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TableResource)
            .WithMany()
            .HasForeignKey(x => x.TableResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}