using BookingPlatform.Domain.SystemAlarms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingPlatform.Infrastructure.Persistence.Configurations;

public sealed class SystemAlarmTriggerConfiguration : IEntityTypeConfiguration<SystemAlarmTrigger>
{
    public void Configure(EntityTypeBuilder<SystemAlarmTrigger> entity)
    {
        entity.ToTable("system_alarm_triggers");

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Domain)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.AlarmType)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.TargetType)
            .HasConversion<int>()
            .IsRequired();

        entity.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        entity.Property(x => x.Message)
            .IsRequired()
            .HasMaxLength(1000);

        entity.Property(x => x.SoundKey)
            .IsRequired()
            .HasMaxLength(120);

        entity.Property(x => x.ActionKey)
            .HasMaxLength(120);

        entity.Property(x => x.PayloadJson)
            .HasColumnType("jsonb");

        entity.Property(x => x.IsUrgent)
            .HasDefaultValue(false);

        entity.Property(x => x.RequiresUserAction)
            .HasDefaultValue(true);

        entity.HasIndex(x => x.BusinessId);

        entity.HasIndex(x => new { x.BusinessId, x.Status, x.TriggerAtUtc });

        entity.HasIndex(x => new { x.BusinessId, x.Domain, x.AlarmType });

        entity.HasIndex(x => x.TargetUserId);

        entity.HasIndex(x => x.TargetOperationUnitId);

        entity.HasIndex(x => x.RelatedOrderId);

        entity.HasIndex(x => x.RelatedAppointmentId);

        entity.HasIndex(x => x.RelatedChatConversationId);

        entity.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}