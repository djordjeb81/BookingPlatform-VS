using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessSettingsConfiguration : IEntityTypeConfiguration<FitnessSettings>
{
    public void Configure(EntityTypeBuilder<FitnessSettings> builder)
    {
        builder.ToTable("fitness_settings");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.BusinessId)
            .IsUnique();

        builder.Property(x => x.GroupClassesEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.IndividualTrainingEnabled)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.MembershipsEnabled)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.UnpaidMembershipBookingPolicy)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.DefaultMembershipDurationDays)
            .HasDefaultValue(30)
            .IsRequired();

        builder.Property(x => x.AllowCustomerCancelBooking)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.CustomerCancelDeadlineMinutes)
            .HasDefaultValue(120)
            .IsRequired();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}