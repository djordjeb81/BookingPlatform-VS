using BookingPlatform.Domain.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class RestaurantTableReservationConfiguration : IEntityTypeConfiguration<RestaurantTableReservation>
{
    public void Configure(EntityTypeBuilder<RestaurantTableReservation> builder)
    {
        builder.ToTable("restaurant_table_reservations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PartySize)
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.CustomerPhone)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CustomerEmail)
            .HasMaxLength(200);

        builder.Property(x => x.ReservationAtUtc)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.Property(x => x.InternalNote)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.RestaurantAreaId);

        builder.HasIndex(x => x.TableResourceId);

        builder.HasIndex(x => x.ReservationAtUtc);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => new
        {
            x.BusinessId,
            x.ReservationAtUtc,
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
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.CreatedTableSession)
            .WithMany()
            .HasForeignKey(x => x.CreatedTableSessionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}