using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.SystemAlarms;

public sealed class SystemAlarmTrigger : Entity
{
    public long BusinessId { get; set; }

    public SystemAlarmDomain Domain { get; set; }

    public SystemAlarmType AlarmType { get; set; }

    public SystemAlarmStatus Status { get; set; } = SystemAlarmStatus.Pending;

    public SystemAlarmTargetType TargetType { get; set; } = SystemAlarmTargetType.Business;

    public long? TargetUserId { get; set; }

    public long? TargetOperationUnitId { get; set; }

    public long? RelatedOrderId { get; set; }

    public long? RelatedAppointmentId { get; set; }

    public long? RelatedChatConversationId { get; set; }

    public long? RelatedChatMessageId { get; set; }

    public DateTime TriggerAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? FiredAtUtc { get; set; }

    public DateTime? StoppedAtUtc { get; set; }

    public DateTime? SnoozedUntilUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string SoundKey { get; set; } = string.Empty;

    public bool IsUrgent { get; set; }

    public bool RequiresUserAction { get; set; } = true;

    public string? ActionKey { get; set; }

    public string? PayloadJson { get; set; }

    public Business? Business { get; set; }
}