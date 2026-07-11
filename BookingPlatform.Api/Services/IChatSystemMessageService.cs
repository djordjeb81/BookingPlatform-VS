using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Restaurants;

namespace BookingPlatform.Api.Services;

public interface IChatSystemMessageService
{
    Task SendDelayProposalToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendTimeProposalToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendCustomerAcceptedProposalToBusinessAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendCustomerRejectedProposalToBusinessAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendCustomerRequestedNewBookingToBusinessAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendCustomerRequestedRescheduleToBusinessAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendAppointmentApprovedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendAppointmentRejectedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendCustomerCancelledAppointmentToBusinessAsync(
    Appointment appointment,
    string? reason,
    CancellationToken cancellationToken);

    Task SendCustomerWithdrawnAppointmentRequestToBusinessAsync(
        Appointment appointment,
        string? reason,
        CancellationToken cancellationToken);

    Task SendBusinessCancelledAppointmentToCustomerAsync(
    Appointment appointment,
    string? note,
    CancellationToken cancellationToken);

    Task SendDelayAcceptedToCustomerAsync(
    Appointment appointment,
    AppointmentChangeRequest changeRequest,
    CancellationToken cancellationToken);

    Task SendDelayRejectedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendRescheduleRequestAcceptedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendRescheduleRequestRejectedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken);

    Task SendRestaurantTableReservationApprovedOrderPromptToCustomerAsync(
    RestaurantTableReservation reservation,
    CancellationToken cancellationToken);
}