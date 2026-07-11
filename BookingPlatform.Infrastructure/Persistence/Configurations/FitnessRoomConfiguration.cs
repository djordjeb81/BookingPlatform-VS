using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessRoomConfiguration : IEntityTypeConfiguration<FitnessRoom>
{
    public void Configure(EntityTypeBuilder<FitnessRoom> builder)
    {
        builder.ToTable("fitness_rooms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Capacity)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.AllowsGroupClasses)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.AllowsIndividualTraining)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => new { x.BusinessId, x.Name })
            .IsUnique();

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}