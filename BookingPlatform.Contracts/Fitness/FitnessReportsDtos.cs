namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessDailyReportDto
{
    public long BusinessId { get; set; }

    public DateOnly Date { get; set; }

    public int SessionsCount { get; set; }

    public int BookingsCount { get; set; }

    public int PendingApprovalBookingsCount { get; set; }

    public int FullSessionsCount { get; set; }

    public int FreeSpotsCount { get; set; }

    public int ActiveMembersCount { get; set; }

    public int ActiveTrainingPassesCount { get; set; }

    public int TrainingPassesExpiringSoonCount { get; set; }

    public int ExpiredTrainingPassesCount { get; set; }

    public int OpenSessionDebtsCount { get; set; }

    public int UnreadChatCount { get; set; }

    public int OpenNotificationsCount { get; set; }

    public List<FitnessDailyReportSessionDto> UpcomingSessions { get; set; } = new();
}

public sealed class FitnessDailyReportSessionDto
{
    public long FitnessSessionId { get; set; }

    public string TimeText { get; set; } = "";

    public string RoomName { get; set; } = "";

    public string ClassName { get; set; } = "";

    public string SessionTypeText { get; set; } = "";

    public int Capacity { get; set; }

    public int BookedCount { get; set; }

    public string CapacityText { get; set; } = "";

    public bool IsFull { get; set; }
}

public sealed class FitnessInactiveMemberDto
{
    public long MemberId { get; set; }

    public string FullName { get; set; } = "";

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateTime? LastBookingStartAtUtc { get; set; }

    public int? InactiveDays { get; set; }

    public bool HasActiveTrainingPass { get; set; }

    public DateOnly? TrainingPassValidUntil { get; set; }

    public bool HasOpenDebt { get; set; }

    public long? BusinessCustomerId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? AppUserId { get; set; }

    public string StatusText { get; set; } = "";
}