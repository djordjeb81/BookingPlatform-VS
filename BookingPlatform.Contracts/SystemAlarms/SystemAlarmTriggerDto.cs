namespace BookingPlatform.Contracts.SystemAlarms;

public sealed class SystemAlarmTriggerDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public int Domain { get; set; }

    public string DomainText { get; set; } = string.Empty;

    public int AlarmType { get; set; }

    public string AlarmTypeText { get; set; } = string.Empty;

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public int TargetType { get; set; }

    public string TargetTypeText { get; set; } = string.Empty;

    public long? TargetUserId { get; set; }

    public long? TargetOperationUnitId { get; set; }

    public long? RelatedOrderId { get; set; }

    public long? RelatedAppointmentId { get; set; }

    public long? RelatedChatConversationId { get; set; }

    public long? RelatedChatMessageId { get; set; }

    public DateTime TriggerAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? FiredAtUtc { get; set; }

    public DateTime? StoppedAtUtc { get; set; }

    public DateTime? SnoozedUntilUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string SoundKey { get; set; } = string.Empty;

    public bool IsUrgent { get; set; }

    public bool RequiresUserAction { get; set; }

    public string? ActionKey { get; set; }

    public string? PayloadJson { get; set; }
}