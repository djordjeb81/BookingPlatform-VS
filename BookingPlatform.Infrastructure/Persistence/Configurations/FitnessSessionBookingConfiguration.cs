using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessSessionBookingConfiguration : IEntityTypeConfiguration<FitnessSessionBooking>
{
	public void Configure(EntityTypeBuilder<FitnessSessionBooking> builder)
	{
		builder.ToTable("fitness_session_bookings");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.CustomerName)
			.HasMaxLength(200)
			.IsRequired();

		builder.Property(x => x.CustomerPhone)
			.HasMaxLength(80)
			.IsRequired();

		builder.Property(x => x.Status)
			.HasConversion<int>()
			.IsRequired();

		builder.Property(x => x.MembershipWasActiveAtBooking)
			.HasDefaultValue(true)
			.IsRequired();

		builder.Property(x => x.MembershipWarningText)
			.HasMaxLength(500);

		builder.HasIndex(x => x.BusinessId);

		builder.HasIndex(x => x.FitnessSessionId);

		builder.HasIndex(x => x.CustomerProfileId);

		builder.HasIndex(x => x.BusinessCustomerId);

		builder.HasIndex(x => x.AppUserId);

		builder.HasIndex(x => new { x.FitnessSessionId, x.Status });

		builder.HasOne(x => x.Business)
			.WithMany()
			.HasForeignKey(x => x.BusinessId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.FitnessSession)
			.WithMany(x => x.Bookings)
			.HasForeignKey(x => x.FitnessSessionId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.CustomerProfile)
			.WithMany()
			.HasForeignKey(x => x.CustomerProfileId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.BusinessCustomer)
			.WithMany()
			.HasForeignKey(x => x.BusinessCustomerId)
			.OnDelete(DeleteBehavior.SetNull);

		builder.HasOne(x => x.AppUser)
			.WithMany()
			.HasForeignKey(x => x.AppUserId)
			.OnDelete(DeleteBehavior.SetNull);

        builder.Property(x => x.ConsumesTrainingPassSession)
    .HasDefaultValue(true)
    .IsRequired();

        builder.HasIndex(x => x.FitnessMemberTrainingPassId);

        builder.HasOne(x => x.FitnessMemberTrainingPass)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.FitnessMemberTrainingPassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.FitnessMemberId);

        builder.HasOne(x => x.FitnessMember)
            .WithMany()
            .HasForeignKey(x => x.FitnessMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}