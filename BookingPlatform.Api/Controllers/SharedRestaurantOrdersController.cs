using BookingPlatform.Api.Hubs;
using BookingPlatform.Api.Services;
using BookingPlatform.Contracts.Chat;
using BookingPlatform.Contracts.CustomerPortal;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Chat;
using BookingPlatform.Domain.Customers;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/CustomerPortal/shared-restaurant-orders")]
public sealed class SharedRestaurantOrdersController : ApiControllerBase
{
    private const int ImmediateOrderDefaultOffsetMinutes = 1;
    private const int ScheduledOrderMinimumLeadTimeMin = 5;

    private readonly ISystemAlarmService _systemAlarmService;
    private readonly IHubContext<BusinessActivityHub> _businessActivityHub;
    private readonly IFirebasePushNotificationService _pushNotificationService;

    public SharedRestaurantOrdersController(
        BookingDbContext dbContext,
        ISystemAlarmService systemAlarmService,
        IHubContext<BusinessActivityHub> businessActivityHub,
        IFirebasePushNotificationService pushNotificationService)
        : base(dbContext)
    {
        _systemAlarmService = systemAlarmService;
        _businessActivityHub = businessActivityHub;
        _pushNotificationService = pushNotificationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SharedRestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SharedRestaurantOrderDto>> Create(
        [FromBody] CreateSharedRestaurantOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("Podaci deljene porudžbine su obavezni.");

        if (request.Items is null || request.Items.Count == 0)
            return BadRequest("Dodajte bar jednu stavku.");

        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var now = DateTime.UtcNow;
        var sharedOrder = new SharedRestaurantOrder
        {
            OwnerCustomerProfileId = customer.ProfileId,
            OwnerAppUserId = customer.AppUserId,
            OwnerDisplayNameSnapshot = customer.DisplayName,
            Title = NormalizeText(request.Title, 200),
            Note = NormalizeText(request.Note, 1000),
            Status = SharedRestaurantOrderStatus.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.SharedRestaurantOrders.Add(sharedOrder);

        foreach (var requestItem in request.Items)
        {
            var itemResult = await BuildSharedOrderItemAsync(
                sharedOrder,
                requestItem,
                customer,
                null,
                null,
                cancellationToken);

            if (itemResult.Error is not null)
                return BadRequest(itemResult.Error);

            sharedOrder.Items.Add(itemResult.Item!);
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToSharedRestaurantOrderDto(sharedOrder));
    }

    [HttpGet("{sharedOrderId:long}")]
    [ProducesResponseType(typeof(SharedRestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SharedRestaurantOrderDto>> GetById(
        [FromRoute] long sharedOrderId,
        CancellationToken cancellationToken)
    {
        var sharedOrder = await LoadSharedOrderAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Deljena porudžbina ne postoji.");

        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId &&
            sharedOrder.Items.All(x => x.AddedByCustomerProfileId != customer.ProfileId))
        {
            return Forbid();
        }

        return Ok(ToSharedRestaurantOrderDto(sharedOrder));
    }

    [HttpGet("my-active")]
    [ProducesResponseType(typeof(List<SharedRestaurantOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SharedRestaurantOrderDto>>> MyActive(
      CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var orders = await DbContext.SharedRestaurantOrders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x =>
                x.OwnerCustomerProfileId == customer.ProfileId &&
                x.Status == SharedRestaurantOrderStatus.Draft)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(ToSharedRestaurantOrderDto).ToList());
    }

    [HttpDelete("{sharedOrderId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSharedOrder(
    [FromRoute] long sharedOrderId,
    CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var sharedOrder = await LoadSharedOrderForUpdateAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Grupna porudžbina ne postoji.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId)
            return Forbid();

        if (sharedOrder.Status == SharedRestaurantOrderStatus.Submitted)
            return BadRequest("Grupna porudžbina je već poslata i ne može se obrisati.");

        sharedOrder.Status = SharedRestaurantOrderStatus.Cancelled;
        sharedOrder.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("my-history")]
    [ProducesResponseType(typeof(List<SharedRestaurantOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SharedRestaurantOrderDto>>> MyHistory(
        CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var orders = await DbContext.SharedRestaurantOrders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x =>
                x.OwnerCustomerProfileId == customer.ProfileId &&
                (
                    x.Status == SharedRestaurantOrderStatus.SentToChat ||
                    x.Status == SharedRestaurantOrderStatus.Submitted ||
                    x.Status == SharedRestaurantOrderStatus.Completed
                ))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(orders.Select(ToSharedRestaurantOrderDto).ToList());
    }

    [HttpDelete("{sharedOrderId:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(SharedRestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SharedRestaurantOrderDto>> DeleteSharedOrderItem(
    [FromRoute] long sharedOrderId,
    [FromRoute] long itemId,
    CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var sharedOrder = await LoadSharedOrderForUpdateAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Grupna porudžbina ne postoji.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId)
            return Forbid();

        if (sharedOrder.Status == SharedRestaurantOrderStatus.Submitted)
            return BadRequest("Grupna porudžbina je već poslata i ne može se menjati.");

        var item = sharedOrder.Items.FirstOrDefault(x => x.Id == itemId);

        if (item is null)
            return NotFound("Stavka ne postoji.");

        if (sharedOrder.Items.Count <= 1)
            return BadRequest("Ne možete obrisati poslednju stavku. Obrišite celu grupnu porudžbinu.");

        DbContext.SharedRestaurantOrderItemOptions.RemoveRange(item.Options);
        DbContext.SharedRestaurantOrderItems.Remove(item);

        sharedOrder.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadSharedOrderAsync(sharedOrderId, cancellationToken);

        return Ok(ToSharedRestaurantOrderDto(reloaded!));
    }

    [HttpPost("{sharedOrderId:long}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSharedOrder(
    [FromRoute] long sharedOrderId,
    CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var sharedOrder = await LoadSharedOrderForUpdateAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Grupna porudžbina ne postoji.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId)
            return Forbid();

        if (sharedOrder.Status != SharedRestaurantOrderStatus.Submitted)
            return BadRequest("Samo poslata grupna porudžbina može da se skloni kao završena.");

        sharedOrder.Status = SharedRestaurantOrderStatus.Completed;
        sharedOrder.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPut("{sharedOrderId:long}/items/{itemId:long}")]
    [ProducesResponseType(typeof(SharedRestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SharedRestaurantOrderDto>> UpdateSharedOrderItem(
        [FromRoute] long sharedOrderId,
        [FromRoute] long itemId,
        [FromBody] UpdateSharedRestaurantOrderItemRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("Podaci izmene su obavezni.");

        if (request.Quantity <= 0)
            return BadRequest("Količina mora biti veća od 0.");

        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var sharedOrder = await LoadSharedOrderForUpdateAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Grupna porudžbina ne postoji.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId)
            return Forbid();

        if (sharedOrder.Status == SharedRestaurantOrderStatus.Submitted)
            return BadRequest("Grupna porudžbina je već poslata i ne može se menjati.");

        var item = sharedOrder.Items.FirstOrDefault(x => x.Id == itemId);

        if (item is null)
            return NotFound("Stavka ne postoji.");

        var normalizedAddons = NormalizeAddonSelections(request.Addons);
        var addonResult = await LoadAddonsAsync(item.BusinessId, normalizedAddons, cancellationToken);

        if (addonResult.Error is not null)
            return BadRequest(addonResult.Error);

        var now = DateTime.UtcNow;
        var addonTotal = addonResult.Addons!.Sum(x => x.PriceDelta);

        item.Quantity = request.Quantity;
        item.OrderPersonName = NormalizeText(request.OrderPersonName, 200)
            ?? item.AddedByDisplayNameSnapshot
            ?? "Klijent";
        item.Note = NormalizeText(request.Note, 1000);
        item.UnitPriceSnapshot = item.UnitPriceSnapshot - item.Options.Sum(x => x.PriceDeltaSnapshot) + addonTotal;
        item.LineSubtotal = item.UnitPriceSnapshot * item.Quantity;
        item.UpdatedAtUtc = now;

        DbContext.SharedRestaurantOrderItemOptions.RemoveRange(item.Options);
        item.Options.Clear();

        item.Options = normalizedAddons
            .Select(selection =>
            {
                var addon = addonResult.AddonsById![selection.AddonId];

                return new SharedRestaurantOrderItemOption
                {
                    SharedRestaurantOrderItem = item,
                    RestaurantAddonId = addon.Id,
                    OptionNameSnapshot = addon.Name,
                    PriceDeltaSnapshot = addon.PriceDelta,
                    AmountMode = (RestaurantAddonAmountMode)selection.AmountMode,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
            })
            .ToList();

        sharedOrder.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        var reloaded = await LoadSharedOrderAsync(sharedOrderId, cancellationToken);

        return Ok(ToSharedRestaurantOrderDto(reloaded!));
    }

    [HttpPost("{sharedOrderId:long}/send-to-chat")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageDto>> SendToChat(
        [FromRoute] long sharedOrderId,
        [FromBody] SendSharedRestaurantOrderToChatRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.ConversationId <= 0)
            return BadRequest("Izaberite razgovor.");

        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var sharedOrder = await LoadSharedOrderForUpdateAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Deljena porudžbina ne postoji.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId)
            return Forbid();

        if (sharedOrder.Items.Count == 0)
            return BadRequest("Porudžbina nema stavke.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.Id == request.ConversationId && x.IsActive,
                cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        if (!await IsCustomerConversationParticipantAsync(conversation, customer.AppUserId, cancellationToken))
            return Forbid();

        var now = DateTime.UtcNow;
        var text = BuildSharedOrderInviteText(sharedOrder);

        var message = new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.Customer,
            SenderUserId = customer.AppUserId,
            Text = text,
            ActionType = "SharedRestaurantOrderInvite",
            SharedRestaurantOrderId = sharedOrder.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ReadByCustomerAtUtc = now
        };

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = text.Length > 500 ? text[..500] : text;
        conversation.UpdatedAtUtc = now;

        if (conversation.BusinessId > 0)
        {
            conversation.UnreadForBusinessCount += 1;
        }
        else
        {
            conversation.UnreadForCustomerCount += 1;
        }

        sharedOrder.Status = SharedRestaurantOrderStatus.SentToChat;
        sharedOrder.SentToChatAtUtc = now;
        sharedOrder.UpdatedAtUtc = now;

        DbContext.ChatMessages.Add(message);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (conversation.BusinessId > 0)
        {
            await _businessActivityHub.Clients
                .Group(BusinessActivityHub.BusinessGroupName(conversation.BusinessId))
                .SendAsync(
                    "BusinessActivityChanged",
                    new
                    {
                        businessId = conversation.BusinessId,
                        conversationId = conversation.Id,
                        messageId = message.Id,
                        sharedRestaurantOrderId = sharedOrder.Id,
                        activityType = "ChatMessage"
                    },
                    cancellationToken);

            await _pushNotificationService.SendToBusinessUsersAsync(
                conversation.BusinessId,
                "SmartChat",
                "Stigla je deljena porudžbina.",
                new Dictionary<string, string>
                {
                    ["type"] = "businessChat",
                    ["conversationId"] = conversation.Id.ToString(),
                    ["businessId"] = conversation.BusinessId.ToString(),
                    ["sharedRestaurantOrderId"] = sharedOrder.Id.ToString()
                },
                cancellationToken);
        }
        else
        {
            await NotifyChatParticipantsAsync(
                conversation,
                customer.AppUserId,
                "SmartChat",
                "Stigla je deljena porudžbina.",
                cancellationToken);
        }

        return Ok(ToChatMessageDto(message));
    }

    [HttpPost("chat-messages/{messageId:long}/accept")]
    [ProducesResponseType(typeof(SharedRestaurantOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SharedRestaurantOrderDto>> AcceptFromChat(
        [FromRoute] long messageId,
        [FromBody] AcceptSharedRestaurantOrderChatRequest? request,
        CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var message = await DbContext.ChatMessages
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message is null || message.SharedRestaurantOrderId is null)
            return NotFound("Poruka deljene porudžbine ne postoji.");

        if (!string.Equals(message.ActionType, "SharedRestaurantOrderInvite", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Ova poruka nije deljena porudžbina.");

        if (message.IsActionCompleted)
            return BadRequest("Ova porudžbina je već obrađena.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.Id == message.ConversationId && x.IsActive,
                cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        if (!await IsCustomerConversationParticipantAsync(conversation, customer.AppUserId, cancellationToken))
            return Forbid();

        if (message.SenderUserId == customer.AppUserId)
            return BadRequest("Ne možete prihvatiti porudžbinu koju ste vi poslali.");


        var sourceOrder = await LoadSharedOrderAsync(message.SharedRestaurantOrderId.Value, cancellationToken);

        if (sourceOrder is null)
            return NotFound("Deljena porudžbina ne postoji.");

        SharedRestaurantOrder? targetOrder = null;

        if (request?.CreateNew != true && request?.TargetSharedRestaurantOrderId is > 0)
        {
            targetOrder = await LoadSharedOrderForUpdateAsync(
                request.TargetSharedRestaurantOrderId.Value,
                cancellationToken);

            if (targetOrder is null)
                return NotFound("Ciljna grupna porudžbina ne postoji.");

            if (targetOrder.OwnerCustomerProfileId != customer.ProfileId)
                return Forbid();
        }

        if (targetOrder is null)
        {
            targetOrder = await DbContext.SharedRestaurantOrders
                .Include(x => x.Items)
                    .ThenInclude(x => x.Options)
                .Where(x =>
                    x.OwnerCustomerProfileId == customer.ProfileId &&
                    x.Status == SharedRestaurantOrderStatus.Draft)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var now = DateTime.UtcNow;

        if (targetOrder is null || request?.CreateNew == true)
        {
            targetOrder = new SharedRestaurantOrder
            {
                OwnerCustomerProfileId = customer.ProfileId,
                OwnerAppUserId = customer.AppUserId,
                OwnerDisplayNameSnapshot = customer.DisplayName,
                Title = "Grupna porudžbina",
                Status = SharedRestaurantOrderStatus.Draft,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            DbContext.SharedRestaurantOrders.Add(targetOrder);
        }

        foreach (var sourceItem in sourceOrder.Items)
        {
            var copiedItem = CopySharedOrderItem(sourceItem, targetOrder, message.Id, now);
            targetOrder.Items.Add(copiedItem);
        }

        message.IsActionCompleted = true;
        message.UpdatedAtUtc = now;
        targetOrder.UpdatedAtUtc = now;

        DbContext.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversation.Id,
            SenderType = ChatSenderType.System,
            Text = $"{customer.DisplayName} je dodao porudžbinu u grupnu korpu.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        conversation.LastMessageAtUtc = now;
        conversation.LastMessageText = $"{customer.DisplayName} je dodao porudžbinu u grupnu korpu.";
        conversation.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToSharedRestaurantOrderDto(targetOrder));
    }

    [HttpPost("chat-messages/{messageId:long}/decline")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageDto>> DeclineFromChat(
        [FromRoute] long messageId,
        CancellationToken cancellationToken)
    {
        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var message = await DbContext.ChatMessages
            .FirstOrDefaultAsync(x => x.Id == messageId, cancellationToken);

        if (message is null)
            return NotFound("Poruka ne postoji.");

        var conversation = await DbContext.ChatConversations
            .FirstOrDefaultAsync(
                x => x.Id == message.ConversationId && x.IsActive,
                cancellationToken);

        if (conversation is null)
            return NotFound("Razgovor ne postoji.");

        if (!await IsCustomerConversationParticipantAsync(conversation, customer.AppUserId, cancellationToken))
            return Forbid();

        if (message.SenderUserId == customer.AppUserId)
            return BadRequest("Ne možete odbiti porudžbinu koju ste vi poslali.");

        message.IsActionCompleted = true;
        message.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToChatMessageDto(message));
    }

    [HttpPost("{sharedOrderId:long}/submit")]
    [ProducesResponseType(typeof(SubmitSharedRestaurantOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmitSharedRestaurantOrderResponse>> Submit(
        [FromRoute] long sharedOrderId,
        [FromBody] SubmitSharedRestaurantOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest("Podaci slanja su obavezni.");

        var customer = await GetCurrentCustomerAsync(cancellationToken);

        if (customer is null)
            return Unauthorized("Korisnik nije prijavljen kao klijent.");

        var sharedOrder = await LoadSharedOrderForUpdateAsync(sharedOrderId, cancellationToken);

        if (sharedOrder is null)
            return NotFound("Deljena porudžbina ne postoji.");

        if (sharedOrder.OwnerCustomerProfileId != customer.ProfileId)
            return Forbid();

        if (sharedOrder.Items.Count == 0)
            return BadRequest("Grupna porudžbina nema stavke.");

        if (sharedOrder.Status == SharedRestaurantOrderStatus.Submitted)
            return BadRequest("Grupna porudžbina je već poslata.");

        var businessRequests = (request.Businesses ?? new List<SubmitSharedRestaurantOrderBusinessRequest>())
            .Where(x => x.BusinessId > 0)
            .GroupBy(x => x.BusinessId)
            .ToDictionary(x => x.Key, x => x.Last());

        var businessIds = sharedOrder.Items
            .Select(x => x.BusinessId)
            .Distinct()
            .ToList();

        foreach (var businessId in businessIds)
        {
            if (!businessRequests.ContainsKey(businessId))
                return BadRequest("Nedostaju uslovi slanja za jedan restoran.");
        }

        var savedOrders = new List<RestaurantOrder>();

        foreach (var businessId in businessIds)
        {
            var submitRequest = businessRequests[businessId];
            var items = sharedOrder.Items
                .Where(x => x.BusinessId == businessId)
                .ToList();

            var orderResult = await CreateRestaurantOrderFromSharedItemsAsync(
                businessId,
                submitRequest,
                items,
                customer,
                cancellationToken);

            if (orderResult.Error is not null)
                return BadRequest(orderResult.Error);

            savedOrders.Add(orderResult.Order!);
        }

        sharedOrder.Status = SharedRestaurantOrderStatus.Submitted;
        sharedOrder.SubmittedAtUtc = DateTime.UtcNow;
        sharedOrder.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        foreach (var order in savedOrders)
        {
            await NotifyBusinessSideAsync(
                order,
                "Nova grupna porudžbina",
                $"Klijent je poslao grupnu porudžbinu {FormatDisplayOrderNumber(order.DailyOrderNumber)}.",
                cancellationToken);
        }

        var orderIds = savedOrders.Select(x => x.Id).ToList();
        var loadedOrders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Guests)
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x => orderIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        return Ok(new SubmitSharedRestaurantOrderResponse
        {
            SharedRestaurantOrderId = sharedOrder.Id,
            Orders = loadedOrders.Select(ToRestaurantOrderDto).ToList()
        });
    }

    private async Task<CustomerContext?> GetCurrentCustomerAsync(CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return null;

        var user = await DbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);

        if (user is null)
            return null;

        var profile = await DbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AppUserId == user.Id, cancellationToken);

        if (profile is null)
            return null;

        return new CustomerContext(
            profile.Id,
            user.Id,
            BuildCustomerDisplayName(profile.Nickname, profile.FullName, profile.Phone, profile.Email ?? user.Email),
            profile.FullName ?? user.FullName,
            profile.Phone,
            profile.Email ?? user.Email);
    }

    private async Task<SharedRestaurantOrder?> LoadSharedOrderAsync(
        long sharedOrderId,
        CancellationToken cancellationToken)
    {
        return await DbContext.SharedRestaurantOrders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == sharedOrderId, cancellationToken);
    }

    private async Task<SharedRestaurantOrder?> LoadSharedOrderForUpdateAsync(
        long sharedOrderId,
        CancellationToken cancellationToken)
    {
        return await DbContext.SharedRestaurantOrders
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == sharedOrderId, cancellationToken);
    }

    private async Task<BuildSharedItemResult> BuildSharedOrderItemAsync(
        SharedRestaurantOrder sharedOrder,
        CreateSharedRestaurantOrderItemRequest request,
        CustomerContext customer,
        long? sourceSharedOrderId,
        long? sourceChatMessageId,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BuildSharedItemResult.Fail("businessId je obavezan.");

        if (request.MenuItemId <= 0)
            return BuildSharedItemResult.Fail("menuItemId je obavezan.");

        if (request.Quantity <= 0)
            return BuildSharedItemResult.Fail("Količina mora biti veća od 0.");

        var business = await DbContext.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.BusinessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return BuildSharedItemResult.Fail("Restoran ne postoji ili nije aktivan.");

        if (business.BusinessType is not (BusinessType.Restaurant or BusinessType.Cafe or BusinessType.FastFood))
            return BuildSharedItemResult.Fail("Izabrani biznis nije restoran, kafić ili brza hrana.");

        var menuItem = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.MenuItemId &&
                     x.BusinessId == request.BusinessId &&
                     x.IsActive &&
                     x.IsAvailable,
                cancellationToken);

        if (menuItem is null)
            return BuildSharedItemResult.Fail("Artikal ne postoji ili trenutno nije dostupan.");

        var normalizedAddons = NormalizeAddonSelections(request.Addons);
        var addonResult = await LoadAddonsAsync(request.BusinessId, normalizedAddons, cancellationToken);

        if (addonResult.Error is not null)
            return BuildSharedItemResult.Fail(addonResult.Error);

        var now = DateTime.UtcNow;
        var addonTotal = addonResult.Addons!.Sum(x => x.PriceDelta);
        var unitPrice = menuItem.Price + addonTotal;

        var item = new SharedRestaurantOrderItem
        {
            SharedRestaurantOrder = sharedOrder,
            BusinessId = business.Id,
            BusinessNameSnapshot = business.Name,
            AddedByCustomerProfileId = customer.ProfileId,
            AddedByAppUserId = customer.AppUserId,
            AddedByDisplayNameSnapshot = customer.DisplayName,
            OrderPersonName = NormalizeText(request.OrderPersonName, 200) ?? customer.DisplayName,
            MenuItemId = menuItem.Id,
            MenuItemNameSnapshot = menuItem.Name,
            UnitPriceSnapshot = unitPrice,
            Quantity = request.Quantity,
            LineSubtotal = unitPrice * request.Quantity,
            SendToKitchenSnapshot = menuItem.SendToKitchen,
            Note = NormalizeText(request.Note, 1000),
            SourceSharedRestaurantOrderId = sourceSharedOrderId,
            SourceChatMessageId = sourceChatMessageId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        item.Options = normalizedAddons
            .Select(selection =>
            {
                var addon = addonResult.AddonsById![selection.AddonId];

                return new SharedRestaurantOrderItemOption
                {
                    SharedRestaurantOrderItem = item,
                    RestaurantAddonId = addon.Id,
                    OptionNameSnapshot = addon.Name,
                    PriceDeltaSnapshot = addon.PriceDelta,
                    AmountMode = (RestaurantAddonAmountMode)selection.AmountMode,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
            })
            .ToList();

        return BuildSharedItemResult.Ok(item);
    }

    private async Task<CreateRestaurantOrderResult> CreateRestaurantOrderFromSharedItemsAsync(
        long businessId,
        SubmitSharedRestaurantOrderBusinessRequest request,
        List<SharedRestaurantOrderItem> sharedItems,
        CustomerContext customer,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(RestaurantOrderType), request.OrderType))
            return CreateRestaurantOrderResult.Fail("Nepoznat tip porudžbine.");

        var orderType = (RestaurantOrderType)request.OrderType;

        if (orderType is not (RestaurantOrderType.Takeaway or RestaurantOrderType.Delivery))
            return CreateRestaurantOrderResult.Fail("Grupna porudžbina može biti za poneti ili dostavu.");

        var business = await DbContext.Businesses
            .AsNoTracking()
            .Include(x => x.FeatureSettings)
            .FirstOrDefaultAsync(
                x => x.Id == businessId && x.IsActive,
                cancellationToken);

        if (business is null)
            return CreateRestaurantOrderResult.Fail("Restoran ne postoji ili nije aktivan.");

        if (business.BusinessType is not (BusinessType.Restaurant or BusinessType.Cafe or BusinessType.FastFood))
            return CreateRestaurantOrderResult.Fail("Izabrani biznis nije restoran, kafić ili brza hrana.");

        var featureSettings = business.FeatureSettings;

        if (featureSettings is not null)
        {
            if (!featureSettings.FoodOrdersEnabled && !featureSettings.DrinkOrdersEnabled)
                return CreateRestaurantOrderResult.Fail("Restoran trenutno ne prima porudžbine hrane ili pića.");

            if (orderType == RestaurantOrderType.Takeaway && !featureSettings.TakeawayOrdersEnabled)
                return CreateRestaurantOrderResult.Fail("Porudžbine za lično preuzimanje trenutno nisu uključene.");

            if (orderType == RestaurantOrderType.Delivery && !featureSettings.DeliveryOrdersEnabled)
                return CreateRestaurantOrderResult.Fail("Dostava trenutno nije uključena za ovaj restoran.");
        }

        var restaurantSettings = await DbContext.RestaurantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken)
            ?? CreateDefaultRestaurantSettings(businessId);

        if (orderType == RestaurantOrderType.Delivery)
        {
            if (!restaurantSettings.IsDeliveryEnabled)
                return CreateRestaurantOrderResult.Fail("Dostava trenutno nije uključena za ovaj restoran.");

            if (string.IsNullOrWhiteSpace(request.DeliveryAddress))
                return CreateRestaurantOrderResult.Fail("Za dostavu unesite adresu.");

            if (restaurantSettings.IsDeliveryLocationRequired &&
                (!request.DeliveryLatitude.HasValue || !request.DeliveryLongitude.HasValue))
            {
                return CreateRestaurantOrderResult.Fail("Za dostavu je obavezno poslati lokaciju.");
            }
        }

        var now = DateTime.UtcNow;
        var requestedPickupUtc = request.RequestedPickupAtUtc.HasValue
            ? EnsureUtc(request.RequestedPickupAtUtc.Value)
            : now.AddMinutes(ImmediateOrderDefaultOffsetMinutes);

        if (request.IsScheduledOrder)
        {
            if (!restaurantSettings.IsScheduledOrderingEnabled)
                return CreateRestaurantOrderResult.Fail("Zakazane porudžbine trenutno nisu uključene za ovaj restoran.");

            if (requestedPickupUtc < now.AddMinutes(ScheduledOrderMinimumLeadTimeMin))
            {
                return CreateRestaurantOrderResult.Fail(
                    $"Zakazana porudžbina mora biti najmanje {ScheduledOrderMinimumLeadTimeMin} minuta unapred.");
            }
        }
        else if (requestedPickupUtc < now.AddMinutes(-1))
        {
            return CreateRestaurantOrderResult.Fail("Vreme porudžbine ne može biti u prošlosti.");
        }

        RestaurantDeliveryZone? deliveryZone = null;

        if (orderType == RestaurantOrderType.Delivery)
        {
            if (!request.DeliveryZoneId.HasValue || request.DeliveryZoneId.Value <= 0)
                return CreateRestaurantOrderResult.Fail("Za dostavu izaberite zonu dostave.");

            deliveryZone = await DbContext.RestaurantDeliveryZones
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == request.DeliveryZoneId.Value &&
                         x.BusinessId == businessId &&
                         x.IsActive,
                    cancellationToken);

            if (deliveryZone is null)
                return CreateRestaurantOrderResult.Fail("Izabrana zona dostave ne postoji ili nije aktivna.");
        }

        await EnsureBusinessCustomerAsync(businessId, customer, now, cancellationToken);

        var orderDateLocal = DateOnly.FromDateTime(now.ToLocalTime());
        var dailyOrderNumber = await GetNextDailyOrderNumberAsync(businessId, orderDateLocal, cancellationToken);

        var order = new RestaurantOrder
        {
            BusinessId = businessId,
            OrderDateLocal = orderDateLocal,
            DailyOrderNumber = dailyOrderNumber,
            OrderType = orderType,
            OrderSource = RestaurantOrderSource.AndroidCustomer,
            RequestedPickupAtUtc = requestedPickupUtc,
            IsScheduledOrder = request.IsScheduledOrder,
            DeliveryAddress = NormalizeText(request.DeliveryAddress, 500),
            DeliveryZoneId = deliveryZone?.Id,
            DeliveryLatitude = request.DeliveryLatitude,
            DeliveryLongitude = request.DeliveryLongitude,
            DeliveryZoneNameSnapshot = deliveryZone?.Name,
            DeliveryFeeAmount = deliveryZone?.DeliveryFeeAmount ?? 0m,
            DeliveryMinimumOrderAmountSnapshot = deliveryZone?.MinimumOrderAmount ?? 0m,
            DeliveryNote = NormalizeText(request.DeliveryNote, 1000),
            CustomerName = NormalizeText(request.CustomerName, 200) ?? customer.FullName ?? customer.DisplayName,
            CustomerPhone = NormalizeText(request.CustomerPhone, 50) ?? customer.Phone,
            Note = BuildSubmittedOrderNote(request.Note),
            Status = RestaurantOrderStatus.Submitted,
            SubtotalAmount = 0m,
            TotalAmount = 0m,
            Currency = "RSD",
            SubmittedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantOrders.Add(order);

        var guestByName = new Dictionary<string, RestaurantOrderGuest>(StringComparer.OrdinalIgnoreCase);

        foreach (var sharedItem in sharedItems)
        {
            var guestName = NormalizeText(sharedItem.OrderPersonName, 200)
                ?? NormalizeText(sharedItem.AddedByDisplayNameSnapshot, 200)
                ?? "Klijent";

            if (!guestByName.TryGetValue(guestName, out var guest))
            {
                guest = new RestaurantOrderGuest
                {
                    Order = order,
                    Name = guestName,
                    DisplayOrder = guestByName.Count + 1,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                guestByName.Add(guestName, guest);
                order.Guests.Add(guest);
            }

            var itemRequest = new CreateCustomerRestaurantOrderItemRequest
            {
                MenuItemId = sharedItem.MenuItemId,
                Quantity = sharedItem.Quantity,
                Note = BuildSubmittedItemNote(guestName, sharedItem.Note),
                Addons = sharedItem.Options
                    .Where(x => x.RestaurantAddonId.HasValue)
                    .Select(x => new RestaurantOrderItemAddonSelectionDto
                    {
                        AddonId = x.RestaurantAddonId!.Value,
                        AmountMode = (int)x.AmountMode
                    })
                    .ToList()
            };

            var itemResult = await BuildRestaurantOrderItemAsync(order, itemRequest, cancellationToken);

            if (itemResult.Error is not null)
                return CreateRestaurantOrderResult.Fail(itemResult.Error);

            itemResult.Item!.OrderGuest = guest;
            order.Items.Add(itemResult.Item);
        }

        RecalculateOrderTotals(order);

        if (order.OrderType == RestaurantOrderType.Delivery &&
            order.DeliveryMinimumOrderAmountSnapshot > 0 &&
            order.SubtotalAmount < order.DeliveryMinimumOrderAmountSnapshot)
        {
            var missingAmount = order.DeliveryMinimumOrderAmountSnapshot - order.SubtotalAmount;

            return CreateRestaurantOrderResult.Fail(
                $"Minimalna porudžbina za dostavu u zoni {order.DeliveryZoneNameSnapshot} je {order.DeliveryMinimumOrderAmountSnapshot:0.##} {order.Currency}. " +
                $"Dodajte još {missingAmount:0.##} {order.Currency} ili izaberite lično preuzimanje.");
        }

        await AddRestaurantOrderMessageAsync(
            order,
            "Klijent je poslao grupnu porudžbinu iz aplikacije.",
            cancellationToken);

        return CreateRestaurantOrderResult.Ok(order);
    }

    private async Task<RestaurantOrderItemBuildResult> BuildRestaurantOrderItemAsync(
        RestaurantOrder order,
        CreateCustomerRestaurantOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MenuItemId <= 0)
            return RestaurantOrderItemBuildResult.Fail("menuItemId je obavezan.");

        if (request.Quantity <= 0)
            return RestaurantOrderItemBuildResult.Fail("Količina mora biti veća od 0.");

        var menuItem = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.MenuItemId &&
                     x.BusinessId == order.BusinessId &&
                     x.IsActive &&
                     x.IsAvailable,
                cancellationToken);

        if (menuItem is null)
            return RestaurantOrderItemBuildResult.Fail("Artikal ne postoji ili trenutno nije dostupan.");

        var normalizedAddons = NormalizeAddonSelections(request.Addons);
        var addonResult = await LoadAddonsAsync(order.BusinessId, normalizedAddons, cancellationToken);

        if (addonResult.Error is not null)
            return RestaurantOrderItemBuildResult.Fail(addonResult.Error);

        var addonTotal = addonResult.Addons!.Sum(x => x.PriceDelta);
        var unitPrice = menuItem.Price + addonTotal;
        var now = DateTime.UtcNow;

        var orderItem = new RestaurantOrderItem
        {
            Order = order,
            MenuItemId = menuItem.Id,
            MenuItemNameSnapshot = menuItem.Name,
            UnitPriceSnapshot = unitPrice,
            Quantity = request.Quantity,
            LineSubtotal = unitPrice * request.Quantity,
            SendToKitchenSnapshot = menuItem.SendToKitchen,
            Note = NormalizeText(request.Note, 1000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Options = normalizedAddons
                .Select(selection =>
                {
                    var addon = addonResult.AddonsById![selection.AddonId];

                    return new RestaurantOrderItemOption
                    {
                        RestaurantAddonId = addon.Id,
                        MenuItemOptionId = null,
                        OptionNameSnapshot = addon.Name,
                        PriceDeltaSnapshot = addon.PriceDelta,
                        AmountMode = (RestaurantAddonAmountMode)selection.AmountMode,
                        CreatedAtUtc = now,
                        UpdatedAtUtc = now
                    };
                })
                .ToList()
        };

        return RestaurantOrderItemBuildResult.Ok(orderItem);
    }

    private async Task<AddonLoadResult> LoadAddonsAsync(
        long businessId,
        List<RestaurantOrderItemAddonSelectionDto> normalizedSelections,
        CancellationToken cancellationToken)
    {
        foreach (var addonSelection in normalizedSelections)
        {
            if (!Enum.IsDefined(typeof(RestaurantAddonAmountMode), addonSelection.AmountMode))
                return AddonLoadResult.Fail("Nepoznata mera dodatka.");
        }

        var addonIds = normalizedSelections
            .Select(x => x.AddonId)
            .Distinct()
            .ToList();

        var selectedAddons = addonIds.Count == 0
            ? new List<RestaurantAddon>()
            : await DbContext.RestaurantAddons
                .AsNoTracking()
                .Where(x =>
                    addonIds.Contains(x.Id) &&
                    x.BusinessId == businessId &&
                    x.IsActive &&
                    x.IsAvailable &&
                    x.AddonGroup.IsActive)
                .ToListAsync(cancellationToken);

        if (selectedAddons.Count != addonIds.Count)
            return AddonLoadResult.Fail("Jedan ili više dodataka nisu dostupni.");

        return AddonLoadResult.Ok(selectedAddons);
    }

    private async Task EnsureBusinessCustomerAsync(
        long businessId,
        CustomerContext customer,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var businessCustomer = await DbContext.BusinessCustomers
            .FirstOrDefaultAsync(
                x => x.BusinessId == businessId &&
                     x.CustomerProfileId == customer.ProfileId,
                cancellationToken);

        if (businessCustomer is not null)
        {
            businessCustomer.IsActive = true;
            businessCustomer.AppUserId = customer.AppUserId;
            businessCustomer.FullName = customer.FullName ?? customer.DisplayName;
            businessCustomer.Phone = customer.Phone;
            businessCustomer.Email = customer.Email;
            businessCustomer.RemovedFromCustomerListAtUtc = null;
            businessCustomer.UpdatedAtUtc = now;
            return;
        }

        DbContext.BusinessCustomers.Add(new BusinessCustomer
        {
            BusinessId = businessId,
            CustomerProfileId = customer.ProfileId,
            AppUserId = customer.AppUserId,
            FullName = customer.FullName ?? customer.DisplayName,
            Phone = customer.Phone,
            Email = customer.Email,
            Notes = "Klijent je poslao grupnu porudžbinu.",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    private async Task AddRestaurantOrderMessageAsync(
        RestaurantOrder order,
        string text,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var recipientOperationUnitIds = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x => x.BusinessId == order.BusinessId && x.IsActive && x.ReceivesCustomerChat)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (recipientOperationUnitIds.Count == 0)
        {
            var kitchenId = await DbContext.RestaurantOperationUnits
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == order.BusinessId &&
                    x.UnitType == RestaurantOperationUnitType.Kitchen &&
                    x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (kitchenId.HasValue)
                recipientOperationUnitIds.Add(kitchenId.Value);
        }

        var message = new RestaurantOrderMessage
        {
            BusinessId = order.BusinessId,
            Order = order,
            SenderType = RestaurantOrderMessageSenderType.Customer,
            MessageType = RestaurantOrderMessageType.Text,
            Text = text,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var recipientOperationUnitId in recipientOperationUnitIds)
        {
            message.Recipients.Add(new RestaurantOrderMessageRecipient
            {
                BusinessId = order.BusinessId,
                RecipientType = RestaurantOrderMessageRecipientType.OperationUnit,
                RecipientOperationUnitId = recipientOperationUnitId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        DbContext.RestaurantOrderMessages.Add(message);
    }

    private async Task NotifyBusinessSideAsync(
        RestaurantOrder order,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        await _systemAlarmService.CreateRestaurantOrderNotificationAlarmAsync(
            order.BusinessId,
            order.Id,
            title,
            message,
            "restaurant_order_created",
            "open_restaurant_order",
            cancellationToken);

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(order.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = order.BusinessId,
                    orderId = order.Id,
                    orderType = (int)order.OrderType,
                    orderSource = (int)order.OrderSource,
                    status = (int)order.Status,
                    activityType = "RestaurantOrderCreated"
                },
                cancellationToken);
    }

    private async Task NotifyChatParticipantsAsync(
        ChatConversation conversation,
        long senderUserId,
        string title,
        string body,
        CancellationToken cancellationToken)
    {
        var participantUserIds = await GetCustomerConversationParticipantUserIdsAsync(
            conversation,
            cancellationToken);

        foreach (var participantUserId in participantUserIds.Where(x => x != senderUserId))
        {
            await _pushNotificationService.SendToUserAsync(
                participantUserId,
                title,
                body,
                new Dictionary<string, string>
                {
                    ["type"] = "customerChat",
                    ["conversationId"] = conversation.Id.ToString()
                },
                cancellationToken);
        }
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
            .Where(x => x.ConversationId == conversation.Id && x.IsActive && x.AppUserId.HasValue)
            .Select(x => x.AppUserId!.Value)
            .ToListAsync(cancellationToken);

        userIds.AddRange(memberUserIds);

        return userIds.Distinct().ToList();
    }

    private async Task<bool> IsCustomerConversationParticipantAsync(
        ChatConversation conversation,
        long appUserId,
        CancellationToken cancellationToken)
    {
        if (conversation.AppUserId == appUserId)
            return true;

        return await DbContext.ChatConversationMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.ConversationId == conversation.Id &&
                     x.AppUserId == appUserId &&
                     x.IsActive,
                cancellationToken);
    }

    private async Task<int> GetNextDailyOrderNumberAsync(
        long businessId,
        DateOnly orderDateLocal,
        CancellationToken cancellationToken)
    {
        var lastNumber = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.OrderDateLocal == orderDateLocal)
            .MaxAsync(x => (int?)x.DailyOrderNumber, cancellationToken);

        return (lastNumber ?? 0) + 1;
    }

    private static SharedRestaurantOrderItem CopySharedOrderItem(
        SharedRestaurantOrderItem sourceItem,
        SharedRestaurantOrder targetOrder,
        long sourceChatMessageId,
        DateTime now)
    {
        var item = new SharedRestaurantOrderItem
        {
            SharedRestaurantOrder = targetOrder,
            BusinessId = sourceItem.BusinessId,
            BusinessNameSnapshot = sourceItem.BusinessNameSnapshot,
            AddedByCustomerProfileId = sourceItem.AddedByCustomerProfileId,
            AddedByAppUserId = sourceItem.AddedByAppUserId,
            AddedByDisplayNameSnapshot = sourceItem.AddedByDisplayNameSnapshot,
            OrderPersonName = sourceItem.OrderPersonName,
            MenuItemId = sourceItem.MenuItemId,
            MenuItemNameSnapshot = sourceItem.MenuItemNameSnapshot,
            UnitPriceSnapshot = sourceItem.UnitPriceSnapshot,
            Quantity = sourceItem.Quantity,
            LineSubtotal = sourceItem.LineSubtotal,
            SendToKitchenSnapshot = sourceItem.SendToKitchenSnapshot,
            Note = sourceItem.Note,
            SourceSharedRestaurantOrderId = sourceItem.SharedRestaurantOrderId,
            SourceSharedRestaurantOrderItemId = sourceItem.Id,
            SourceChatMessageId = sourceChatMessageId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        item.Options = sourceItem.Options
            .Select(option => new SharedRestaurantOrderItemOption
            {
                SharedRestaurantOrderItem = item,
                RestaurantAddonId = option.RestaurantAddonId,
                OptionNameSnapshot = option.OptionNameSnapshot,
                PriceDeltaSnapshot = option.PriceDeltaSnapshot,
                AmountMode = option.AmountMode,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            })
            .ToList();

        return item;
    }

    private static string BuildSharedOrderInviteText(SharedRestaurantOrder sharedOrder)
    {
        var businessNames = sharedOrder.Items
            .Select(x => x.BusinessNameSnapshot)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var lines = sharedOrder.Items
            .Take(6)
            .Select(x =>
            {
                var person = !string.IsNullOrWhiteSpace(x.OrderPersonName)
                    ? $" za {x.OrderPersonName}"
                    : string.Empty;

                return $"{x.Quantity} x {x.MenuItemNameSnapshot}{person}";
            })
            .ToList();

        var moreText = sharedOrder.Items.Count > lines.Count
            ? $"\n+ još {sharedOrder.Items.Count - lines.Count} stavki"
            : string.Empty;

        return
            $"{sharedOrder.OwnerDisplayNameSnapshot} šalje porudžbinu.\n" +
            $"Biznis: {string.Join(", ", businessNames)}\n\n" +
            string.Join("\n", lines) +
            moreText +
            "\n\nDodati u grupnu porudžbinu?";
    }

    private static string? BuildSubmittedOrderNote(string? requestNote)
    {
        var note = NormalizeText(requestNote, 900);

        if (string.IsNullOrWhiteSpace(note))
            return "Grupna porudžbina iz aplikacije.";

        return $"Grupna porudžbina iz aplikacije.\n{note}";
    }

    private static string? BuildSubmittedItemNote(string personName, string? note)
    {
        var normalizedNote = NormalizeText(note, 900);

        if (string.IsNullOrWhiteSpace(normalizedNote))
            return $"Za: {personName}";

        return $"Za: {personName}\n{normalizedNote}";
    }

    private static List<RestaurantOrderItemAddonSelectionDto> NormalizeAddonSelections(
        List<RestaurantOrderItemAddonSelectionDto>? addons)
    {
        return (addons ?? new List<RestaurantOrderItemAddonSelectionDto>())
            .Where(x => x.AddonId > 0)
            .GroupBy(x => x.AddonId)
            .Select(x => x.Last())
            .ToList();
    }

    private static void RecalculateOrderTotals(RestaurantOrder order)
    {
        order.SubtotalAmount = order.Items.Sum(x => x.LineSubtotal);
        order.TotalAmount = order.SubtotalAmount +
            (order.OrderType == RestaurantOrderType.Delivery ? order.DeliveryFeeAmount : 0m);
        order.Currency = "RSD";
    }

    private static SharedRestaurantOrderDto ToSharedRestaurantOrderDto(SharedRestaurantOrder entity)
    {
        return new SharedRestaurantOrderDto
        {
            Id = entity.Id,
            OwnerCustomerProfileId = entity.OwnerCustomerProfileId,
            OwnerAppUserId = entity.OwnerAppUserId,
            OwnerDisplayName = entity.OwnerDisplayNameSnapshot,
            Title = entity.Title,
            Note = entity.Note,
            Status = (int)entity.Status,
            StatusText = GetSharedOrderStatusText(entity.Status),
            SubtotalAmount = entity.Items.Sum(x => x.LineSubtotal),
            Currency = "RSD",
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Items = entity.Items
                .OrderBy(x => x.BusinessNameSnapshot)
                .ThenBy(x => x.OrderPersonName)
                .ThenBy(x => x.Id)
                .Select(ToSharedRestaurantOrderItemDto)
                .ToList()
        };
    }

    private static SharedRestaurantOrderItemDto ToSharedRestaurantOrderItemDto(SharedRestaurantOrderItem item)
    {
        return new SharedRestaurantOrderItemDto
        {
            Id = item.Id,
            BusinessId = item.BusinessId,
            BusinessName = item.BusinessNameSnapshot,
            AddedByCustomerProfileId = item.AddedByCustomerProfileId,
            AddedByAppUserId = item.AddedByAppUserId,
            AddedByDisplayName = item.AddedByDisplayNameSnapshot,
            OrderPersonName = item.OrderPersonName,
            MenuItemId = item.MenuItemId,
            MenuItemName = item.MenuItemNameSnapshot,
            UnitPrice = item.UnitPriceSnapshot,
            Quantity = item.Quantity,
            LineSubtotal = item.LineSubtotal,
            Note = item.Note,
            Addons = item.Options
                .Select(x => new SharedRestaurantOrderItemAddonDto
                {
                    RestaurantAddonId = x.RestaurantAddonId,
                    Name = x.OptionNameSnapshot,
                    PriceDelta = x.PriceDeltaSnapshot,
                    AmountMode = (int)x.AmountMode,
                    AmountModeText = GetAddonAmountModeText(x.AmountMode)
                })
                .ToList()
        };
    }

    private static string GetAddonAmountModeText(RestaurantAddonAmountMode amountMode)
    {
        return amountMode switch
        {
            RestaurantAddonAmountMode.Less => "malo",
            RestaurantAddonAmountMode.More => "više",
            _ => "normalno"
        };
    }

    private static RestaurantOrderDto ToRestaurantOrderDto(RestaurantOrder entity)
    {
        return new RestaurantOrderDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            OrderDateLocal = entity.OrderDateLocal,
            DailyOrderNumber = entity.DailyOrderNumber,
            DisplayOrderNumberText = FormatDisplayOrderNumber(entity.DailyOrderNumber),
            OrderType = (int)entity.OrderType,
            OrderSource = (int)entity.OrderSource,
            RequestedPickupAtUtc = entity.RequestedPickupAtUtc,
            IsScheduledOrder = entity.IsScheduledOrder,
            DeliveryAddress = entity.DeliveryAddress,
            DeliveryNote = entity.DeliveryNote,
            DeliveryLatitude = entity.DeliveryLatitude,
            DeliveryLongitude = entity.DeliveryLongitude,
            DeliveryZoneId = entity.DeliveryZoneId,
            DeliveryZoneNameSnapshot = entity.DeliveryZoneNameSnapshot,
            DeliveryFeeAmount = entity.DeliveryFeeAmount,
            DeliveryMinimumOrderAmountSnapshot = entity.DeliveryMinimumOrderAmountSnapshot,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Note = entity.Note,
            Status = (int)entity.Status,
            SubtotalAmount = entity.SubtotalAmount,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            SubmittedAtUtc = entity.SubmittedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Guests = entity.Guests
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new RestaurantOrderGuestDto
                {
                    Id = x.Id,
                    OrderId = x.OrderId,
                    Name = x.Name,
                    DisplayOrder = x.DisplayOrder,
                    Note = x.Note
                })
                .ToList(),
            Items = entity.Items
                .Select(x => new RestaurantOrderItemDto
                {
                    Id = x.Id,
                    OrderId = x.OrderId,
                    OrderGuestId = x.OrderGuestId,
                    MenuItemId = x.MenuItemId,
                    MenuItemNameSnapshot = x.MenuItemNameSnapshot,
                    UnitPriceSnapshot = x.UnitPriceSnapshot,
                    Quantity = x.Quantity,
                    LineSubtotal = x.LineSubtotal,
                    SendToKitchenSnapshot = x.SendToKitchenSnapshot,
                    IsReady = x.IsReady,
                    ReadyAtUtc = x.ReadyAtUtc,
                    Note = x.Note,
                    Options = x.Options
                        .Select(option => new RestaurantOrderItemOptionDto
                        {
                            Id = option.Id,
                            OrderItemId = option.OrderItemId,
                            MenuItemOptionId = option.MenuItemOptionId,
                            RestaurantAddonId = option.RestaurantAddonId,
                            OptionNameSnapshot = option.OptionNameSnapshot,
                            PriceDeltaSnapshot = option.PriceDeltaSnapshot,
                            AmountMode = (int)option.AmountMode
                        })
                        .ToList()
                })
                .ToList()
        };
    }

    private static ChatMessageDto ToChatMessageDto(ChatMessage message)
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
            ReadByCustomerAtUtc = message.ReadByCustomerAtUtc
        };
    }

    private static RestaurantSettings CreateDefaultRestaurantSettings(long businessId)
    {
        return new RestaurantSettings
        {
            BusinessId = businessId,
            PreparationReminderBufferMin = 10,
            ScheduledOrderMinLeadTimeMin = 30,
            ScheduledOrderMaxDaysAhead = 7,
            IsScheduledOrderingEnabled = true,
            IsDeliveryEnabled = true,
            IsDeliveryLocationRequired = false
        };
    }

    private static string GetSharedOrderStatusText(SharedRestaurantOrderStatus status)
    {
        return status switch
        {
            SharedRestaurantOrderStatus.Draft => "U pripremi",
            SharedRestaurantOrderStatus.SentToChat => "Poslato osobi",
            SharedRestaurantOrderStatus.Submitted => "Poslato restoranima",
            SharedRestaurantOrderStatus.Completed => "Završeno",
            SharedRestaurantOrderStatus.Cancelled => "Otkazano",
            _ => status.ToString()
        };
    }

    private static string BuildCustomerDisplayName(
        string? nickname,
        string? fullName,
        string? phone,
        string? email)
    {
        return NormalizeText(nickname, 200)
            ?? NormalizeText(fullName, 200)
            ?? NormalizeText(phone, 200)
            ?? NormalizeText(email, 200)
            ?? "Klijent";
    }

    private static string FormatDisplayOrderNumber(int dailyOrderNumber)
    {
        return $"#{dailyOrderNumber}";
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? NormalizeText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }

    private sealed record CustomerContext(
        long ProfileId,
        long AppUserId,
        string DisplayName,
        string? FullName,
        string? Phone,
        string? Email);

    private sealed class BuildSharedItemResult
    {
        public SharedRestaurantOrderItem? Item { get; private init; }

        public string? Error { get; private init; }

        public static BuildSharedItemResult Ok(SharedRestaurantOrderItem item) => new() { Item = item };

        public static BuildSharedItemResult Fail(string error) => new() { Error = error };
    }

    private sealed class RestaurantOrderItemBuildResult
    {
        public RestaurantOrderItem? Item { get; private init; }

        public string? Error { get; private init; }

        public static RestaurantOrderItemBuildResult Ok(RestaurantOrderItem item) => new() { Item = item };

        public static RestaurantOrderItemBuildResult Fail(string error) => new() { Error = error };
    }

    private sealed class CreateRestaurantOrderResult
    {
        public RestaurantOrder? Order { get; private init; }

        public string? Error { get; private init; }

        public static CreateRestaurantOrderResult Ok(RestaurantOrder order) => new() { Order = order };

        public static CreateRestaurantOrderResult Fail(string error) => new() { Error = error };
    }

    private sealed class AddonLoadResult
    {
        public List<RestaurantAddon>? Addons { get; private init; }

        public Dictionary<long, RestaurantAddon>? AddonsById { get; private init; }

        public string? Error { get; private init; }

        public static AddonLoadResult Ok(List<RestaurantAddon> addons)
        {
            return new AddonLoadResult
            {
                Addons = addons,
                AddonsById = addons.ToDictionary(x => x.Id)
            };
        }

        public static AddonLoadResult Fail(string error) => new() { Error = error };
    }
}
