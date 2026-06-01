using BookingPlatform.Api.Hubs;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Chat;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Services;

public sealed class ChatSystemMessageService : IChatSystemMessageService
{
    private readonly BookingDbContext _dbContext;
    private readonly IFirebasePushNotificationService _pushNotificationService;
    private readonly IHubContext<BusinessActivityHub> _businessActivityHub;
    private readonly IConfiguration _configuration;
    private readonly ISystemAlarmService _systemAlarmService;

    public ChatSystemMessageService(
        BookingDbContext dbContext,
        IFirebasePushNotificationService pushNotificationService,
        IHubContext<BusinessActivityHub> businessActivityHub,
        IConfiguration configuration,
        ISystemAlarmService systemAlarmService)
    {
        _dbContext = dbContext;
        _pushNotificationService = pushNotificationService;
        _businessActivityHub = businessActivityHub;
        _configuration = configuration;
        _systemAlarmService = systemAlarmService;
    }

    public async Task SendDelayProposalToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Radnja predlaže odlaganje termina.\n\n" +
            $"Trenutni termin: {FormatDateTime(changeRequest.OriginalStartAtUtc)}\n" +
            $"Novi predlog: {FormatDateTime(changeRequest.ProposedStartAtUtc)}\n\n" +
            "Predlog možete prihvatiti ili odbiti ovde u chatu ili u detaljima termina.";

        if (!string.IsNullOrWhiteSpace(changeRequest.Message))
        {
            text += $"\n\nPoruka radnje: {changeRequest.Message.Trim()}";
        }

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Radnja predlaže odlaganje termina.",
            "appointmentDelayProposal",
            "AppointmentDelayProposal",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendTimeProposalToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Radnja predlaže novi termin.\n\n" +
            $"Trenutni termin: {FormatDateTime(changeRequest.OriginalStartAtUtc)}\n" +
            $"Novi predlog: {FormatDateTime(changeRequest.ProposedStartAtUtc)}\n\n" +
            "Predlog možete prihvatiti ili odbiti ovde u chatu ili u detaljima termina.";

        if (!string.IsNullOrWhiteSpace(changeRequest.Message))
        {
            text += $"\n\nPoruka radnje: {changeRequest.Message.Trim()}";
        }

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Radnja predlaže novi termin.",
            "appointmentTimeProposal",
            "AppointmentTimeProposal",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendCustomerAcceptedProposalToBusinessAsync(
     Appointment appointment,
     AppointmentChangeRequest changeRequest,
     CancellationToken cancellationToken)
    {
        var proposalName = GetProposalDisplayName(changeRequest);

        var text =
            $"Klijent je prihvatio {proposalName}.\n\n" +
            $"Novi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}";

        await SendSystemMessageToBusinessAsync(
            appointment,
            text,
            "Klijent je prihvatio predlog.",
            "appointmentProposalAccepted",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendCustomerRejectedProposalToBusinessAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var proposalName = GetProposalDisplayName(changeRequest);

        var text =
            $"Klijent je odbio {proposalName}.\n\n" +
            $"Predloženi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(changeRequest.Reason))
        {
            text += $"\n\nRazlog: {changeRequest.Reason.Trim()}";
        }

        await SendSystemMessageToBusinessAsync(
            appointment,
            text,
            "Klijent je odbio predlog.",
            "appointmentProposalRejected",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendCustomerRequestedNewBookingToBusinessAsync(
    Appointment appointment,
    AppointmentChangeRequest changeRequest,
    CancellationToken cancellationToken)
    {
        var text =
            "Klijent je poslao zahtev za novi termin.\n\n" +
            $"Predloženi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}\n\n" +
            "Zahtev možete prihvatiti ili odbiti u rasporedu.";

        if (!string.IsNullOrWhiteSpace(changeRequest.Message))
        {
            text += $"\n\nPoruka klijenta: {changeRequest.Message.Trim()}";
        }

        await SendSystemMessageToBusinessAsync(
            appointment,
            text,
            "Klijent je poslao zahtev za termin.",
            "newBookingRequest",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendCustomerRequestedRescheduleToBusinessAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Klijent traži promenu termina.\n\n" +
            $"Trenutni termin: {FormatDateTime(changeRequest.OriginalStartAtUtc)}\n" +
            $"Traženi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}\n\n" +
            "Zahtev možete prihvatiti ili odbiti u rasporedu.";

        if (!string.IsNullOrWhiteSpace(changeRequest.Message))
        {
            text += $"\n\nPoruka klijenta: {changeRequest.Message.Trim()}";
        }

        await SendSystemMessageToBusinessAsync(
            appointment,
            text,
            "Klijent traži promenu termina.",
            "appointmentRescheduleRequest",
            changeRequest.Id,
            cancellationToken);
    }



    public async Task SendCustomerCancelledAppointmentToBusinessAsync(
    Appointment appointment,
    string? reason,
    CancellationToken cancellationToken)
    {
        var text =
            "Klijent je otkazao potvrđen termin.\n\n" +
            $"Termin: {FormatDateTime(appointment.StartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(reason))
        {
            text += $"\n\nRazlog: {reason.Trim()}";
        }

        await SendSystemMessageToBusinessAsync(
            appointment,
            text,
            "Klijent je otkazao termin.",
            "appointmentCancelledByCustomer",
            null,
            cancellationToken);
    }

    public async Task SendBusinessCancelledAppointmentToCustomerAsync(
    Appointment appointment,
    string? note,
    CancellationToken cancellationToken)
    {
        var text =
            "Radnja je otkazala termin.\n\n" +
            $"Termin: {FormatDateTime(appointment.StartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(note))
        {
            text += $"\n\nNapomena radnje: {note.Trim()}";
        }

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Radnja je otkazala termin.",
            "appointmentCancelledByBusiness",
            "AppointmentInfo",
            null,
            cancellationToken);
    }

    public async Task SendDelayAcceptedToCustomerAsync(
    Appointment appointment,
    AppointmentChangeRequest changeRequest,
    CancellationToken cancellationToken)
    {
        var text =
            "Prihvaćeno je odlaganje termina.\n\n" +
            $"Novi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}";

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Odlaganje termina je prihvaćeno.",
            "appointmentDelayAccepted",
            "AppointmentInfo",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendDelayRejectedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Odbijeno je odlaganje termina.\n\n" +
            $"Termin ostaje: {FormatDateTime(appointment.StartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(changeRequest.Reason))
        {
            text += $"\n\nRazlog: {changeRequest.Reason.Trim()}";
        }

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Odlaganje termina je odbijeno.",
            "appointmentDelayRejected",
            "AppointmentInfo",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendRescheduleRequestAcceptedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Radnja je prihvatila promenu termina.\n\n" +
            $"Novi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}";

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Promena termina je prihvaćena.",
            "appointmentRescheduleAccepted",
            "AppointmentInfo",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendRescheduleRequestRejectedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Radnja je odbila promenu termina.\n\n" +
            $"Traženi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}\n" +
            $"Termin ostaje: {FormatDateTime(appointment.StartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(changeRequest.Reason))
        {
            text += $"\n\nRazlog: {changeRequest.Reason.Trim()}";
        }

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Promena termina je odbijena.",
            "appointmentRescheduleRejected",
            "AppointmentInfo",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendCustomerWithdrawnAppointmentRequestToBusinessAsync(
        Appointment appointment,
        string? reason,
        CancellationToken cancellationToken)
    {
        var text =
            "Klijent je povukao zahtev za termin.\n\n" +
            $"Traženi termin: {FormatDateTime(appointment.StartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(reason))
        {
            text += $"\n\nRazlog: {reason.Trim()}";
        }

        await SendSystemMessageToBusinessAsync(
            appointment,
            text,
            "Klijent je povukao zahtev za termin.",
            "appointmentRequestWithdrawnByCustomer",
            null,
            cancellationToken);
    }

    public async Task SendAppointmentApprovedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Vaš termin je potvrđen.\n\n" +
            $"Termin: {FormatDateTime(appointment.StartAtUtc)}";

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Termin je potvrđen.",
            "appointmentApproved",
            "AppointmentInfo",
            changeRequest.Id,
            cancellationToken);
    }

    public async Task SendAppointmentRejectedToCustomerAsync(
        Appointment appointment,
        AppointmentChangeRequest changeRequest,
        CancellationToken cancellationToken)
    {
        var text =
            "Vaš zahtev za termin je odbijen.\n\n" +
            $"Traženi termin: {FormatDateTime(changeRequest.ProposedStartAtUtc)}";

        if (!string.IsNullOrWhiteSpace(changeRequest.Reason))
        {
            text += $"\n\nRazlog: {changeRequest.Reason.Trim()}";
        }

        await SendSystemMessageToCustomerAsync(
            appointment,
            text,
            "Zahtev za termin je odbijen.",
            "appointmentRejected",
            "AppointmentInfo",
            changeRequest.Id,
            cancellationToken);
    }

    private async Task SendSystemMessageToCustomerAsync(
        Appointment appointment,
        string text,
        string pushTitle,
        string pushType,
        string actionType,
        long? changeRequestId,
        CancellationToken cancellationToken)
    {
        if (!appointment.BusinessCustomerId.HasValue)
            return;

        var customer = await _dbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.Id == appointment.BusinessCustomerId.Value &&
                     x.BusinessId == appointment.BusinessId &&
                     x.IsActive,
                cancellationToken);

        if (customer is null)
            return;

        var now = DateTime.UtcNow;

        var conversation = await _dbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.BusinessId == appointment.BusinessId &&
                     x.BusinessCustomerId == customer.Id &&
                     x.IsActive,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                BusinessId = appointment.BusinessId,
                BusinessCustomerId = customer.Id,
                CustomerProfileId = customer.CustomerProfileId,
                AppUserId = customer.AppUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                IsActive = true
            };

            _dbContext.ChatConversations.Add(conversation);
        }

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.System,
            SenderUserId = null,
            Text = text,
            ActionType = actionType,
            AppointmentId = appointment.Id,
            ChangeRequestId = changeRequestId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = text.Length > 500 ? text[..500] : text;
        conversation.UnreadForCustomerCount += 1;
        conversation.UpdatedAtUtc = now;

        _dbContext.ChatMessages.Add(message);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (conversation.AppUserId.HasValue)
        {
            await _pushNotificationService.SendToUserAsync(
                conversation.AppUserId.Value,
                "SmartChat",
                pushTitle,
                new Dictionary<string, string>
                {
                    ["type"] = pushType,
                    ["businessId"] = appointment.BusinessId.ToString(),
                    ["appointmentId"] = appointment.Id.ToString(),
                    ["changeRequestId"] = changeRequestId?.ToString() ?? "",
                    ["conversationId"] = conversation.Id.ToString()
                },
                cancellationToken);
        }

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(appointment.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = appointment.BusinessId,
                    appointmentId = appointment.Id,
                    changeRequestId,
                    conversationId = conversation.Id,
                    activityType = pushType
                },
                cancellationToken);
    }

    private async Task SendSystemMessageToBusinessAsync(
        Appointment appointment,
        string text,
        string pushTitle,
        string pushType,
        long? changeRequestId,
        CancellationToken cancellationToken)
    {
        if (!appointment.BusinessCustomerId.HasValue)
            return;

        var customer = await _dbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.Id == appointment.BusinessCustomerId.Value &&
                     x.BusinessId == appointment.BusinessId &&
                     x.IsActive,
                cancellationToken);

        if (customer is null)
            return;

        var now = DateTime.UtcNow;

        var conversation = await _dbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.BusinessId == appointment.BusinessId &&
                     x.BusinessCustomerId == customer.Id &&
                     x.IsActive,
                cancellationToken);

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                BusinessId = appointment.BusinessId,
                BusinessCustomerId = customer.Id,
                CustomerProfileId = customer.CustomerProfileId,
                AppUserId = customer.AppUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                IsActive = true
            };

            _dbContext.ChatConversations.Add(conversation);
        }

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.System,
            SenderUserId = null,
            Text = text,
            ActionType = null,
            AppointmentId = appointment.Id,
            ChangeRequestId = changeRequestId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ReadByCustomerAtUtc = now
        };

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = text.Length > 500 ? text[..500] : text;
        conversation.UnreadForBusinessCount += 1;
        conversation.UpdatedAtUtc = now;

        _dbContext.ChatMessages.Add(message);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (ShouldCreateUrgentChatAlarmForBusiness(pushType))
        {
            await _systemAlarmService.CreateChatUrgentMessageAlarmAsync(
                businessId: appointment.BusinessId,
                chatConversationId: conversation.Id,
                chatMessageId: message.Id,
                messagePreview: pushTitle,
                cancellationToken: cancellationToken);
        }


        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(appointment.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = appointment.BusinessId,
                    appointmentId = appointment.Id,
                    changeRequestId,
                    conversationId = conversation.Id,
                    activityType = pushType
                },
                cancellationToken);

        try
        {
            await _pushNotificationService.SendToBusinessUsersAsync(
                appointment.BusinessId,
                "SmartChat",
                pushTitle,
                new Dictionary<string, string>
                {
                    ["type"] = pushType,
                    ["businessId"] = appointment.BusinessId.ToString(),
                    ["appointmentId"] = appointment.Id.ToString(),
                    ["changeRequestId"] = changeRequestId?.ToString() ?? "",
                    ["conversationId"] = conversation.Id.ToString()
                },
                cancellationToken);
        }
        catch
        {
            // Push nije kritičan za Desktop osvežavanje.
            // Sistemsku poruku smo već upisali i SignalR smo već poslali.
        }
    }

    private static bool ShouldCreateUrgentChatAlarmForBusiness(string pushType)
    {
        return pushType is
            "newBookingRequest" or
            "appointmentRescheduleRequest" or
            "appointmentCancelledByCustomer" or
            "appointmentRequestWithdrawnByCustomer" or
            "appointmentProposalAccepted" or
            "appointmentProposalRejected";
    }

    private static string GetProposalDisplayName(AppointmentChangeRequest changeRequest)
    {
        return changeRequest.RequestType switch
        {
            AppointmentChangeRequestType.DelayProposal => "predlog odlaganja termina",
            AppointmentChangeRequestType.CounterProposal => "predlog novog termina",
            AppointmentChangeRequestType.RescheduleRequest => "zahtev za promenu termina",
            AppointmentChangeRequestType.NewBookingRequest => "zahtev za termin",
            _ => "predlog promene termina"
        };
    }

    private string FormatDateTime(DateTime utc)
    {
        var timeZoneId = _configuration["Scheduling:TimeZoneId"] ?? "Europe/Belgrade";

        TimeZoneInfo timeZone;

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            timeZone);

        return local.ToString("dd.MM.yyyy. HH:mm");
    }
}