namespace BookingPlatform.Domain.Appointments;

public enum AppointmentChangeRequestType
{
    NewBookingRequest = 1,
    CounterProposal = 2,
    RescheduleRequest = 3,
    DelayProposal = 4
}