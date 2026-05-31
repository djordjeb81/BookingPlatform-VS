namespace BookingPlatform.Domain.SystemAlarms;

public enum SystemAlarmType
{
    GeneralReminder = 0,

    RestaurantNewOrder = 100,
    RestaurantPreparationStart = 101,
    RestaurantOrderLate = 102,
    RestaurantCustomerRejectedDelay = 103,
    RestaurantInternalUrgentMessage = 104,
    RestaurantTableShouldBeFree = 105,

    AppointmentUpcoming = 200,
    AppointmentApprovalWaiting = 201,
    AppointmentCustomerRejectedProposal = 202,

    ChatNewMessage = 300,
    ChatUrgentMessage = 301,

    TaxiVehicleShouldBeFree = 400,
    TaxiUrgentRequest = 401,

    GymMembershipExpiring = 500,
    GymBirthday = 501,

    BillingPaymentDue = 600
}