using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessRoomWorkingHourConfiguration : IEntityTypeConfiguration<FitnessRoomWorkingHour>
{
    public void Configure(EntityTypeBuilder<FitnessRoomWorkingHour> builder)
    {
        builder.ToTable("fitness_room_working_hours");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek)
            .IsRequired();

        builder.Property(x => x.IsClosed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.OpenTime);

        builder.Property(x => x.CloseTime);

        builder.HasIndex(x => x.BusinessId);
        builder.HasIndex(x => x.FitnessRoomId);

        builder.HasIndex(x => new { x.FitnessRoomId, x.DayOfWeek })
            .IsUnique();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessRoom)
            .WithMany(x => x.WorkingHours)
            .HasForeignKey(x => x.FitnessRoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}