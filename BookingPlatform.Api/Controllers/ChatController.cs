using BookingPlatform.Contracts.Chat;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Chat;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using BookingPlatform.Api.Services;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/Chat")]
public sealed class ChatController : ApiControllerBase
{
    private readonly IHubContext<BusinessActivityHub> _businessActivityHub;
    private readonly IFirebasePushNotificationService _pushNotificationService;

    public ChatController(
        BookingDbContext dbContext,
        IHubContext<BusinessActivityHub> businessActivityHub,
        IFirebasePushNotificationService pushNotificationService)
        : base(dbContext)
    {
        _businessActivityHub = businessActivityHub;
        _pushNotificationService = pushNotificationService;
    }

    [HttpGet("business/{businessId:long}/conversations")]
    [ProducesResponseType(typeof(List<ChatConversationListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatConversationListItemDto>>> GetBusinessConversations(
        [FromRoute] long businessId,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var conversations = await DbContext.ChatConversations
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var businessCustomerIds = conversations
            .Where(x => x.BusinessCustomerId.HasValue)
            .Select(x => x.BusinessCustomerId!.Value)
            .Distinct()
            .ToList();

        var customers = await DbContext.BusinessCustomers
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && businessCustomerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = conversations.Select(x =>
        {
            customers.TryGetValue(x.BusinessCustomerId ?? 0, out var customer);

            return new ChatConversationListItemDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                BusinessCustomerId = x.BusinessCustomerId,
                CustomerProfileId = x.CustomerProfileId,
                AppUserId = x.AppUserId,
                CustomerName = customer?.FullName ?? "Klijent",
                CustomerPhone = customer?.Phone,
                CustomerEmail = customer?.Email,
                LastMessageAtUtc = x.LastMessageAtUtc,
                LastMessageText = x.LastMessageText,
                UnreadForBusinessCount = x.UnreadForBusinessCount,
                UnreadForCustomerCount = x.UnreadForCustomerCount
            };
        }).ToList();

        return Ok(result);
    }

    [HttpPost("business/{businessId:long}/conversations/start")]
    [ProducesResponseType(typeof(ChatConversationListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatConversationListItemDto>> StartBusinessConversation(
        [FromRoute] long businessId,
        [FromBody] StartChatConversationRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessWriteAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var customer = await DbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.BusinessCustomerId &&
                     x.BusinessId == businessId &&
                     x.IsActive,
                cancellationToken);

        if (customer is null)
            return BadRequest("Izabrani klijent ne postoji ili ne pripada ovoj radnji.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.BusinessCustomerId == request.BusinessCustomerId &&
                     x.IsActive,
                cancellationToken);

        var now = DateTime.UtcNow;

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                BusinessId = businessId,
                BusinessCustomerId = customer.Id,
                CustomerProfileId = customer.CustomerProfileId,
                AppUserId = customer.AppUserId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                IsActive = true
            };

            DbContext.ChatConversations.Add(conversation);
            await DbContext.SaveChangesAsync(cancellationToken);

        }

        return Ok(new ChatConversationListItemDto
        {
            Id = conversation.Id,
            BusinessId = conversation.BusinessId,
            BusinessCustomerId = conversation.BusinessCustomerId,
            CustomerProfileId = conversation.CustomerProfileId,
            AppUserId = conversation.AppUserId,
            CustomerName = customer.FullName,
            CustomerPhone = customer.Phone,
            CustomerEmail = customer.Email,
            LastMessageAtUtc = conversation.LastMessageAtUtc,
            LastMessageText = conversation.LastMessageText,
            UnreadForBusinessCount = conversation.UnreadForBusinessCount,
            UnreadForCustomerCount = conversation.UnreadForCustomerCount
        });
    }

    [HttpPost("customer/business/{businessId:long}/conversations/start")]
    [ProducesResponseType(typeof(ChatConversationListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatConversationListItemDto>> StartCustomerConversation(
    [FromRoute] long businessId,
    CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var customer = await DbContext.BusinessCustomers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.AppUserId == userId.Value &&
                     x.IsActive,
                cancellationToken);

        if (customer is null)
            return NotFound("Niste povezani sa ovom radnjom kao klijent.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.BusinessCustomerId == customer.Id &&
                     x.IsActive,
                cancellationToken);

        var now = DateTime.UtcNow;

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                BusinessId = businessId,
                BusinessCustomerId = customer.Id,
                CustomerProfileId = customer.CustomerProfileId,
                AppUserId = userId.Value,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                IsActive = true
            };

            DbContext.ChatConversations.Add(conversation);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new ChatConversationListItemDto
        {
            Id = conversation.Id,
            BusinessId = conversation.BusinessId,
            BusinessCustomerId = conversation.BusinessCustomerId,
            CustomerProfileId = conversation.CustomerProfileId,
            AppUserId = conversation.AppUserId,
            CustomerName = customer.FullName,
            CustomerPhone = customer.Phone,
            CustomerEmail = customer.Email,
            LastMessageAtUtc = conversation.LastMessageAtUtc,
            LastMessageText = conversation.LastMessageText,
            UnreadForBusinessCount = conversation.UnreadForBusinessCount,
            UnreadForCustomerCount = conversation.UnreadForCustomerCount
        });
    }

    [HttpGet("conversations/{conversationId:long}/messages")]
    [ProducesResponseType(typeof(List<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(
     [FromRoute] long conversationId,
     [FromQuery] int take = 50,
     CancellationToken cancellationToken = default)
    {
        var conversation = await DbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var isCustomerConversationOwner =
            conversation.AppUserId.HasValue &&
            conversation.AppUserId.Value == userId.Value;

        if (!isCustomerConversationOwner)
        {
            var accessResult = await EnsureBusinessReadAccessAsync(conversation.BusinessId, cancellationToken);
            if (accessResult is not null)
                return accessResult;
        }

        take = Math.Clamp(take, 1, 100);

        var rawMessages = await DbContext.ChatMessages
    .AsNoTracking()
    .Where(x => x.ConversationId == conversationId)
    .OrderByDescending(x => x.CreatedAtUtc)
    .Take(take)
    .OrderBy(x => x.CreatedAtUtc)
    .ToListAsync(cancellationToken);

        var changeRequestIds = rawMessages
            .Where(x => x.ChangeRequestId.HasValue)
            .Select(x => x.ChangeRequestId!.Value)
            .Distinct()
            .ToList();

        var changeRequestStatuses = await DbContext.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x => changeRequestIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => x.Status.ToString(),
                cancellationToken);

        var messages = rawMessages
            .Select(x =>
            {
                string? changeRequestStatus = null;

                if (x.ChangeRequestId.HasValue)
                {
                    changeRequestStatuses.TryGetValue(
                        x.ChangeRequestId.Value,
                        out changeRequestStatus);
                }

                return new ChatMessageDto
                {
                    Id = x.Id,
                    ConversationId = x.ConversationId,
                    SenderType = x.SenderType.ToString(),
                    SenderUserId = x.SenderUserId,
                    Text = x.Text,
                    ActionType = x.ActionType,
                    AppointmentId = x.AppointmentId,
                    ChangeRequestId = x.ChangeRequestId,
                    ChangeRequestStatus = changeRequestStatus,
                    CreatedAtUtc = x.CreatedAtUtc,
                    ReadByBusinessAtUtc = x.ReadByBusinessAtUtc,
                    ReadByCustomerAtUtc = x.ReadByCustomerAtUtc
                };
            })
            .ToList();

        return Ok(messages);
    }

    [HttpGet("customer/conversations")]
    [ProducesResponseType(typeof(List<ChatConversationListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ChatConversationListItemDto>>> GetCustomerConversations(
    CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var conversations = await DbContext.ChatConversations
            .AsNoTracking()
            .Where(x =>
                x.AppUserId == userId.Value &&
                x.IsActive)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var businessIds = conversations
            .Select(x => x.BusinessId)
            .Distinct()
            .ToList();

        var businesses = await DbContext.Businesses
            .AsNoTracking()
            .Where(x => businessIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = conversations.Select(x =>
        {
            businesses.TryGetValue(x.BusinessId, out var business);

            return new ChatConversationListItemDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                BusinessCustomerId = x.BusinessCustomerId,
                CustomerProfileId = x.CustomerProfileId,
                AppUserId = x.AppUserId,

                CustomerName = business?.Name ?? "Radnja",
                CustomerPhone = business?.Phone,
                CustomerEmail = business?.Email,

                LastMessageAtUtc = x.LastMessageAtUtc,
                LastMessageText = x.LastMessageText,
                UnreadForBusinessCount = x.UnreadForBusinessCount,
                UnreadForCustomerCount = x.UnreadForCustomerCount
            };
        }).ToList();

        return Ok(result);
    }

    [HttpPost("conversations/{conversationId:long}/messages/business")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageDto>> SendBusinessMessage(
    [FromRoute] long conversationId,
    [FromBody] SendChatMessageRequest request,
    CancellationToken cancellationToken)
    {
        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(conversation.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var text = request.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("Unesite tekst poruke.");

        if (text.Length > 2000)
            return BadRequest("Poruka može imati najviše 2000 karaktera.");

        var now = DateTime.UtcNow;
        var userId = TryGetCurrentUserId();

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.Business,
            SenderUserId = userId,
            Text = text,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ReadByBusinessAtUtc = now
        };

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = text.Length > 500 ? text[..500] : text;
        conversation.UnreadForCustomerCount += 1;
        conversation.UpdatedAtUtc = now;

        DbContext.ChatMessages.Add(message);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (conversation.AppUserId.HasValue)
        {
            await _pushNotificationService.SendToUserAsync(
                conversation.AppUserId.Value,
                "SmartChat",
                text.Length > 80 ? text[..80] + "..." : text,
                new Dictionary<string, string>
                {
                    ["type"] = "customerChat",
                    ["conversationId"] = conversation.Id.ToString(),
                    ["businessId"] = conversation.BusinessId.ToString()
                },
                cancellationToken);
        }

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(conversation.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = conversation.BusinessId,
                    conversationId = conversation.Id,
                    messageId = message.Id,
                    activityType = "ChatMessage"
                },
                cancellationToken);

        return Ok(new ChatMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType.ToString(),
            SenderUserId = message.SenderUserId,
            Text = message.Text,
            ActionType = message.ActionType,
            AppointmentId = message.AppointmentId,
            ChangeRequestId = message.ChangeRequestId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadByBusinessAtUtc = message.ReadByBusinessAtUtc,
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc
        });
    }

    [HttpPost("conversations/{conversationId:long}/messages/customer")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageDto>> SendCustomerMessage(
     [FromRoute] long conversationId,
     [FromBody] SendChatMessageRequest request,
     CancellationToken cancellationToken)
    {
        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        if (!conversation.AppUserId.HasValue || conversation.AppUserId.Value != userId.Value)
            return Forbid();

        var text = request.Text?.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("Unesite tekst poruke.");

        if (text.Length > 2000)
            return BadRequest("Poruka može imati najviše 2000 karaktera.");

        var now = DateTime.UtcNow;

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.Customer,
            SenderUserId = userId,
            Text = text,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ReadByCustomerAtUtc = now
        };

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = text.Length > 500 ? text[..500] : text;
        conversation.UnreadForBusinessCount += 1;
        conversation.UpdatedAtUtc = now;

        DbContext.ChatMessages.Add(message);
        await DbContext.SaveChangesAsync(cancellationToken);

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(conversation.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = conversation.BusinessId,
                    conversationId = conversation.Id,
                    messageId = message.Id,
                    activityType = "ChatMessage"
                },
                cancellationToken);

        await _pushNotificationService.SendToBusinessUsersAsync(
            conversation.BusinessId,
            "SmartChat",
            text.Length > 80 ? text[..80] + "..." : text,
            new Dictionary<string, string>
            {
                ["type"] = "businessChat",
                ["conversationId"] = conversation.Id.ToString(),
                ["businessId"] = conversation.BusinessId.ToString()
            },
            cancellationToken);

        return Ok(new ChatMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType.ToString(),
            SenderUserId = message.SenderUserId,
            Text = message.Text,
            ActionType = message.ActionType,
            AppointmentId = message.AppointmentId,
            ChangeRequestId = message.ChangeRequestId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadByBusinessAtUtc = message.ReadByBusinessAtUtc,
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc
        });
    }


    [HttpPost("conversations/{conversationId:long}/mark-read/business")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReadByBusiness(
        [FromRoute] long conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(conversation.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var now = DateTime.UtcNow;

        var unreadMessages = await DbContext.ChatMessages
            .Where(x =>
                x.ConversationId == conversationId &&
                (x.SenderType == ChatSenderType.Customer ||
                 x.SenderType == ChatSenderType.System) &&
                x.ReadByBusinessAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var message in unreadMessages)
        {
            message.ReadByBusinessAtUtc = now;
            message.UpdatedAtUtc = now;
        }

        conversation.UnreadForBusinessCount = 0;
        conversation.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("conversations/{conversationId:long}/mark-read/customer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkReadByCustomer(
    [FromRoute] long conversationId,
    CancellationToken cancellationToken)
    {
        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        if (!conversation.AppUserId.HasValue || conversation.AppUserId.Value != userId.Value)
            return Forbid();

        var now = DateTime.UtcNow;

        var unreadMessages = await DbContext.ChatMessages
            .Where(x =>
                x.ConversationId == conversationId &&
                (x.SenderType == ChatSenderType.Business ||
                 x.SenderType == ChatSenderType.System) &&
                x.ReadByCustomerAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var message in unreadMessages)
        {
            message.ReadByCustomerAtUtc = now;
            message.UpdatedAtUtc = now;
        }

        conversation.UnreadForCustomerCount = 0;
        conversation.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}