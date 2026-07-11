namespace BookingPlatform.Domain.BusinessActivityNotifications;

public static class BusinessActivityNotificationRecipients
{
    public const string Business = "Business";
    public const string Customer = "Customer";
    public const string Staff = "Staff";
    public const string OperationUnit = "OperationUnit";
    public const string System = "System";
}

public static class BusinessActivityNotificationDomains
{
    public const string Salon = "Salon";
    public const string Restaurant = "Restaurant";
    public const string Fitness = "Fitness";
    public const string Chat = "Chat";
    public const string System = "System";
    public const string Taxi = "Taxi";
    public const string Hotel = "Hotel";
    public const string Service = "Service";
}

public static class BusinessActivityNotificationKinds
{
    public const string PendingApproval = "PendingApproval";
    public const string MembershipWarning = "MembershipWarning";
    public const string OpenDebt = "OpenDebt";

    public const string NewOrder = "NewOrder";
    public const string TableReservationRequest = "TableReservationRequest";
    public const string AreaReservationRequest = "AreaReservationRequest";

    public const string AppointmentRequest = "AppointmentRequest";
    public const string ChangeRequest = "ChangeRequest";

    public const string UnreadChatMessage = "UnreadChatMessage";
    public const string SystemAlarm = "SystemAlarm";
}