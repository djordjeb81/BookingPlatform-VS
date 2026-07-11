using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessBusinessWorkingHourConfiguration : IEntityTypeConfiguration<FitnessBusinessWorkingHour>
{
    public void Configure(EntityTypeBuilder<FitnessBusinessWorkingHour> builder)
    {
        builder.ToTable("fitness_business_working_hours");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DayOfWeek)
            .IsRequired();

        builder.Property(x => x.IsClosed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.OpenTime);

        builder.Property(x => x.CloseTime);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => new { x.BusinessId, x.DayOfWeek })
            .IsUnique();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}