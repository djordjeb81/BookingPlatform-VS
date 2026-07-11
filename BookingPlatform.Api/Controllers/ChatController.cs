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
using BookingPlatform.Domain.Customers;
using BookingPlatform.Domain.BusinessActivityNotifications;

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
    private readonly ISystemAlarmService _systemAlarmService;

    public ChatController(
        BookingDbContext dbContext,
        IHubContext<BusinessActivityHub> businessActivityHub,
        IFirebasePushNotificationService pushNotificationService,
        ISystemAlarmService systemAlarmService)
        : base(dbContext)
    {
        _businessActivityHub = businessActivityHub;
        _pushNotificationService = pushNotificationService;
        _systemAlarmService = systemAlarmService;
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
            .Include(x => x.CustomerProfile)
            .Where(x => x.BusinessId == businessId && businessCustomerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = conversations.Select(x =>
        {
            customers.TryGetValue(x.BusinessCustomerId ?? 0, out var customer);
            var displayName = BuildCustomerDisplayName(customer);

            return new ChatConversationListItemDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                BusinessCustomerId = x.BusinessCustomerId,
                CustomerProfileId = x.CustomerProfileId,
                AppUserId = x.AppUserId,
                CustomerName = displayName,
                CustomerDisplayName = displayName,
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
            .Include(x => x.CustomerProfile)
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
            CustomerName = BuildCustomerDisplayName(customer),
            CustomerDisplayName = BuildCustomerDisplayName(customer),
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
            .Include(x => x.CustomerProfile)
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.AppUserId == userId.Value &&
                     x.IsActive,
                cancellationToken);

        ChatConversation? conversation = null;

        if (customer is not null)
        {
            conversation = await DbContext.ChatConversations
                .FirstOrDefaultAsync(
                    x => x.BusinessId == businessId &&
                         x.BusinessCustomerId == customer.Id &&
                         x.IsActive,
                    cancellationToken);
        }

        if (customer is null)
        {
            conversation = await DbContext.ChatConversations
                .FirstOrDefaultAsync(
                    x => x.BusinessId == businessId &&
                         x.IsActive &&
                         DbContext.ChatConversationMembers.Any(member =>
                             member.ConversationId == x.Id &&
                             member.AppUserId == userId.Value &&
                             member.IsActive),
                    cancellationToken);

            if (conversation is null)
                return NotFound("Niste povezani sa ovom radnjom kao klijent.");
        }

        var now = DateTime.UtcNow;

        if (conversation is null && customer is not null)
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

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        return Ok(new ChatConversationListItemDto
        {
            Id = conversation.Id,
            BusinessId = conversation.BusinessId,
            BusinessCustomerId = conversation.BusinessCustomerId,
            CustomerProfileId = conversation.CustomerProfileId,
            AppUserId = conversation.AppUserId,
            CustomerName = customer is null ? "Radnja" : BuildCustomerDisplayName(customer),
            CustomerDisplayName = customer is null ? "Radnja" : BuildCustomerDisplayName(customer),
            CustomerPhone = customer?.Phone,
            CustomerEmail = customer?.Email,
            LastMessageAtUtc = conversation.LastMessageAtUtc,
            LastMessageText = conversation.LastMessageText,
            UnreadForBusinessCount = conversation.UnreadForBusinessCount,
            UnreadForCustomerCount = conversation.UnreadForCustomerCount
        });
    }

    [HttpGet("customer/search")]
    [ProducesResponseType(typeof(List<ChatSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ChatSearchResultDto>>> SearchCustomerChatTargets(
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var query = NormalizeText(q, 100);

        if (query is null || query.Length < 2)
            return BadRequest("Unesite najmanje 2 karaktera za pretragu.");

        var phoneDigits = NormalizePhoneDigits(query);
        var normalizedQuery = NormalizeSearchText(query);

        var businesses = await DbContext.Businesses
            .AsNoTracking()
            .Where(x =>
                x.IsActive &&
                (
                    EF.Functions.ILike(x.Name, $"%{query}%") ||
                    (x.City != null && EF.Functions.ILike(x.City, $"%{query}%")) ||
                    (x.Phone != null && EF.Functions.ILike(x.Phone, $"%{query}%")) ||
                    (x.Email != null && EF.Functions.ILike(x.Email, $"%{query}%")) ||
                    (phoneDigits.Length >= 3 && x.Phone != null)
                ))
            .OrderBy(x => x.Name)
            .Take(20)
            .ToListAsync(cancellationToken);

        var businessResults = businesses
            .Where(x =>
                ContainsNormalized(x.Name, normalizedQuery) ||
                ContainsNormalized(x.City, normalizedQuery) ||
                ContainsNormalized(x.Phone, normalizedQuery) ||
                ContainsNormalized(x.Email, normalizedQuery) ||
                PhoneContains(x.Phone, phoneDigits))
            .Take(10)
            .Select(x => new ChatSearchResultDto
            {
                TargetType = "Business",
                TargetId = x.Id,
                BusinessId = x.Id,
                DisplayName = x.Name,
                Subtitle = BuildBusinessSearchSubtitle(x.City, x.Street, x.StreetNumber),
                PhoneMasked = MaskPhone(x.Phone),
                EmailMasked = MaskEmail(x.Email),
                CanChat = true
            })
            .ToList();

        var profiles = await DbContext.CustomerProfiles
            .AsNoTracking()
            .Where(x =>
                x.AppUserId.HasValue &&
                x.AppUserId != userId.Value &&
                x.AllowChatDiscovery &&
                (
                    (x.Nickname != null && EF.Functions.ILike(x.Nickname, $"%{query}%")) ||
                    EF.Functions.ILike(x.FullName, $"%{query}%") ||
                    (x.Phone != null && EF.Functions.ILike(x.Phone, $"%{query}%")) ||
                    (x.Email != null && EF.Functions.ILike(x.Email, $"%{query}%")) ||
                    (phoneDigits.Length >= 3 && x.Phone != null)
                ))
            .OrderBy(x => x.Nickname ?? x.FullName)
            .Take(30)
            .ToListAsync(cancellationToken);

        var customerResults = profiles
            .Where(x =>
                ContainsNormalized(x.Nickname, normalizedQuery) ||
                ContainsNormalized(x.FullName, normalizedQuery) ||
                ContainsNormalized(x.Phone, normalizedQuery) ||
                ContainsNormalized(x.Email, normalizedQuery) ||
                PhoneContains(x.Phone, phoneDigits))
            .Take(10)
            .Select(x => new ChatSearchResultDto
            {
                TargetType = "Customer",
                TargetId = x.Id,
                CustomerProfileId = x.Id,
                AppUserId = x.AppUserId,
                DisplayName = BuildCustomerDisplayName(x),
                Subtitle = "Klijent",
                PhoneMasked = MaskPhone(x.Phone),
                EmailMasked = MaskEmail(x.Email),
                CanChat = true
            })
            .ToList();

        return Ok(businessResults
            .Concat(customerResults)
            .Take(20)
            .ToList());
    }

    [HttpPost("customer/conversations/start")]
    [ProducesResponseType(typeof(ChatConversationListItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatConversationListItemDto>> StartCustomerTargetConversation(
        [FromBody] StartCustomerTargetConversationRequest request,
        CancellationToken cancellationToken)
    {
        var targetType = NormalizeText(request.TargetType, 30);

        if (targetType is null || request.TargetId <= 0)
            return BadRequest("Izaberite sagovornika.");

        if (targetType.Equals("Business", StringComparison.OrdinalIgnoreCase))
            return await StartCustomerBusinessConversationAsync(request.TargetId, cancellationToken);

        if (targetType.Equals("Customer", StringComparison.OrdinalIgnoreCase))
            return await StartCustomerPrivateConversationAsync(request.TargetId, cancellationToken);

        return BadRequest("Nepoznat tip sagovornika.");
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

        var isCustomerConversationParticipant =
            await IsCustomerConversationParticipantAsync(conversation, userId.Value, cancellationToken);

        if (!isCustomerConversationParticipant)
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
                    RestaurantTableReservationId = x.RestaurantTableReservationId,
                    RestaurantOrderId = x.RestaurantOrderId,
                    SharedRestaurantOrderId = x.SharedRestaurantOrderId,
                    IsActionCompleted = x.IsActionCompleted,
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

        var memberConversationIds = await DbContext.ChatConversationMembers
            .AsNoTracking()
            .Where(x =>
                x.AppUserId == userId.Value &&
                x.IsActive)
            .Select(x => x.ConversationId)
            .ToListAsync(cancellationToken);

        var conversations = await DbContext.ChatConversations
            .AsNoTracking()
            .Where(x =>
                (x.AppUserId == userId.Value ||
                 memberConversationIds.Contains(x.Id)) &&
                x.IsActive)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var businessIds = conversations
            .Where(x => x.BusinessId > 0)
            .Select(x => x.BusinessId)
            .Distinct()
            .ToList();

        var businesses = await DbContext.Businesses
            .AsNoTracking()
            .Where(x => businessIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var directConversationIds = conversations
            .Where(x => x.BusinessId <= 0)
            .Select(x => x.Id)
            .ToList();

        var directMembers = await DbContext.ChatConversationMembers
            .AsNoTracking()
            .Where(x =>
                directConversationIds.Contains(x.ConversationId) &&
                x.IsActive)
            .ToListAsync(cancellationToken);

        var directOwnerProfileIds = conversations
            .Where(x => x.BusinessId <= 0 && x.CustomerProfileId.HasValue)
            .Select(x => x.CustomerProfileId!.Value)
            .Distinct()
            .ToList();

        var directOwnerProfiles = await DbContext.CustomerProfiles
            .AsNoTracking()
            .Where(x => directOwnerProfileIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var result = conversations.Select(x =>
        {
            businesses.TryGetValue(x.BusinessId, out var business);
            var isDirectCustomerChat = x.BusinessId <= 0 || business is null;
            var directDisplayName = ResolveDirectConversationDisplayName(
                x,
                userId.Value,
                directMembers,
                directOwnerProfiles);

            return new ChatConversationListItemDto
            {
                Id = x.Id,
                ConversationTargetType = isDirectCustomerChat ? "Customer" : "Business",
                BusinessId = x.BusinessId,
                BusinessCustomerId = x.BusinessCustomerId,
                CustomerProfileId = x.CustomerProfileId,
                AppUserId = x.AppUserId,

                CustomerName = isDirectCustomerChat ? directDisplayName : business?.Name ?? "Radnja",
                CustomerDisplayName = isDirectCustomerChat ? directDisplayName : business?.Name ?? "Radnja",
                CustomerPhone = isDirectCustomerChat ? null : business?.Phone,
                CustomerEmail = isDirectCustomerChat ? null : business?.Email,

                LastMessageAtUtc = x.LastMessageAtUtc,
                LastMessageText = x.LastMessageText,
                UnreadForBusinessCount = x.UnreadForBusinessCount,
                UnreadForCustomerCount = x.UnreadForCustomerCount
            };
        }).ToList();

        return Ok(result);
    }

    [HttpGet("conversations/{conversationId:long}/members")]
    [ProducesResponseType(typeof(List<ChatConversationMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ChatConversationMemberDto>>> GetConversationMembers(
        [FromRoute] long conversationId,
        CancellationToken cancellationToken)
    {
        var conversation = await DbContext.ChatConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var hasCustomerAccess = await IsCustomerConversationParticipantAsync(
            conversation,
            userId.Value,
            cancellationToken);

        if (!hasCustomerAccess)
        {
            var accessResult = await EnsureBusinessReadAccessAsync(conversation.BusinessId, cancellationToken);
            if (accessResult is not null)
                return accessResult;
        }

        var members = await DbContext.ChatConversationMembers
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId && x.IsActive)
            .OrderBy(x => x.DisplayNameSnapshot)
            .Select(x => ToMemberDto(x))
            .ToListAsync(cancellationToken);

        return Ok(members);
    }

    [HttpPost("conversations/{conversationId:long}/members/customer")]
    [ProducesResponseType(typeof(ChatConversationMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatConversationMemberDto>> AddConversationMemberByCustomer(
        [FromRoute] long conversationId,
        [FromBody] AddChatConversationMemberRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(x => x.Id == conversationId && x.IsActive, cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        if (!await IsCustomerConversationParticipantAsync(conversation, userId.Value, cancellationToken))
            return Forbid();

        if (request.CustomerProfileId <= 0)
            return BadRequest("Izaberite osobu.");

        var profile = await DbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.CustomerProfileId &&
                     x.AllowChatDiscovery &&
                     x.AppUserId.HasValue,
                cancellationToken);

        if (profile is null)
            return BadRequest("Osoba nije dostupna za dodavanje u chat.");

        if (profile.AppUserId == userId.Value)
            return BadRequest("Već ste u ovom razgovoru.");

        if (conversation.CustomerProfileId == profile.Id)
            return BadRequest("Osoba je već vlasnik ovog razgovora.");

        var existing = await DbContext.ChatConversationMembers
            .FirstOrDefaultAsync(
                x => x.ConversationId == conversation.Id &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                await DbContext.SaveChangesAsync(cancellationToken);
            }

            return Ok(ToMemberDto(existing));
        }

        var now = DateTime.UtcNow;
        var displayName = BuildCustomerDisplayName(profile);

        var member = new ChatConversationMember
        {
            ConversationId = conversation.Id,
            CustomerProfileId = profile.Id,
            AppUserId = profile.AppUserId,
            DisplayNameSnapshot = displayName,
            CreatedByAppUserId = userId.Value,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var systemText = $"{displayName} je dodat u razgovor.";
        var systemMessage = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.System,
            SenderUserId = null,
            Text = systemText,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = systemText;
        conversation.UnreadForBusinessCount += 1;
        conversation.UpdatedAtUtc = now;

        DbContext.ChatConversationMembers.Add(member);
        DbContext.ChatMessages.Add(systemMessage);

        await DbContext.SaveChangesAsync(cancellationToken);

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(conversation.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = conversation.BusinessId,
                    conversationId = conversation.Id,
                    memberId = member.Id,
                    activityType = "ChatMemberAdded"
                },
                cancellationToken);

        if (profile.AppUserId.HasValue)
        {
            await _pushNotificationService.SendToUserAsync(
                profile.AppUserId.Value,
                "SmartChat",
                "Dodati ste u razgovor.",
                new Dictionary<string, string>
                {
                    ["type"] = "customerChat",
                    ["businessId"] = conversation.BusinessId.ToString(),
                    ["conversationId"] = conversation.Id.ToString()
                },
                cancellationToken);
        }

        return Ok(ToMemberDto(member));
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

        var customerParticipantUserIds = await GetCustomerConversationParticipantUserIdsAsync(
            conversation,
            cancellationToken);

        foreach (var participantUserId in customerParticipantUserIds)
        {
            await _pushNotificationService.SendToUserAsync(
                participantUserId,
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
            RestaurantTableReservationId = message.RestaurantTableReservationId,
            RestaurantOrderId = message.RestaurantOrderId,
            SharedRestaurantOrderId = message.SharedRestaurantOrderId,
            IsActionCompleted = message.IsActionCompleted,
            ChangeRequestId = message.ChangeRequestId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadByBusinessAtUtc = message.ReadByBusinessAtUtc,
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc
        });
    }

    [HttpPost("business/{businessId:long}/broadcast")]
    [ProducesResponseType(typeof(SendBusinessBroadcastMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SendBusinessBroadcastMessageResponse>> SendBusinessBroadcastMessage(
    [FromRoute] long businessId,
    [FromBody] SendBusinessBroadcastMessageRequest request,
    CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessWriteAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var title = NormalizeText(request.Title, 120);
        var text = NormalizeText(request.Text, 1800);

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Unesite naslov obaveštenja.");

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("Unesite tekst obaveštenja.");

        var fullText = BuildBroadcastMessageText(
            title,
            text,
            request.ValidFromUtc,
            request.ValidToUtc);

        if (fullText.Length > 2000)
            return BadRequest("Obaveštenje je predugačko. Skratite naslov ili tekst.");

        var customersQuery = DbContext.BusinessCustomers
            .Include(x => x.CustomerProfile)
            .Where(x =>
                x.BusinessId == businessId &&
                x.AppUserId.HasValue);

        if (request.OnlyActiveCustomers)
        {
            customersQuery = customersQuery.Where(x => x.IsActive);
        }

        var customers = await customersQuery
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return Ok(new SendBusinessBroadcastMessageResponse
            {
                BusinessId = businessId,
                TargetCount = 0,
                SentCount = 0,
                SkippedCount = 0,
                Message = "Nema povezanih članova kojima može da se pošalje chat poruka."
            });
        }

        var now = DateTime.UtcNow;
        var sentCount = 0;
        var skippedCount = 0;

        foreach (var customer in customers)
        {
            if (!customer.AppUserId.HasValue)
            {
                skippedCount++;
                continue;
            }

            var conversation = await DbContext.ChatConversations
                .FirstOrDefaultAsync(
                    x => x.BusinessId == businessId &&
                         x.BusinessCustomerId == customer.Id &&
                         x.IsActive,
                    cancellationToken);

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

            var message = new ChatMessage
            {
                ConversationId = conversation.Id,
                SenderType = ChatSenderType.System,
                SenderUserId = null,
                Text = fullText,
                ActionType = "business_broadcast",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                ReadByBusinessAtUtc = now
            };

            conversation.LastMessageAtUtc = now;
            conversation.LastMessageText = fullText.Length > 500 ? fullText[..500] : fullText;
            conversation.UnreadForCustomerCount += 1;
            conversation.UpdatedAtUtc = now;

            DbContext.ChatMessages.Add(message);
            sentCount++;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        foreach (var customer in customers.Where(x => x.AppUserId.HasValue))
        {
            await _pushNotificationService.SendToUserAsync(
                customer.AppUserId!.Value,
                title,
                text.Length > 80 ? text[..80] + "..." : text,
                new Dictionary<string, string>
                {
                    ["type"] = "businessBroadcast",
                    ["businessId"] = businessId.ToString()
                },
                cancellationToken);
        }

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(businessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId,
                    activityType = "BusinessBroadcastMessage"
                },
                cancellationToken);

        return Ok(new SendBusinessBroadcastMessageResponse
        {
            BusinessId = businessId,
            TargetCount = customers.Count,
            SentCount = sentCount,
            SkippedCount = skippedCount,
            Message = $"Obaveštenje je poslato. Poslato: {sentCount}, preskočeno: {skippedCount}."
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

        if (!await IsCustomerConversationParticipantAsync(conversation, userId.Value, cancellationToken))
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

        if (conversation.BusinessId > 0)
        {
            conversation.UnreadForBusinessCount += 1;
        }
        else
        {
            conversation.UnreadForCustomerCount += 1;
        }

        conversation.UpdatedAtUtc = now;

        DbContext.ChatMessages.Add(message);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (conversation.BusinessId > 0)
        {
            await UpsertBusinessUnreadChatNotificationAsync(
                conversation,
                message,
                cancellationToken);
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
                cancellationToken,
                excludedAppUserId: userId.Value);
        }
        else
        {
            var participantUserIds = await GetCustomerConversationParticipantUserIdsAsync(
                conversation,
                cancellationToken);

            foreach (var participantUserId in participantUserIds.Where(x => x != userId.Value))
            {
                await _pushNotificationService.SendToUserAsync(
                    participantUserId,
                    "SmartChat",
                    text.Length > 80 ? text[..80] + "..." : text,
                    new Dictionary<string, string>
                    {
                        ["type"] = "customerChat",
                        ["conversationId"] = conversation.Id.ToString()
                    },
                    cancellationToken);
            }
        }

        return Ok(new ChatMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType.ToString(),
            SenderUserId = message.SenderUserId,
            Text = message.Text,
            ActionType = message.ActionType,
            AppointmentId = message.AppointmentId,
            RestaurantTableReservationId = message.RestaurantTableReservationId,
            RestaurantOrderId = message.RestaurantOrderId,
            SharedRestaurantOrderId = message.SharedRestaurantOrderId,
            IsActionCompleted = message.IsActionCompleted,
            ChangeRequestId = message.ChangeRequestId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadByBusinessAtUtc = message.ReadByBusinessAtUtc,
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc
        });
    }

    [HttpPost("conversations/{conversationId:long}/messages/customer/urgent")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageDto>> SendCustomerUrgentMessage(
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

        if (!await IsCustomerConversationParticipantAsync(conversation, userId.Value, cancellationToken))
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

        await _systemAlarmService.CreateChatUrgentMessageAlarmAsync(
            businessId: conversation.BusinessId,
            chatConversationId: conversation.Id,
            chatMessageId: message.Id,
            messagePreview: text,
            cancellationToken: cancellationToken);

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(conversation.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = conversation.BusinessId,
                    conversationId = conversation.Id,
                    messageId = message.Id,
                    activityType = "ChatUrgentMessage"
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
            RestaurantTableReservationId = message.RestaurantTableReservationId,
            RestaurantOrderId = message.RestaurantOrderId,
            SharedRestaurantOrderId = message.SharedRestaurantOrderId,
            IsActionCompleted = message.IsActionCompleted,
            ChangeRequestId = message.ChangeRequestId,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadByBusinessAtUtc = message.ReadByBusinessAtUtc,
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc
        });
    }

    [HttpPost("messages/{messageId:long}/complete-action/customer")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatMessageDto>> CompleteCustomerMessageAction(
    [FromRoute] long messageId,
    [FromBody] CompleteChatMessageActionRequest request,
    CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var message = await DbContext.ChatMessages
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message is null)
            return NotFound("Poruka ne postoji.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.Id == message.ConversationId &&
                     x.IsActive,
                cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        if (!await IsCustomerConversationParticipantAsync(conversation, userId.Value, cancellationToken))
            return Forbid();

        if (string.IsNullOrWhiteSpace(message.ActionType))
            return BadRequest("Poruka nema aktivnu akciju.");

        if (message.IsActionCompleted)
            return Ok(ToChatMessageDto(message, null));

        var normalizedResult = string.IsNullOrWhiteSpace(request.Result)
            ? null
            : request.Result.Trim();

        message.IsActionCompleted = true;
        message.UpdatedAtUtc = DateTime.UtcNow;

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
                    actionType = message.ActionType,
                    actionResult = normalizedResult,
                    activityType = "ChatMessageActionCompleted"
                },
                cancellationToken);

        return Ok(ToChatMessageDto(message, null));
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

        await ResolveBusinessUnreadChatNotificationAsync(
            conversation.BusinessId,
            conversation.Id,
            now,
            cancellationToken);

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

    private async Task UpsertBusinessUnreadChatNotificationAsync(
        ChatConversation conversation,
        ChatMessage message,
        CancellationToken cancellationToken)
    {
        var businessCustomer = conversation.BusinessCustomerId.HasValue
            ? await DbContext.BusinessCustomers
                .AsNoTracking()
                .Include(x => x.CustomerProfile)
                .FirstOrDefaultAsync(
                    x => x.Id == conversation.BusinessCustomerId.Value,
                    cancellationToken)
            : null;

        var customerProfile = businessCustomer?.CustomerProfile;

        if (customerProfile is null && conversation.CustomerProfileId.HasValue)
        {
            customerProfile = await DbContext.CustomerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == conversation.CustomerProfileId.Value,
                    cancellationToken);
        }

        var customerName =
            businessCustomer is not null
                ? BuildCustomerDisplayName(businessCustomer)
                : customerProfile is not null
                    ? BuildCustomerDisplayName(customerProfile)
                    : "Klijent";

        var previewText = NormalizeText(message.Text, 500) ?? "-";
        var activityKey = BusinessUnreadChatNotificationKey(conversation.Id);

        var notification = await DbContext.BusinessActivityNotifications
            .FirstOrDefaultAsync(
                x => x.BusinessId == conversation.BusinessId &&
                     x.RecipientKey == "business" &&
                     x.ActivityKey == activityKey,
                cancellationToken);

        var nowUtc = DateTime.UtcNow;

        if (notification is null)
        {
            notification = new BusinessActivityNotification
            {
                BusinessId = conversation.BusinessId,
                RecipientType = BusinessActivityNotificationRecipients.Business,
                RecipientKey = "business",
                Domain = BusinessActivityNotificationDomains.Chat,
                Kind = BusinessActivityNotificationKinds.UnreadChatMessage,
                ActivityKey = activityKey,
                CreatedAtUtc = nowUtc
            };

            DbContext.BusinessActivityNotifications.Add(notification);
        }

        notification.RecipientType = BusinessActivityNotificationRecipients.Business;
        notification.RecipientKey = "business";
        notification.Domain = BusinessActivityNotificationDomains.Chat;
        notification.Kind = BusinessActivityNotificationKinds.UnreadChatMessage;
        notification.Title = "Nova poruka";
        notification.MainText = customerName;
        notification.PreviewText = previewText;
        notification.Priority = 90;
        notification.SortAtUtc = message.CreatedAtUtc;
        notification.ConversationId = conversation.Id;
        notification.ChatMessageId = message.Id;
        notification.CustomerProfileId = conversation.CustomerProfileId ?? customerProfile?.Id;
        notification.BusinessCustomerId = conversation.BusinessCustomerId;
        notification.CustomerName = customerName;
        notification.CustomerPhone =
            NormalizeText(businessCustomer?.Phone, 50) ??
            NormalizeText(customerProfile?.Phone, 50);
        notification.IsSeen = false;
        notification.SeenAtUtc = null;
        notification.SeenByUserId = null;
        notification.IsResolved = false;
        notification.ResolvedAtUtc = null;
        notification.ResolvedByUserId = null;
        notification.SnoozedUntilUtc = null;
        notification.SnoozedByUserId = null;
        notification.UpdatedAtUtc = nowUtc;
    }

    private async Task ResolveBusinessUnreadChatNotificationAsync(
        long businessId,
        long conversationId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var activityKey = BusinessUnreadChatNotificationKey(conversationId);

        var notification = await DbContext.BusinessActivityNotifications
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.RecipientKey == "business" &&
                     x.ActivityKey == activityKey,
                cancellationToken);

        if (notification is null)
            return;

        notification.IsResolved = true;
        notification.IsSeen = true;
        notification.ResolvedAtUtc = nowUtc;
        notification.SeenAtUtc ??= nowUtc;
        notification.SnoozedUntilUtc = null;
        notification.UpdatedAtUtc = nowUtc;
    }

    private static string BusinessUnreadChatNotificationKey(long conversationId)
    {
        return $"chat.unread.business:{conversationId}";
    }

    private static ChatMessageDto ToChatMessageDto(
    ChatMessage message,
    string? changeRequestStatus)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderType = message.SenderType.ToString(),
            SenderUserId = message.SenderUserId,
            Text = message.Text,
            ActionType = message.ActionType,
            AppointmentId = message.AppointmentId,
            ChangeRequestId = message.ChangeRequestId,
            RestaurantTableReservationId = message.RestaurantTableReservationId,
            RestaurantOrderId = message.RestaurantOrderId,
            SharedRestaurantOrderId = message.SharedRestaurantOrderId,
            IsActionCompleted = message.IsActionCompleted,
            CreatedAtUtc = message.CreatedAtUtc,
            ReadByBusinessAtUtc = message.ReadByBusinessAtUtc,
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc,
            ChangeRequestStatus = changeRequestStatus
        };
    }

    private static ChatConversationMemberDto ToMemberDto(ChatConversationMember member)
    {
        return new ChatConversationMemberDto
        {
            Id = member.Id,
            ConversationId = member.ConversationId,
            CustomerProfileId = member.CustomerProfileId,
            AppUserId = member.AppUserId,
            DisplayName = member.DisplayNameSnapshot,
            IsActive = member.IsActive,
            CreatedAtUtc = member.CreatedAtUtc
        };
    }

    private async Task<ActionResult<ChatConversationListItemDto>> StartCustomerBusinessConversationAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var business = await DbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessId && x.IsActive, cancellationToken);

        if (business is null)
            return NotFound("Radnja ne postoji ili nije aktivna.");

        var profile = await GetOrCreateCustomerProfileAsync(userId.Value, cancellationToken);
        var businessCustomer = await GetOrCreateBusinessCustomerAsync(
            businessId,
            profile,
            userId.Value,
            cancellationToken);

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.BusinessCustomerId == businessCustomer.Id &&
                     x.IsActive,
                cancellationToken);

        var now = DateTime.UtcNow;

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                BusinessId = businessId,
                BusinessCustomerId = businessCustomer.Id,
                CustomerProfileId = profile.Id,
                AppUserId = userId.Value,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                IsActive = true
            };

            DbContext.ChatConversations.Add(conversation);
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return new ChatConversationListItemDto
        {
            Id = conversation.Id,
            ConversationTargetType = "Business",
            BusinessId = conversation.BusinessId,
            BusinessCustomerId = conversation.BusinessCustomerId,
            CustomerProfileId = conversation.CustomerProfileId,
            AppUserId = conversation.AppUserId,
            CustomerName = business.Name,
            CustomerDisplayName = business.Name,
            CustomerPhone = business.Phone,
            CustomerEmail = business.Email,
            LastMessageAtUtc = conversation.LastMessageAtUtc,
            LastMessageText = conversation.LastMessageText,
            UnreadForBusinessCount = conversation.UnreadForBusinessCount,
            UnreadForCustomerCount = conversation.UnreadForCustomerCount
        };
    }

    private async Task<ActionResult<ChatConversationListItemDto>> StartCustomerPrivateConversationAsync(
        long targetCustomerProfileId,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return Unauthorized("Korisnik nije prijavljen.");

        var ownerProfile = await GetOrCreateCustomerProfileAsync(userId.Value, cancellationToken);

        var targetProfile = await DbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == targetCustomerProfileId &&
                     x.AllowChatDiscovery &&
                     x.AppUserId.HasValue,
                cancellationToken);

        if (targetProfile is null)
            return NotFound("Klijent nije dostupan za chat.");

        if (targetProfile.AppUserId == userId.Value || targetProfile.Id == ownerProfile.Id)
            return BadRequest("Ne možete otvoriti chat sami sa sobom.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.BusinessId == 0 &&
                     x.IsActive &&
                     (
                         x.AppUserId == userId.Value &&
                         DbContext.ChatConversationMembers.Any(member =>
                             member.ConversationId == x.Id &&
                             member.CustomerProfileId == targetProfile.Id &&
                             member.IsActive)
                         ||
                         x.AppUserId == targetProfile.AppUserId &&
                         DbContext.ChatConversationMembers.Any(member =>
                             member.ConversationId == x.Id &&
                             member.CustomerProfileId == ownerProfile.Id &&
                             member.IsActive)
                     ),
                cancellationToken);

        var now = DateTime.UtcNow;

        if (conversation is null)
        {
            conversation = new ChatConversation
            {
                BusinessId = 0,
                BusinessCustomerId = null,
                CustomerProfileId = ownerProfile.Id,
                AppUserId = userId.Value,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                IsActive = true
            };

            DbContext.ChatConversations.Add(conversation);
            await DbContext.SaveChangesAsync(cancellationToken);

            DbContext.ChatConversationMembers.Add(new ChatConversationMember
            {
                ConversationId = conversation.Id,
                CustomerProfileId = targetProfile.Id,
                AppUserId = targetProfile.AppUserId,
                DisplayNameSnapshot = BuildCustomerDisplayName(targetProfile),
                CreatedByAppUserId = userId.Value,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            await DbContext.SaveChangesAsync(cancellationToken);
        }

        var displayName = BuildCustomerDisplayName(targetProfile);

        return new ChatConversationListItemDto
        {
            Id = conversation.Id,
            ConversationTargetType = "Customer",
            BusinessId = 0,
            BusinessCustomerId = null,
            CustomerProfileId = targetProfile.Id,
            AppUserId = targetProfile.AppUserId,
            CustomerName = displayName,
            CustomerDisplayName = displayName,
            CustomerPhone = null,
            CustomerEmail = null,
            LastMessageAtUtc = conversation.LastMessageAtUtc,
            LastMessageText = conversation.LastMessageText,
            UnreadForBusinessCount = conversation.UnreadForBusinessCount,
            UnreadForCustomerCount = conversation.UnreadForCustomerCount
        };
    }

    private async Task<CustomerProfile> GetOrCreateCustomerProfileAsync(
        long appUserId,
        CancellationToken cancellationToken)
    {
        var profile = await DbContext.CustomerProfiles
            .FirstOrDefaultAsync(x => x.AppUserId == appUserId, cancellationToken);

        if (profile is not null)
            return profile;

        var user = await DbContext.AppUsers
            .AsNoTracking()
            .FirstAsync(x => x.Id == appUserId, cancellationToken);

        var now = DateTime.UtcNow;

        profile = new CustomerProfile
        {
            AppUserId = appUserId,
            FullName = NormalizeText(user.FullName, 200)
                ?? NormalizeText(user.Email, 256)
                ?? "Klijent",
            Email = user.Email,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.CustomerProfiles.Add(profile);
        await DbContext.SaveChangesAsync(cancellationToken);

        return profile;
    }

    private async Task<BusinessCustomer> GetOrCreateBusinessCustomerAsync(
        long businessId,
        CustomerProfile profile,
        long appUserId,
        CancellationToken cancellationToken)
    {
        var businessCustomer = await DbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == profile.Id,
                cancellationToken);

        var now = DateTime.UtcNow;

        if (businessCustomer is not null)
        {
            if (!businessCustomer.IsActive)
            {
                businessCustomer.IsActive = true;
                businessCustomer.RemovedFromCustomerListAtUtc = null;
                businessCustomer.UpdatedAtUtc = now;
                await DbContext.SaveChangesAsync(cancellationToken);
            }

            return businessCustomer;
        }

        businessCustomer = new BusinessCustomer
        {
            BusinessId = businessId,
            CustomerProfileId = profile.Id,
            AppUserId = appUserId,
            FullName = BuildCustomerDisplayName(profile),
            Phone = profile.Phone,
            Email = profile.Email,
            Notes = "Klijent je otvorio SmartChat.",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.BusinessCustomers.Add(businessCustomer);
        await DbContext.SaveChangesAsync(cancellationToken);

        return businessCustomer;
    }

    private static string ResolveDirectConversationDisplayName(
        ChatConversation conversation,
        long currentUserId,
        List<ChatConversationMember> members,
        Dictionary<long, CustomerProfile> ownerProfiles)
    {
        if (conversation.AppUserId == currentUserId)
        {
            var targetMember = members
                .Where(x => x.ConversationId == conversation.Id)
                .FirstOrDefault(x => x.AppUserId != currentUserId);

            return NormalizeText(targetMember?.DisplayNameSnapshot, 200) ?? "Klijent";
        }

        if (conversation.CustomerProfileId.HasValue &&
            ownerProfiles.TryGetValue(conversation.CustomerProfileId.Value, out var ownerProfile))
        {
            return BuildCustomerDisplayName(ownerProfile);
        }

        return "Klijent";
    }

    private static string? BuildBusinessSearchSubtitle(
        string? city,
        string? street,
        string? streetNumber)
    {
        var address = string.Join(
            " ",
            new[]
            {
                NormalizeText(street, 200),
                NormalizeText(streetNumber, 40)
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.Join(
            " • ",
            new[]
            {
                NormalizeText(city, 120),
                NormalizeText(address, 260)
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private async Task<bool> IsCustomerConversationParticipantAsync(
        ChatConversation conversation,
        long appUserId,
        CancellationToken cancellationToken)
    {
        if (conversation.AppUserId.HasValue && conversation.AppUserId.Value == appUserId)
            return true;

        return await DbContext.ChatConversationMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.ConversationId == conversation.Id &&
                     x.AppUserId == appUserId &&
                     x.IsActive,
                cancellationToken);
    }

    private async Task<List<long>> GetCustomerConversationParticipantUserIdsAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        var userIds = new List<long>();

        if (conversation.AppUserId.HasValue)
            userIds.Add(conversation.AppUserId.Value);

        var memberUserIds = await DbContext.ChatConversationMembers
            .AsNoTracking()
            .Where(x =>
                x.ConversationId == conversation.Id &&
                x.AppUserId.HasValue &&
                x.IsActive)
            .Select(x => x.AppUserId!.Value)
            .ToListAsync(cancellationToken);

        userIds.AddRange(memberUserIds);

        return userIds
            .Distinct()
            .ToList();
    }

    private static string BuildCustomerDisplayName(BookingPlatform.Domain.Customers.BusinessCustomer? customer)
    {
        return NormalizeText(customer?.CustomerProfile?.Nickname, 80)
            ?? NormalizeText(customer?.CustomerProfile?.FullName, 200)
            ?? NormalizeText(customer?.CustomerProfile?.Phone, 50)
            ?? NormalizeText(customer?.CustomerProfile?.Email, 256)
            ?? NormalizeText(customer?.FullName, 200)
            ?? NormalizeText(customer?.Phone, 50)
            ?? NormalizeText(customer?.Email, 256)
            ?? "Klijent";
    }

    private static string BuildCustomerDisplayName(BookingPlatform.Domain.Customers.CustomerProfile profile)
    {
        return NormalizeText(profile.Nickname, 80)
            ?? NormalizeText(profile.FullName, 200)
            ?? NormalizeText(profile.Phone, 50)
            ?? NormalizeText(profile.Email, 256)
            ?? "Klijent";
    }

    private static bool ContainsNormalized(string? value, string normalizedQuery)
    {
        var normalizedValue = NormalizeSearchText(value);

        return normalizedValue.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchText(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizePhoneDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return new string(value.Where(char.IsDigit).ToArray());
    }

    private static bool PhoneContains(string? phone, string phoneDigits)
    {
        if (phoneDigits.Length < 3)
            return false;

        return NormalizePhoneDigits(phone).Contains(phoneDigits, StringComparison.OrdinalIgnoreCase);
    }

    private static string? MaskPhone(string? phone)
    {
        var digits = NormalizePhoneDigits(phone);

        if (digits.Length < 5)
            return null;

        return $"{digits[..3]}***{digits[^2..]}";
    }

    private static string? MaskEmail(string? email)
    {
        email = NormalizeText(email, 256);

        if (email is null)
            return null;

        var atIndex = email.IndexOf('@');

        if (atIndex <= 1)
            return "***";

        return $"{email[0]}***{email[atIndex..]}";
    }

    private static string BuildBroadcastMessageText(
    string title,
    string text,
    DateTime? validFromUtc,
    DateTime? validToUtc)
    {
        var result =
            "[OBAVEŠTENJE]\n\n" +
            title.Trim() +
            "\n\n" +
            text.Trim();

        if (validFromUtc.HasValue || validToUtc.HasValue)
        {
            result += "\n\n";

            if (validFromUtc.HasValue && validToUtc.HasValue)
            {
                result += $"Važi od: {validFromUtc.Value:dd.MM.yyyy. HH:mm} do {validToUtc.Value:dd.MM.yyyy. HH:mm}";
            }
            else if (validFromUtc.HasValue)
            {
                result += $"Važi od: {validFromUtc.Value:dd.MM.yyyy. HH:mm}";
            }
            else if (validToUtc.HasValue)
            {
                result += $"Važi do: {validToUtc.Value:dd.MM.yyyy. HH:mm}";
            }
        }

        return result;
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            normalized = normalized[..maxLength];

        return normalized;
    }
}
