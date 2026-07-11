using BookingPlatform.Domain.Fitness;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class FitnessSessionConfiguration : IEntityTypeConfiguration<FitnessSession>
{
    public void Configure(EntityTypeBuilder<FitnessSession> builder)
    {
        builder.ToTable("fitness_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.StartAtUtc)
            .IsRequired();

        builder.Property(x => x.EndAtUtc)
            .IsRequired();

        builder.Property(x => x.Capacity)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.BusinessId);

        builder.HasIndex(x => x.FitnessRoomId);

        builder.HasIndex(x => x.FitnessClassTypeId);

        builder.HasIndex(x => x.FitnessSessionTemplateId);

        builder.HasIndex(x => x.TrainerStaffMemberId);

        builder.HasIndex(x => new { x.BusinessId, x.StartAtUtc });

        builder.HasIndex(x => new { x.FitnessRoomId, x.StartAtUtc, x.EndAtUtc });

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessRoom)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.FitnessRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.FitnessClassType)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.FitnessClassTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FitnessSessionTemplate)
            .WithMany(x => x.Sessions)
            .HasForeignKey(x => x.FitnessSessionTemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.TrainerStaffMember)
            .WithMany()
            .HasForeignKey(x => x.TrainerStaffMemberId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}