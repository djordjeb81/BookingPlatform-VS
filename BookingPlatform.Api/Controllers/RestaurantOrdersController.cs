using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace BookingPlatform.Api.Controllers;




[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantOrdersController : ApiControllerBase
{
    private readonly IHubContext<BusinessActivityHub> _businessActivityHub;

    public RestaurantOrdersController(
        BookingDbContext dbContext,
        IHubContext<BusinessActivityHub> businessActivityHub)
        : base(dbContext)
    {
        _businessActivityHub = businessActivityHub;
    }

    [HttpGet]
    public async Task<ActionResult<List<RestaurantOrderDto>>> GetAll(
    [FromQuery] long businessId,
    [FromQuery] long? tableSessionId,
    [FromQuery] long? restaurantAreaId,
    [FromQuery] long? tableResourceId,
    [FromQuery] int? status,
    [FromQuery] int? orderType,
    [FromQuery] DateTime? fromUtc,
    [FromQuery] DateTime? toUtc,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x => x.BusinessId == businessId);

        if (tableSessionId.HasValue)
            query = query.Where(x => x.TableSessionId == tableSessionId.Value);

        if (restaurantAreaId.HasValue)
            query = query.Where(x => x.RestaurantAreaId == restaurantAreaId.Value);

        if (tableResourceId.HasValue)
            query = query.Where(x => x.TableResourceId == tableResourceId.Value);

        if (status.HasValue)
        {
            if (!Enum.IsDefined(typeof(RestaurantOrderStatus), status.Value))
                return BadRequest("Nepoznat status narudžbine.");

            query = query.Where(x => x.Status == (RestaurantOrderStatus)status.Value);
        }

        if (orderType.HasValue)
        {
            if (!Enum.IsDefined(typeof(RestaurantOrderType), orderType.Value))
                return BadRequest("Nepoznat tip narudžbine.");

            query = query.Where(x => x.OrderType == (RestaurantOrderType)orderType.Value);
        }

        if (fromUtc.HasValue)
        {
            var from = EnsureUtc(fromUtc.Value);
            query = query.Where(x => x.CreatedAtUtc >= from);
        }

        if (toUtc.HasValue)
        {
            var to = EnsureUtc(toUtc.Value);
            query = query.Where(x => x.CreatedAtUtc < to);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(300)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("kitchen-board")]
    public async Task<ActionResult<List<RestaurantKitchenBoardOrderDto>>> GetKitchenBoard(
    [FromQuery] long businessId,
    [FromQuery] long? restaurantAreaId,
    [FromQuery] int? orderType,
    CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (orderType.HasValue && !Enum.IsDefined(typeof(RestaurantOrderType), orderType.Value))
            return BadRequest("Nepoznat tip narudžbine.");

        var query = DbContext.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Guests)
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
.Where(x =>
    x.BusinessId == businessId &&
    (
x.Status == RestaurantOrderStatus.Submitted ||
x.Status == RestaurantOrderStatus.Preparing ||
x.Status == RestaurantOrderStatus.Ready ||
x.Status == RestaurantOrderStatus.Served ||
(
    x.Status == RestaurantOrderStatus.Cancelled &&
    x.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.Rejected
)
    ));

        if (restaurantAreaId.HasValue)
            query = query.Where(x => x.RestaurantAreaId == restaurantAreaId.Value);

        if (orderType.HasValue)
            query = query.Where(x => x.OrderType == (RestaurantOrderType)orderType.Value);

        var orders = await query
            .OrderBy(x => x.RequestedPickupAtUtc ?? x.SubmittedAtUtc ?? x.CreatedAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        var tableResourceIds = orders
    .Where(x => x.TableResourceId.HasValue)
    .Select(x => x.TableResourceId!.Value)
    .Distinct()
    .ToList();

        var tableNamesById = tableResourceIds.Count == 0
            ? new Dictionary<long, string>()
            : await DbContext.Resources
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == businessId &&
                    tableResourceIds.Contains(x.Id))
                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.Name,
                    cancellationToken);

        var result = orders
            .Where(order =>
                order.OrderSource == RestaurantOrderSource.KitchenDesk ||
                order.Items.Any(item => item.SendToKitchenSnapshot))
            .Select(order => new RestaurantKitchenBoardOrderDto
            {
                OrderId = order.Id,
                OrderDateLocal = order.OrderDateLocal,
                DailyOrderNumber = order.DailyOrderNumber,
                DisplayOrderNumberText = FormatDisplayOrderNumber(order.DailyOrderNumber),
                BusinessId = order.BusinessId,
                RestaurantAreaId = order.RestaurantAreaId,
                TableResourceId = order.TableResourceId,
                TableName = order.TableResourceId.HasValue &&
            tableNamesById.TryGetValue(order.TableResourceId.Value, out var tableName)
    ? tableName
    : null,
                TableSessionId = order.TableSessionId,
                OrderType = (int)order.OrderType,
                OrderSource = (int)order.OrderSource,
                OrderSourceText = GetOrderSourceText(order.OrderSource),
                OrderTypeText = GetOrderTypeText(order.OrderType),
                Status = (int)order.Status,
                StatusText = GetStatusText(order.Status),
                KitchenDecisionStatus = (int)order.KitchenDecisionStatus,
                KitchenDecisionStatusText = GetKitchenDecisionStatusText(order.KitchenDecisionStatus),
                KitchenAcceptedAtUtc = order.KitchenAcceptedAtUtc,
                KitchenAcceptLaterMinutes = order.KitchenAcceptLaterMinutes,
                KitchenRejectedAtUtc = order.KitchenRejectedAtUtc,
                KitchenRejectReason = order.KitchenRejectReason,
                KitchenRejectNote = order.KitchenRejectNote,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                RequestedPickupAtUtc = order.RequestedPickupAtUtc,
                DeliveryAddress = order.DeliveryAddress,
                DeliveryNote = order.DeliveryNote,
                Note = order.Note,
                CreatedAtUtc = order.CreatedAtUtc,
                SubmittedAtUtc = order.SubmittedAtUtc,
                TotalAmount = order.TotalAmount,
                Currency = order.Currency,


                Items = order.Items
    .Where(x =>
        order.OrderSource == RestaurantOrderSource.KitchenDesk ||
        x.SendToKitchenSnapshot)
    .OrderBy(x => x.Id)
    .Select(item => new RestaurantOrderItemDto
    {
        Id = item.Id,
        OrderId = item.OrderId,
        OrderGuestId = item.OrderGuestId,
        OrderGuestName = order.Guests
    .FirstOrDefault(g => g.Id == item.OrderGuestId)
    ?.Name,
        MenuItemId = item.MenuItemId,
        MenuItemNameSnapshot = item.MenuItemNameSnapshot,
        UnitPriceSnapshot = item.UnitPriceSnapshot,
        Quantity = item.Quantity,
        LineSubtotal = item.LineSubtotal,
        SendToKitchenSnapshot = item.SendToKitchenSnapshot,
        IsReady = item.IsReady,
        ReadyAtUtc = item.ReadyAtUtc,
        Note = item.Note,
        Options = item.Options
                            .OrderBy(x => x.Id)
                            .Select(option => new RestaurantOrderItemOptionDto
                            {
                                Id = option.Id,
                                OrderItemId = option.OrderItemId,
                                MenuItemOptionId = option.MenuItemOptionId,
                                RestaurantAddonId = option.RestaurantAddonId,
                                OptionNameSnapshot = option.OptionNameSnapshot,
                                PriceDeltaSnapshot = option.PriceDeltaSnapshot,
                                AmountMode = (int)option.AmountMode,
                                AmountModeText = GetAddonAmountModeText(option.AmountMode)
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("{orderId:long}")]
    public async Task<ActionResult<RestaurantOrderDto>> GetById(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrderAsync(orderId, asTracking: false, cancellationToken);

        if (entity is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpGet("messages")]
    public async Task<ActionResult<List<RestaurantOrderMessageDto>>> GetBusinessMessages(
    [FromQuery] long businessId,
    [FromQuery] string? operationUnitIds,
    [FromQuery] long? beforeMessageId,
    [FromQuery] int take = 30,
    CancellationToken cancellationToken = default)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        take = Math.Clamp(take, 1, 100);

        var parsedOperationUnitIds = ParseOperationUnitIds(operationUnitIds);

        if (parsedOperationUnitIds.Count == 0)
            return BadRequest("operationUnitIds je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validOperationUnitIds = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.IsActive &&
                parsedOperationUnitIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (validOperationUnitIds.Count == 0)
            return BadRequest("Nijedna izabrana radna jedinica ne postoji ili nije aktivna.");

        var query = DbContext.RestaurantOrderMessages
            .AsNoTracking()
            .Include(x => x.Recipients)
            .Where(x => x.BusinessId == businessId);

        if (beforeMessageId.HasValue)
            query = query.Where(x => x.Id < beforeMessageId.Value);

        query = query.Where(x =>
            (
                x.SenderOperationUnitId.HasValue &&
                validOperationUnitIds.Contains(x.SenderOperationUnitId.Value)
            )
            ||
            x.Recipients.Any(r =>
                r.RecipientOperationUnitId.HasValue &&
                validOperationUnitIds.Contains(r.RecipientOperationUnitId.Value))
            ||
            x.Recipients.Any(r =>
                r.RecipientType == RestaurantOrderMessageRecipientType.Business)
            ||
            (
                !x.SenderOperationUnitId.HasValue &&
                !x.Recipients.Any()
            ));

        var messages = await query
            .OrderByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        messages = messages
            .OrderBy(x => x.Id)
            .ToList();

        return Ok(messages.Select(ToMessageDto).ToList());
    }

    [HttpPost("internal-message")]
    public async Task<ActionResult<RestaurantOrderMessageDto>> SendInternalMessage(
    [FromBody] SendRestaurantInternalMessageRequestDto request,
    CancellationToken cancellationToken = default)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        if (request.SenderOperationUnitId <= 0)
            return BadRequest("Pošiljalac je obavezan.");

        if (request.RecipientOperationUnitId <= 0)
            return BadRequest("Primalac je obavezan.");

        var text = NormalizeText(request.Text, 2000);

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest("Unesite tekst poruke.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var operationUnits = await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == request.BusinessId &&
                x.IsActive &&
                (x.Id == request.SenderOperationUnitId ||
                 x.Id == request.RecipientOperationUnitId))
            .ToListAsync(cancellationToken);

        var senderUnit = operationUnits
            .FirstOrDefault(x => x.Id == request.SenderOperationUnitId);

        if (senderUnit is null)
            return BadRequest("Radna jedinica pošiljaoca ne postoji ili nije aktivna.");

        var recipientUnit = operationUnits
            .FirstOrDefault(x => x.Id == request.RecipientOperationUnitId);

        if (recipientUnit is null)
            return BadRequest("Radna jedinica primaoca ne postoji ili nije aktivna.");

        var now = DateTime.UtcNow;

        var senderType = senderUnit.UnitType == RestaurantOperationUnitType.Kitchen
            ? RestaurantOrderMessageSenderType.Kitchen
            : RestaurantOrderMessageSenderType.Restaurant;

        var message = new RestaurantOrderMessage
        {
            BusinessId = request.BusinessId,
            OrderId = null,
            SenderType = senderType,
            SenderOperationUnitId = senderUnit.Id,
            MessageType = RestaurantOrderMessageType.InternalManualMessage,
            Text = text,
            ActionKey = null,
            IsActionRequired = false,
            IsActionCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        message.Recipients.Add(new RestaurantOrderMessageRecipient
        {
            BusinessId = request.BusinessId,
            RecipientType = RestaurantOrderMessageRecipientType.OperationUnit,
            RecipientOperationUnitId = recipientUnit.Id,
            IsRead = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        DbContext.RestaurantOrderMessages.Add(message);

        await DbContext.SaveChangesAsync(cancellationToken);

        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(request.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
                new
                {
                    businessId = request.BusinessId,
                    messageId = message.Id,
                    senderOperationUnitId = senderUnit.Id,
                    recipientOperationUnitId = recipientUnit.Id,
                    activityType = "RestaurantInternalMessage"
                },
                cancellationToken);

        var saved = await DbContext.RestaurantOrderMessages
            .AsNoTracking()
            .Include(x => x.Recipients)
            .FirstAsync(x => x.Id == message.Id, cancellationToken);

        return Ok(ToMessageDto(saved));
    }

    [HttpGet("{orderId:long}/messages")]
    public async Task<ActionResult<List<RestaurantOrderMessageDto>>> GetMessages(
     [FromRoute] long orderId,
     [FromQuery] long? beforeMessageId,
     [FromQuery] int take = 30,
     CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);

        var order = await DbContext.RestaurantOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantOrderMessages
            .AsNoTracking()
            .Where(x => x.OrderId == orderId);

        if (beforeMessageId.HasValue)
            query = query.Where(x => x.Id < beforeMessageId.Value);

        var messages = await query
            .OrderByDescending(x => x.Id)
            .Take(take)
            .ToListAsync(cancellationToken);

        messages = messages
            .OrderBy(x => x.Id)
            .ToList();

        return Ok(messages.Select(ToMessageDto).ToList());
    }

    [HttpGet("table-session/{tableSessionId:long}")]
    public async Task<ActionResult<List<RestaurantOrderDto>>> GetByTableSession(
    [FromRoute] long tableSessionId,
    [FromQuery] bool includeFinished = true,
    CancellationToken cancellationToken = default)
    {
        if (tableSessionId <= 0)
            return BadRequest("tableSessionId je obavezan.");

        var session = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tableSessionId, cancellationToken);

        if (session is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(session.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantOrders
     .AsNoTracking()
     .Include(x => x.Guests)
     .Include(x => x.Items)
         .ThenInclude(x => x.Options)
     .Where(x => x.TableSessionId == tableSessionId);

        if (!includeFinished)
        {
            query = query.Where(x =>
                x.Status != RestaurantOrderStatus.Served &&
                x.Status != RestaurantOrderStatus.Cancelled);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(items.Select(ToDto).ToList());
    }

    [HttpGet("table-session/{tableSessionId:long}/bill")]
    public async Task<ActionResult<RestaurantTableBillDto>> GetTableSessionBill(
    [FromRoute] long tableSessionId,
    CancellationToken cancellationToken)
    {
        if (tableSessionId <= 0)
            return BadRequest("tableSessionId je obavezan.");

        var session = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .Include(x => x.TableResource)
            .FirstOrDefaultAsync(x => x.Id == tableSessionId, cancellationToken);

        if (session is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(session.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var orders = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x =>
                x.TableSessionId == tableSessionId &&
                x.Status != RestaurantOrderStatus.Cancelled)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var payments = await DbContext.RestaurantPayments
    .AsNoTracking()
    .Where(x => x.TableSessionId == tableSessionId)
    .OrderByDescending(x => x.PaidAtUtc)
    .ToListAsync(cancellationToken);

        var activeOrderCount = orders.Count(x =>
            x.Status != RestaurantOrderStatus.Served &&
            x.Status != RestaurantOrderStatus.Cancelled);

        var totalAmount = orders.Sum(x => x.TotalAmount);

        var paidAmount = payments
            .Where(x => x.Status == RestaurantPaymentStatus.Paid)
            .Sum(x => x.Amount);

        var remainingAmount = totalAmount - paidAmount;

        if (remainingAmount < 0)
            remainingAmount = 0;

        var lines = orders
            .SelectMany(x => x.Items)
            .Select(item =>
            {
                var optionsText = item.Options.Count == 0
                    ? null
                    : string.Join(", ", item.Options
                        .OrderBy(o => o.OptionNameSnapshot)
                        .Select(o =>
                        {
                            var amountText = o.AmountMode switch
                            {
                                RestaurantAddonAmountMode.Less => " (malo)",
                                RestaurantAddonAmountMode.More => " (više)",
                                _ => ""
                            };

                            return o.PriceDeltaSnapshot == 0
                                ? $"{o.OptionNameSnapshot}{amountText}"
                                : $"{o.OptionNameSnapshot}{amountText} +{o.PriceDeltaSnapshot:0.##}";
                        }));

                return new RestaurantTableBillLineDto
                {
                    Name = item.MenuItemNameSnapshot,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPriceSnapshot,
                    LineTotal = item.LineSubtotal,
                    OptionsText = optionsText,
                    Note = item.Note
                };
            })
            .ToList();

        return Ok(new RestaurantTableBillDto
        {
            BusinessId = session.BusinessId,
            TableSessionId = session.Id,
            RestaurantAreaId = session.RestaurantAreaId,
            TableResourceId = session.TableResourceId,
            TableName = session.TableResource.Name,
            CustomerName = session.CustomerName,
            PartySize = session.PartySize,
            StartedAtUtc = session.StartedAtUtc,
            OrderCount = orders.Count,
            ActiveOrderCount = activeOrderCount,
            HasActiveOrders = activeOrderCount > 0,
            TotalAmount = totalAmount,
            PaidAmount = paidAmount,
            RemainingAmount = remainingAmount,
            IsFullyPaid = totalAmount > 0 && paidAmount >= totalAmount,
            Currency = orders.FirstOrDefault()?.Currency ?? payments.FirstOrDefault()?.Currency ?? "RSD",
            Lines = lines,
            Payments = payments
    .Select(ToPaymentDto)
    .ToList()
        });
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantOrderDto>> Create(
        [FromBody] CreateRestaurantOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");


        if (!Enum.IsDefined(typeof(RestaurantOrderType), request.OrderType))
            return BadRequest("Nepoznat tip narudžbine.");

        if (!Enum.IsDefined(typeof(RestaurantOrderSource), request.OrderSource))
            return BadRequest("Izvor porudžbine nije ispravan.");

        var orderSource = (RestaurantOrderSource)request.OrderSource;

        var orderType = (RestaurantOrderType)request.OrderType;

        if (orderType == RestaurantOrderType.DineIn && !request.TableSessionId.HasValue)
            return BadRequest("Za narudžbinu u lokalu potrebno je izabrati zauzeće stola.");

        if (orderType == RestaurantOrderType.Takeaway && !request.RequestedPickupAtUtc.HasValue)
            return BadRequest("Za narudžbinu za poneti unesite vreme preuzimanja.");

        if (orderType == RestaurantOrderType.Delivery && string.IsNullOrWhiteSpace(request.DeliveryAddress))
            return BadRequest("Za dostavu unesite adresu.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var businessExists = await DbContext.Businesses
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.BusinessId && x.IsActive, cancellationToken);

        if (!businessExists)
            return BadRequest("Izabrana radnja ne postoji ili nije aktivna.");



        if (request.RestaurantAreaId.HasValue)
        {
            var areaExists = await DbContext.RestaurantAreas
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == request.RestaurantAreaId.Value &&
                    x.BusinessId == request.BusinessId &&
                    x.IsActive,
                    cancellationToken);

            if (!areaExists)
                return BadRequest("Izabrana sala ne postoji ili nije aktivna.");
        }

        if (request.TableResourceId.HasValue)
        {
            var tableExists = await DbContext.Resources
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == request.TableResourceId.Value &&
                    x.BusinessId == request.BusinessId &&
                    x.IsActive,
                    cancellationToken);

            if (!tableExists)
                return BadRequest("Izabrani sto ne postoji ili nije aktivan.");
        }

        if (request.TableSessionId.HasValue)
        {
            var session = await DbContext.RestaurantTableSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.TableSessionId.Value &&
                    x.BusinessId == request.BusinessId,
                    cancellationToken);

            if (session is null)
                return BadRequest("Izabrano zauzeće stola ne postoji.");

            if (session.Status != RestaurantTableSessionStatus.Active || session.ReleasedAtUtc.HasValue)
                return BadRequest("Narudžbina može da se veže samo za aktivno zauzeće stola.");

            request.RestaurantAreaId ??= session.RestaurantAreaId;
            request.TableResourceId ??= session.TableResourceId;
        }

        var now = DateTime.UtcNow;
        var orderDateLocal = GetRestaurantOrderLocalDate(now);
        var dailyOrderNumber = await GetNextDailyOrderNumberAsync(
            request.BusinessId,
            orderDateLocal,
            cancellationToken);

        var entity = new RestaurantOrder
        {
            BusinessId = request.BusinessId,
            OrderDateLocal = orderDateLocal,
            DailyOrderNumber = dailyOrderNumber,
            RestaurantAreaId = request.RestaurantAreaId,
            TableResourceId = request.TableResourceId,
            TableSessionId = request.TableSessionId,
            OrderType = orderType,
            OrderSource = orderSource,
            RequestedPickupAtUtc = EnsureUtcOrNull(request.RequestedPickupAtUtc),
            DeliveryAddress = NormalizeText(request.DeliveryAddress, 500),
            DeliveryNote = NormalizeText(request.DeliveryNote, 1000),
            CustomerName = NormalizeText(request.CustomerName, 200),
            CustomerPhone = NormalizeText(request.CustomerPhone, 50),
            Note = NormalizeText(request.Note, 1000),
            Status = RestaurantOrderStatus.Draft,
            SubtotalAmount = 0m,
            TotalAmount = 0m,
            Currency = "RSD",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantOrders.Add(entity);

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(entity.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpPut("{orderId:long}")]
    public async Task<ActionResult<RestaurantOrderDto>> Update(
        [FromRoute] long orderId,
        [FromBody] UpdateRestaurantOrderRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (entity is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status is RestaurantOrderStatus.Served or RestaurantOrderStatus.Cancelled)
            return BadRequest("Završena ili otkazana narudžbina ne može da se menja.");

        entity.CustomerName = NormalizeText(request.CustomerName, 200);
        entity.CustomerPhone = NormalizeText(request.CustomerPhone, 50);
        entity.Note = NormalizeText(request.Note, 1000);
        entity.RequestedPickupAtUtc = EnsureUtcOrNull(request.RequestedPickupAtUtc);
        entity.DeliveryAddress = NormalizeText(request.DeliveryAddress, 500);
        entity.DeliveryNote = NormalizeText(request.DeliveryNote, 1000);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{orderId:long}/guests")]
    public async Task<ActionResult<RestaurantOrderDto>> AddGuest(
    [FromRoute] long orderId,
    [FromBody] CreateRestaurantOrderGuestRequest request,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status is RestaurantOrderStatus.Served or RestaurantOrderStatus.Cancelled)
            return BadRequest("Završena ili otkazana narudžbina ne može da se menja.");

        var name = NormalizeText(request.Name, 120);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv gosta/osobe.");

        var duplicateExists = order.Guests.Any(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
            return BadRequest("Gost/osoba sa ovim nazivom već postoji u ovoj porudžbini.");

        var now = DateTime.UtcNow;

        var guest = new RestaurantOrderGuest
        {
            OrderId = order.Id,
            Name = name,
            DisplayOrder = request.DisplayOrder,
            Note = NormalizeText(request.Note, 1000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        order.Guests.Add(guest);
        order.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(order.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpPut("{orderId:long}/guests/{guestId:long}")]
    public async Task<ActionResult<RestaurantOrderDto>> UpdateGuest(
    [FromRoute] long orderId,
    [FromRoute] long guestId,
    [FromBody] UpdateRestaurantOrderGuestRequest request,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status is RestaurantOrderStatus.Served or RestaurantOrderStatus.Cancelled)
            return BadRequest("Završena ili otkazana narudžbina ne može da se menja.");

        var guest = order.Guests.FirstOrDefault(x => x.Id == guestId);

        if (guest is null)
            return NotFound("Gost/osoba ne postoji u ovoj porudžbini.");

        var name = NormalizeText(request.Name, 120);

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Unesite naziv gosta/osobe.");

        var duplicateExists = order.Guests.Any(x =>
            x.Id != guest.Id &&
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

        if (duplicateExists)
            return BadRequest("Gost/osoba sa ovim nazivom već postoji u ovoj porudžbini.");

        var now = DateTime.UtcNow;

        guest.Name = name;
        guest.DisplayOrder = request.DisplayOrder;
        guest.Note = NormalizeText(request.Note, 1000);
        guest.UpdatedAtUtc = now;

        order.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(order.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpDelete("{orderId:long}/guests/{guestId:long}")]
    public async Task<ActionResult<RestaurantOrderDto>> DeleteGuest(
    [FromRoute] long orderId,
    [FromRoute] long guestId,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status is RestaurantOrderStatus.Served or RestaurantOrderStatus.Cancelled)
            return BadRequest("Završena ili otkazana narudžbina ne može da se menja.");

        var guest = order.Guests.FirstOrDefault(x => x.Id == guestId);

        if (guest is null)
            return NotFound("Gost/osoba ne postoji u ovoj porudžbini.");

        var hasItems = order.Items.Any(x => x.OrderGuestId == guest.Id);

        if (hasItems)
            return BadRequest("Gost/osoba ne može da se obriše jer ima stavke. Prvo premestite ili obrišite stavke.");

        DbContext.RestaurantOrderGuests.Remove(guest);
        order.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(order.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpPost("{orderId:long}/items")]
    public async Task<ActionResult<RestaurantOrderDto>> AddItem(
        [FromRoute] long orderId,
        [FromBody] AddRestaurantOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!CanEditItems(order.Status))
            return BadRequest("Stavke mogu da se menjaju samo dok narudžbina nije završena ili otkazana.");

        var itemResult = await BuildOrderItemAsync(
            order,
            request.OrderGuestId,
            request.MenuItemId,
            request.Quantity,
            request.Note,
            request.Addons,
            cancellationToken);

        if (itemResult.Error is not null)
            return BadRequest(itemResult.Error);

        order.Items.Add(itemResult.Item!);
        RecalculateOrderTotals(order);
        order.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(order.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpPut("{orderId:long}/items/{orderItemId:long}")]
    public async Task<ActionResult<RestaurantOrderDto>> UpdateItem(
        [FromRoute] long orderId,
        [FromRoute] long orderItemId,
        [FromBody] UpdateRestaurantOrderItemRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!CanEditItems(order.Status))
            return BadRequest("Stavke mogu da se menjaju samo dok narudžbina nije završena ili otkazana.");

        var existingItem = order.Items.FirstOrDefault(x => x.Id == orderItemId);
        if (existingItem is null)
            return NotFound("Stavka narudžbine ne postoji.");

        var itemResult = await BuildOrderItemAsync(
            order,
            request.OrderGuestId,
            existingItem.MenuItemId,
            request.Quantity,
            request.Note,
            request.Addons,
            cancellationToken);

        if (itemResult.Error is not null)
            return BadRequest(itemResult.Error);

        existingItem.OrderGuestId = itemResult.Item!.OrderGuestId;
        existingItem.Quantity = itemResult.Item!.Quantity;
        existingItem.Note = itemResult.Item.Note;
        existingItem.UnitPriceSnapshot = itemResult.Item.UnitPriceSnapshot;
        existingItem.LineSubtotal = itemResult.Item.LineSubtotal;
        existingItem.SendToKitchenSnapshot = itemResult.Item.SendToKitchenSnapshot;
        existingItem.MenuItemNameSnapshot = itemResult.Item.MenuItemNameSnapshot;
        existingItem.UpdatedAtUtc = DateTime.UtcNow;

        DbContext.RestaurantOrderItemOptions.RemoveRange(existingItem.Options);
        existingItem.Options.Clear();

        foreach (var option in itemResult.Item.Options)
        {
            existingItem.Options.Add(option);
        }

        RecalculateOrderTotals(order);
        order.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(order.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpPost("{orderId:long}/items/{orderItemId:long}/toggle-ready")]
    public async Task<ActionResult<RestaurantOrderDto>> ToggleOrderItemReady(
      [FromRoute] long orderId,
      [FromRoute] long orderItemId,
      CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status is RestaurantOrderStatus.Draft)
            return BadRequest("Artikal može da se označi kao spreman tek kada je narudžbina poslata.");

        if (order.Status is RestaurantOrderStatus.Cancelled or RestaurantOrderStatus.Served)
            return BadRequest("Završena ili otkazana narudžbina ne može da menja spremnost artikala.");

        var item = order.Items.FirstOrDefault(x => x.Id == orderItemId);

        if (item is null)
            return NotFound("Stavka narudžbine ne postoji.");

        var now = DateTime.UtcNow;
        var wasReady = item.IsReady;
        var previousOrderStatus = order.Status;

        item.IsReady = !item.IsReady;
        item.ReadyAtUtc = item.IsReady ? now : null;
        item.UpdatedAtUtc = now;
        order.UpdatedAtUtc = now;

        if (!wasReady && item.IsReady)
        {
            if (order.OrderSource != RestaurantOrderSource.KitchenDesk)
            {
                var guestName = order.Guests
                    .FirstOrDefault(x => x.Id == item.OrderGuestId)
                    ?.Name;

                var guestText = string.IsNullOrWhiteSpace(guestName)
                    ? ""
                    : $" ({guestName})";

                await AddOrderMessageAsync(
                    order,
                    RestaurantOrderMessageSenderType.Kitchen,
                    RestaurantOperationUnitType.Kitchen,
                    new[] { RestaurantOperationUnitType.DiningRoom },
                    RestaurantOrderMessageType.Text,
                    $"Artikal je spreman: {item.MenuItemNameSnapshot}{guestText}.",
                    actionKey: null,
                    isActionRequired: false,
                    cancellationToken);
            }
        }

        var kitchenItems = order.Items
            .Where(x => x.SendToKitchenSnapshot)
            .ToList();

        if (kitchenItems.Count == 0)
            kitchenItems = order.Items.ToList();

        var allKitchenItemsReady =
            kitchenItems.Count > 0 &&
            kitchenItems.All(x => x.IsReady);

        if (allKitchenItemsReady)
        {
            if (order.Status != RestaurantOrderStatus.Ready)
            {
                order.Status = RestaurantOrderStatus.Ready;
                order.UpdatedAtUtc = now;

                if (order.SubmittedAtUtc is null)
                    order.SubmittedAtUtc = now;

                await AddOrderMessageAsync(
                    order,
                    RestaurantOrderMessageSenderType.Kitchen,
                    RestaurantOperationUnitType.Kitchen,
                    new[] { RestaurantOperationUnitType.DiningRoom },
                    RestaurantOrderMessageType.OrderReady,
                    "Svi artikli su spremni. Porudžbina je spremna.",
                    actionKey: null,
                    isActionRequired: false,
                    cancellationToken);
            }
        }
        else if (previousOrderStatus == RestaurantOrderStatus.Ready)
        {
            order.Status =
                order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.Accepted ||
                order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.WaitingAcceptedByCustomer
                    ? RestaurantOrderStatus.Preparing
                    : RestaurantOrderStatus.Submitted;

            order.UpdatedAtUtc = now;

            await AddOrderMessageAsync(
                order,
                RestaurantOrderMessageSenderType.Kitchen,
                RestaurantOperationUnitType.Kitchen,
                new[] { RestaurantOperationUnitType.DiningRoom },
                RestaurantOrderMessageType.OrderPreparing,
                "Porudžbina više nije kompletno spremna.",
                actionKey: null,
                isActionRequired: false,
                cancellationToken);
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        var activityType = order.Status switch
        {
            RestaurantOrderStatus.Ready => "RestaurantOrderReady",
            RestaurantOrderStatus.Preparing => "RestaurantOrderPreparing",
            _ => item.IsReady
                ? "RestaurantOrderItemReady"
                : "RestaurantOrderItemNotReady"
        };

        await NotifyRestaurantOrderChangedAsync(
            order,
            activityType,
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/kitchen-accept")]
    public async Task<ActionResult<RestaurantOrderDto>> KitchenAccept(
    [FromRoute] long orderId,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Kuhinja može da prihvati samo porudžbinu koja je poslata.");

        if (order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.Rejected)
            return BadRequest("Odbijena porudžbina ne može naknadno da se prihvati.");

        var now = DateTime.UtcNow;

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.Accepted;
        order.KitchenAcceptedAtUtc = now;
        order.KitchenAcceptLaterMinutes = null;
        order.KitchenRejectedAtUtc = null;
        order.KitchenRejectReason = null;
        order.KitchenRejectNote = null;
        order.UpdatedAtUtc = now;

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Kitchen,
            RestaurantOperationUnitType.Kitchen,
            new[] { RestaurantOperationUnitType.DiningRoom },
            RestaurantOrderMessageType.KitchenAccepted,
            "Kuhinja je prihvatila porudžbinu.",
            actionKey: null,
            isActionRequired: false,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderKitchenAccepted",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/kitchen-accept-later")]
    public async Task<ActionResult<RestaurantOrderDto>> KitchenAcceptLater(
        [FromRoute] long orderId,
        [FromBody] AcceptRestaurantOrderLaterRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Minutes < 5 || request.Minutes > 360 || request.Minutes % 5 != 0)
            return BadRequest("Odlaganje mora biti od 5 minuta do 6 sati, u koracima od 5 minuta.");

        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Kuhinja može da prihvati kasnije samo porudžbinu koja je poslata.");

        if (order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.Rejected)
            return BadRequest("Odbijena porudžbina ne može naknadno da se prihvati.");

        var now = DateTime.UtcNow;

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.AcceptedLater;
        order.KitchenAcceptedAtUtc = now;
        order.KitchenAcceptLaterMinutes = request.Minutes;
        order.KitchenRejectedAtUtc = null;
        order.KitchenRejectReason = null;
        order.KitchenRejectNote = null;
        order.UpdatedAtUtc = now;

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Kitchen,
            RestaurantOperationUnitType.Kitchen,
            new[] { RestaurantOperationUnitType.DiningRoom },
            RestaurantOrderMessageType.KitchenWaitingProposed,
            $"Kuhinja traži čekanje: {FormatDelayMinutes(request.Minutes)}.",
            actionKey: "customer-waiting-response",
            isActionRequired: true,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderKitchenAcceptedLater",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/kitchen-reject")]
    public async Task<ActionResult<RestaurantOrderDto>> KitchenReject(
        [FromRoute] long orderId,
        [FromBody] RejectRestaurantOrderByKitchenRequest request,
        CancellationToken cancellationToken)
    {
        var reason = NormalizeText(request.Reason, 300);
        var note = NormalizeText(request.Note, 1000);

        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest("Unesite razlog odbijanja porudžbine.");

        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Kuhinja može da odbije samo porudžbinu koja je poslata.");

        var now = DateTime.UtcNow;

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.Rejected;
        order.KitchenAcceptedAtUtc = null;
        order.KitchenAcceptLaterMinutes = null;
        order.KitchenRejectedAtUtc = now;
        order.KitchenRejectReason = reason;
        order.KitchenRejectNote = note;
        order.Status = RestaurantOrderStatus.Cancelled;
        order.CompletedAtUtc = now;
        order.UpdatedAtUtc = now;

        AppendNote(order, $"Kuhinja odbila: {reason}");

        if (!string.IsNullOrWhiteSpace(note))
            AppendNote(order, $"Napomena kuhinje: {note}");

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Kitchen,
            RestaurantOperationUnitType.Kitchen,
            new[] { RestaurantOperationUnitType.DiningRoom },
            RestaurantOrderMessageType.KitchenRejected,
            string.IsNullOrWhiteSpace(note)
                ? $"Kuhinja je odbila porudžbinu. Razlog: {reason}"
                : $"Kuhinja je odbila porudžbinu. Razlog: {reason} Napomena: {note}",
            actionKey: null,
            isActionRequired: false,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderKitchenRejected",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/waiting-accepted-by-customer")]
    public async Task<ActionResult<RestaurantOrderDto>> WaitingAcceptedByCustomer(
    [FromRoute] long orderId,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Čekanje može da se potvrdi samo za poslatu porudžbinu.");

        if (order.KitchenDecisionStatus != RestaurantKitchenDecisionStatus.AcceptedLater)
            return BadRequest("Kuhinja nije predložila čekanje za ovu porudžbinu.");

        var now = DateTime.UtcNow;

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.WaitingAcceptedByCustomer;
        order.UpdatedAtUtc = now;

        var delayText = FormatDelayMinutes(order.KitchenAcceptLaterMinutes ?? 0);

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Restaurant,
            RestaurantOperationUnitType.DiningRoom,
            new[] { RestaurantOperationUnitType.Kitchen },
            RestaurantOrderMessageType.CustomerAcceptedWaiting,
            $"Klijent je prihvatio čekanje: {delayText}.",
            actionKey: null,
            isActionRequired: false,
            cancellationToken);

        MarkOrderWaitingProposalMessagesCompleted(order.Id, now);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderWaitingAcceptedByCustomer",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/waiting-rejected-by-customer")]
    public async Task<ActionResult<RestaurantOrderDto>> WaitingRejectedByCustomer(
    [FromRoute] long orderId,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Submitted)
            return BadRequest("Čekanje može da se odbije samo za poslatu porudžbinu.");

        if (order.KitchenDecisionStatus != RestaurantKitchenDecisionStatus.AcceptedLater)
            return BadRequest("Kuhinja nije predložila čekanje za ovu porudžbinu.");

        var now = DateTime.UtcNow;
        var delayText = FormatDelayMinutes(order.KitchenAcceptLaterMinutes ?? 0);

        order.KitchenDecisionStatus = RestaurantKitchenDecisionStatus.WaitingRejectedByCustomer;
        order.Status = RestaurantOrderStatus.Cancelled;
        order.CompletedAtUtc = now;
        order.UpdatedAtUtc = now;

        AppendNote(order, $"Klijent nije prihvatio čekanje: {delayText}.");

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Restaurant,
            RestaurantOperationUnitType.DiningRoom,
            new[] { RestaurantOperationUnitType.Kitchen },
            RestaurantOrderMessageType.CustomerRejectedWaiting,
            $"Klijent nije prihvatio čekanje: {delayText}. Porudžbina je otkazana.",
            actionKey: null,
            isActionRequired: false,
            cancellationToken);

        MarkOrderWaitingProposalMessagesCompleted(order.Id, now);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderWaitingRejectedByCustomer",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/start-preparing")]
    public async Task<ActionResult<RestaurantOrderDto>> StartPreparing(
    [FromRoute] long orderId,
    CancellationToken cancellationToken)
    {
        return await ChangeOrderStatusAsync(
            orderId,
            RestaurantOrderStatus.Preparing,
            null,
            cancellationToken);
    }

    [HttpPost("{orderId:long}/mark-ready")]
    public async Task<ActionResult<RestaurantOrderDto>> MarkReady(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        return await ChangeOrderStatusAsync(
            orderId,
            RestaurantOrderStatus.Ready,
            null,
            cancellationToken);
    }

    [HttpPost("{orderId:long}/mark-served")]
    public async Task<ActionResult<RestaurantOrderDto>> MarkServed(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        return await ChangeOrderStatusAsync(
            orderId,
            RestaurantOrderStatus.Served,
            null,
            cancellationToken);
    }

    [HttpPost("{orderId:long}/cancel")]
    public async Task<ActionResult<RestaurantOrderDto>> CancelOrder(
    [FromRoute] long orderId,
    [FromBody] CancelRestaurantOrderRequest request,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status == RestaurantOrderStatus.Served)
            return BadRequest("Poslužena narudžbina ne može da se otkaže.");

        if (order.Status == RestaurantOrderStatus.Cancelled)
            return BadRequest("Narudžbina je već otkazana.");

        var now = DateTime.UtcNow;

        order.Status = RestaurantOrderStatus.Cancelled;
        order.CompletedAtUtc = now;
        order.UpdatedAtUtc = now;

        AppendNote(order, request.Note);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderCancelled",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpDelete("{orderId:long}/items/{orderItemId:long}")]
    public async Task<ActionResult<RestaurantOrderDto>> DeleteItem(
        [FromRoute] long orderId,
        [FromRoute] long orderItemId,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!CanEditItems(order.Status))
            return BadRequest("Stavke mogu da se menjaju samo dok narudžbina nije završena ili otkazana.");

        var item = order.Items.FirstOrDefault(x => x.Id == orderItemId);
        if (item is null)
            return NotFound("Stavka narudžbine ne postoji.");

        DbContext.RestaurantOrderItems.Remove(item);
        order.Items.Remove(item);

        RecalculateOrderTotals(order);
        order.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await LoadOrderAsync(order.Id, asTracking: false, cancellationToken);
        return Ok(ToDto(dtoEntity!));
    }

    [HttpPost("{orderId:long}/submit")]
    public async Task<ActionResult<RestaurantOrderDto>> Submit(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Draft)
            return BadRequest("Samo nacrt narudžbine može da se pošalje.");

        if (order.Items.Count == 0)
            return BadRequest("Narudžbina mora imati bar jednu stavku.");

        var now = DateTime.UtcNow;

        order.Status = RestaurantOrderStatus.Submitted;
        order.SubmittedAtUtc = now;
        order.UpdatedAtUtc = now;

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Restaurant,
            RestaurantOperationUnitType.DiningRoom,
            new[] { RestaurantOperationUnitType.Kitchen },
            RestaurantOrderMessageType.OrderSubmitted,
            "Porudžbina je poslata kuhinji.",
            actionKey: null,
            isActionRequired: false,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        await NotifyRestaurantOrderChangedAsync(
            order,
            "RestaurantOrderSubmitted",
            cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpPost("{orderId:long}/status")]
    public async Task<ActionResult<RestaurantOrderDto>> SetStatus(
        [FromRoute] long orderId,
        [FromBody] SetRestaurantOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (!Enum.IsDefined(typeof(RestaurantOrderStatus), request.Status))
            return BadRequest("Nepoznat status narudžbine.");

        var newStatus = (RestaurantOrderStatus)request.Status;

        if (order.Status == RestaurantOrderStatus.Cancelled)
            return BadRequest("Otkazana narudžbina ne može da menja status.");

        if (order.Status == RestaurantOrderStatus.Served && newStatus != RestaurantOrderStatus.Served)
            return BadRequest("Poslužena narudžbina ne može da se vraća na prethodni status.");

        if (newStatus == RestaurantOrderStatus.Preparing ||
            newStatus == RestaurantOrderStatus.Ready ||
            newStatus == RestaurantOrderStatus.Served)
        {
            return await ChangeOrderStatusAsync(
                orderId,
                newStatus,
                request.Note,
                cancellationToken);
        }

        var now = DateTime.UtcNow;

        order.Status = newStatus;

        if (newStatus != RestaurantOrderStatus.Draft && order.SubmittedAtUtc is null)
            order.SubmittedAtUtc = now;

        if (newStatus == RestaurantOrderStatus.Cancelled)
            order.CompletedAtUtc = now;

        AppendNote(order, request.Note);
        order.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(order));
    }

    [HttpDelete("{orderId:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long orderId,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status != RestaurantOrderStatus.Draft)
            return BadRequest("Samo nacrt narudžbine može da se obriše. Poslate narudžbine ostaju kao istorija.");

        DbContext.RestaurantOrders.Remove(order);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ActionResult<RestaurantOrderDto>> ChangeOrderStatusAsync(
    long orderId,
    RestaurantOrderStatus newStatus,
    string? note,
    CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(orderId, asTracking: true, cancellationToken);

        if (order is null)
            return NotFound("Narudžbina ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(order.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (order.Status == RestaurantOrderStatus.Cancelled)
            return BadRequest("Otkazana narudžbina ne može da menja status.");

        if (order.Status == RestaurantOrderStatus.Served && newStatus != RestaurantOrderStatus.Served)
            return BadRequest("Poslužena narudžbina ne može da se vraća na prethodni status.");

        if (newStatus == RestaurantOrderStatus.Preparing &&
            order.Status != RestaurantOrderStatus.Submitted)
        {
            return BadRequest("Narudžbina može da pređe u pripremu samo iz statusa Poslato.");
        }

        if (newStatus == RestaurantOrderStatus.Preparing &&
    order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.None)
        {
            return BadRequest("Kuhinja prvo mora da prihvati porudžbinu.");
        }

        if (newStatus == RestaurantOrderStatus.Preparing &&
            order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.AcceptedLater)
        {
            return BadRequest("Kuhinja je predložila čekanje. Sačekajte da restoran/klijent prihvati čekanje.");
        }

        if (newStatus == RestaurantOrderStatus.Preparing &&
            order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.Rejected)
        {
            return BadRequest("Odbijena porudžbina ne može da ide u pripremu.");
        }

        if (newStatus == RestaurantOrderStatus.Preparing &&
            order.KitchenDecisionStatus == RestaurantKitchenDecisionStatus.WaitingRejectedByCustomer)
        {
            return BadRequest("Klijent nije prihvatio čekanje. Porudžbina ne može da ide u pripremu.");
        }

        if (newStatus == RestaurantOrderStatus.Ready &&
            order.Status != RestaurantOrderStatus.Preparing)
        {
            return BadRequest("Narudžbina može da bude spremna samo ako je prethodno bila u pripremi.");
        }

        if (newStatus == RestaurantOrderStatus.Served &&
            order.Status != RestaurantOrderStatus.Ready &&
            order.Status != RestaurantOrderStatus.Preparing &&
            order.Status != RestaurantOrderStatus.Submitted)
        {
            return BadRequest("Narudžbina ne može direktno u status Posluženo iz trenutnog statusa.");
        }

        var now = DateTime.UtcNow;

        order.Status = newStatus;

        if (newStatus != RestaurantOrderStatus.Draft && order.SubmittedAtUtc is null)
            order.SubmittedAtUtc = now;

        if (newStatus == RestaurantOrderStatus.Served)
            order.CompletedAtUtc = now;

        AppendNote(order, note);
        order.UpdatedAtUtc = now;

        var messageType = newStatus switch
        {
            RestaurantOrderStatus.Preparing => RestaurantOrderMessageType.OrderPreparing,
            RestaurantOrderStatus.Ready => RestaurantOrderMessageType.OrderReady,
            RestaurantOrderStatus.Served => RestaurantOrderMessageType.OrderServed,
            RestaurantOrderStatus.Cancelled => RestaurantOrderMessageType.OrderCancelled,
            _ => RestaurantOrderMessageType.Text
        };

        var messageText = newStatus switch
        {
            RestaurantOrderStatus.Preparing => "Porudžbina je u pripremi.",
            RestaurantOrderStatus.Ready => "Porudžbina je spremna.",
            RestaurantOrderStatus.Served => "Porudžbina je poslužena / preuzeta.",
            RestaurantOrderStatus.Cancelled => "Porudžbina je otkazana.",
            _ => $"Status porudžbine je promenjen: {GetStatusText(newStatus)}."
        };

        await AddOrderMessageAsync(
            order,
            RestaurantOrderMessageSenderType.Kitchen,
            RestaurantOperationUnitType.Kitchen,
            new[] { RestaurantOperationUnitType.DiningRoom },
            messageType,
            messageText,
            actionKey: null,
            isActionRequired: false,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        var activityType = newStatus switch
        {
            RestaurantOrderStatus.Preparing => "RestaurantOrderPreparing",
            RestaurantOrderStatus.Ready => "RestaurantOrderReady",
            RestaurantOrderStatus.Served => "RestaurantOrderServed",
            _ => "RestaurantOrderStatusChanged"
        };

        await NotifyRestaurantOrderChangedAsync(
            order,
            activityType,
            cancellationToken);

        return Ok(ToDto(order));
    }

    private async Task<RestaurantOrder?> LoadOrderAsync(
        long orderId,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        var query = DbContext.RestaurantOrders
            .Include(x => x.Guests)
            .Include(x => x.Items)
                .ThenInclude(x => x.Options)
            .Where(x => x.Id == orderId);

        if (!asTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task NotifyRestaurantOrderChangedAsync(
    RestaurantOrder order,
    string activityType,
    CancellationToken cancellationToken)
    {
        await _businessActivityHub.Clients
            .Group(BusinessActivityHub.BusinessGroupName(order.BusinessId))
            .SendAsync(
                "BusinessActivityChanged",
new
{
    businessId = order.BusinessId,
    orderId = order.Id,
    tableResourceId = order.TableResourceId,
    tableSessionId = order.TableSessionId,
    restaurantAreaId = order.RestaurantAreaId,
    orderType = (int)order.OrderType,
    orderSource = (int)order.OrderSource,
    orderSourceText = GetOrderSourceText(order.OrderSource),
    status = (int)order.Status,
    statusText = GetStatusText(order.Status),
    activityType
},
                cancellationToken);
    }

    private async Task<BuildOrderItemResult> BuildOrderItemAsync(
    RestaurantOrder order,
    long? orderGuestId,
    long menuItemId,
    int quantity,
    string? note,
    List<RestaurantOrderItemAddonSelectionDto> addons,
    CancellationToken cancellationToken)
    {
        if (menuItemId <= 0)
            return BuildOrderItemResult.Fail("menuItemId je obavezan.");

        if (quantity <= 0)
            return BuildOrderItemResult.Fail("Količina mora biti veća od 0.");

        if (orderGuestId.HasValue)
        {
            var guestExists = order.Guests.Any(x => x.Id == orderGuestId.Value);

            if (!guestExists)
                return BuildOrderItemResult.Fail("Izabrani gost/osoba ne postoji u ovoj porudžbini.");
        }

        var menuItem = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == menuItemId &&
                x.BusinessId == order.BusinessId &&
                x.IsActive &&
                x.IsAvailable,
                cancellationToken);

        if (menuItem is null)
            return BuildOrderItemResult.Fail("Artikal ne postoji ili trenutno nije dostupan.");

        addons ??= new List<RestaurantOrderItemAddonSelectionDto>();

        var normalizedAddonSelections = addons
            .Where(x => x.AddonId > 0)
            .GroupBy(x => x.AddonId)
            .Select(g => g.Last())
            .ToList();

        foreach (var addonSelection in normalizedAddonSelections)
        {
            if (!Enum.IsDefined(typeof(RestaurantAddonAmountMode), addonSelection.AmountMode))
                return BuildOrderItemResult.Fail("Nepoznata mera dodatka.");
        }

        var addonIds = normalizedAddonSelections
            .Select(x => x.AddonId)
            .Distinct()
            .ToList();

        var selectedAddons = addonIds.Count == 0
            ? new List<RestaurantAddon>()
            : await DbContext.RestaurantAddons
                .AsNoTracking()
                .Where(x =>
                    addonIds.Contains(x.Id) &&
                    x.BusinessId == order.BusinessId &&
                    x.IsActive &&
                    x.IsAvailable &&
                    x.AddonGroup.IsActive)
                .ToListAsync(cancellationToken);

        if (selectedAddons.Count != addonIds.Count)
            return BuildOrderItemResult.Fail("Jedan ili više dodataka nisu dostupni.");

        var addonById = selectedAddons.ToDictionary(x => x.Id);

        var addonTotal = selectedAddons.Sum(x => x.PriceDelta);
        var unitPrice = menuItem.Price + addonTotal;
        var lineSubtotal = unitPrice * quantity;
        var now = DateTime.UtcNow;

        var orderItem = new RestaurantOrderItem
        {
            OrderId = order.Id,
            OrderGuestId = orderGuestId,
            MenuItemId = menuItem.Id,
            MenuItemNameSnapshot = menuItem.Name,
            UnitPriceSnapshot = unitPrice,
            Quantity = quantity,
            LineSubtotal = lineSubtotal,
            SendToKitchenSnapshot = menuItem.SendToKitchen,
            Note = NormalizeText(note, 1000),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Options = normalizedAddonSelections
                .Select(selection =>
                {
                    var addon = addonById[selection.AddonId];

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

        return BuildOrderItemResult.Ok(orderItem);
    }

    private async Task AddOrderMessageAsync(
        RestaurantOrder order,
        RestaurantOrderMessageSenderType senderType,
        RestaurantOperationUnitType? senderUnitType,
        IReadOnlyCollection<RestaurantOperationUnitType> recipientUnitTypes,
        RestaurantOrderMessageType messageType,
        string text,
        string? actionKey,
        bool isActionRequired,
        CancellationToken cancellationToken)
    {
        var normalizedText = NormalizeText(text, 2000);

        if (string.IsNullOrWhiteSpace(normalizedText))
            return;

        var now = DateTime.UtcNow;

        var senderOperationUnitId = senderUnitType.HasValue
            ? await GetDefaultOperationUnitIdAsync(order.BusinessId, senderUnitType.Value, cancellationToken)
            : null;

        var recipientOperationUnitIds = new List<long>();

        foreach (var recipientUnitType in recipientUnitTypes.Distinct())
        {
            var recipientOperationUnitId = await GetDefaultOperationUnitIdAsync(
                order.BusinessId,
                recipientUnitType,
                cancellationToken);

            if (recipientOperationUnitId.HasValue)
                recipientOperationUnitIds.Add(recipientOperationUnitId.Value);
        }

        var message = new RestaurantOrderMessage
        {
            BusinessId = order.BusinessId,
            OrderId = order.Id,
            SenderType = senderType,
            SenderOperationUnitId = senderOperationUnitId,
            MessageType = messageType,
            Text = normalizedText,
            ActionKey = NormalizeText(actionKey, 100),
            IsActionRequired = isActionRequired,
            IsActionCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var recipientOperationUnitId in recipientOperationUnitIds.Distinct())
        {
            message.Recipients.Add(new RestaurantOrderMessageRecipient
            {
                BusinessId = order.BusinessId,
                RecipientType = RestaurantOrderMessageRecipientType.OperationUnit,
                RecipientOperationUnitId = recipientOperationUnitId,
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        if (message.Recipients.Count == 0)
        {
            message.Recipients.Add(new RestaurantOrderMessageRecipient
            {
                BusinessId = order.BusinessId,
                RecipientType = RestaurantOrderMessageRecipientType.Business,
                RecipientOperationUnitId = null,
                IsRead = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        DbContext.RestaurantOrderMessages.Add(message);
    }

    private async Task<long?> GetDefaultOperationUnitIdAsync(
    long businessId,
    RestaurantOperationUnitType unitType,
    CancellationToken cancellationToken)
    {
        return await DbContext.RestaurantOperationUnits
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.UnitType == unitType &&
                x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private void MarkOrderWaitingProposalMessagesCompleted(long orderId, DateTime completedAtUtc)
    {
        var messages = DbContext.RestaurantOrderMessages
            .Where(x =>
                x.OrderId == orderId &&
                x.ActionKey == "customer-waiting-response" &&
                x.IsActionRequired &&
                !x.IsActionCompleted)
            .ToList();

        foreach (var message in messages)
        {
            message.IsActionCompleted = true;
            message.ActionCompletedAtUtc = completedAtUtc;
            message.UpdatedAtUtc = completedAtUtc;
        }
    }

    private static void RecalculateOrderTotals(RestaurantOrder order)
    {
        order.SubtotalAmount = order.Items.Sum(x => x.LineSubtotal);
        order.TotalAmount = order.SubtotalAmount;

        var firstItem = order.Items.FirstOrDefault();
        if (firstItem is not null)
        {
            order.Currency = "RSD";
        }
    }

    private static bool CanEditItems(RestaurantOrderStatus status)
    {
        return status is RestaurantOrderStatus.Draft
            or RestaurantOrderStatus.Submitted
            or RestaurantOrderStatus.Preparing;
    }

    private static RestaurantOrderItemDto ToOrderItemDto(
    RestaurantOrderItem item,
    string? guestName = null)
    {
        return new RestaurantOrderItemDto
        {
            Id = item.Id,
            OrderId = item.OrderId,
            OrderGuestId = item.OrderGuestId,
            OrderGuestName = guestName,
            MenuItemId = item.MenuItemId,
            MenuItemNameSnapshot = item.MenuItemNameSnapshot,
            UnitPriceSnapshot = item.UnitPriceSnapshot,
            Quantity = item.Quantity,
            LineSubtotal = item.LineSubtotal,
            SendToKitchenSnapshot = item.SendToKitchenSnapshot,
            IsReady = item.IsReady,
            ReadyAtUtc = item.ReadyAtUtc,
            Note = item.Note,
            Options = item.Options
    .OrderBy(x => x.Id)
    .Select(option => new RestaurantOrderItemOptionDto
    {
        Id = option.Id,
        OrderItemId = option.OrderItemId,
        MenuItemOptionId = option.MenuItemOptionId,
        RestaurantAddonId = option.RestaurantAddonId,
        OptionNameSnapshot = option.OptionNameSnapshot,
        PriceDeltaSnapshot = option.PriceDeltaSnapshot,
        AmountMode = (int)option.AmountMode,
        AmountModeText = GetAddonAmountModeText(option.AmountMode)
    })
    .ToList()
        };
    }

    private static RestaurantOrderMessageDto ToMessageDto(RestaurantOrderMessage entity)
    {
        return new RestaurantOrderMessageDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            OrderId = entity.OrderId,
            SenderType = (int)entity.SenderType,
            SenderTypeText = GetOrderMessageSenderTypeText(entity.SenderType),
            SenderOperationUnitId = entity.SenderOperationUnitId,
            MessageType = (int)entity.MessageType,
            MessageTypeText = GetOrderMessageTypeText(entity.MessageType),
            Text = entity.Text,
            ActionKey = entity.ActionKey,
            IsActionRequired = entity.IsActionRequired,
            IsActionCompleted = entity.IsActionCompleted,
            ActionCompletedAtUtc = entity.ActionCompletedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            RecipientOperationUnitIds = entity.Recipients
                .Where(x => x.RecipientOperationUnitId.HasValue)
                .Select(x => x.RecipientOperationUnitId!.Value)
                .Distinct()
                .ToList()
        };
    }

    private static RestaurantOrderDto ToDto(RestaurantOrder entity)
    {
        return new RestaurantOrderDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            OrderDateLocal = entity.OrderDateLocal,
            DailyOrderNumber = entity.DailyOrderNumber,
            DisplayOrderNumberText = FormatDisplayOrderNumber(entity.DailyOrderNumber),
            RestaurantAreaId = entity.RestaurantAreaId,
            TableResourceId = entity.TableResourceId,
            TableSessionId = entity.TableSessionId,
            OrderType = (int)entity.OrderType,
            OrderTypeText = GetOrderTypeText(entity.OrderType),
            OrderSource = (int)entity.OrderSource,
            OrderSourceText = GetOrderSourceText(entity.OrderSource),
            RequestedPickupAtUtc = entity.RequestedPickupAtUtc,
            DeliveryAddress = entity.DeliveryAddress,
            DeliveryNote = entity.DeliveryNote,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            Note = entity.Note,
            Status = (int)entity.Status,
            StatusText = GetStatusText(entity.Status),
            KitchenDecisionStatus = (int)entity.KitchenDecisionStatus,
            KitchenDecisionStatusText = GetKitchenDecisionStatusText(entity.KitchenDecisionStatus),
            KitchenAcceptedAtUtc = entity.KitchenAcceptedAtUtc,
            KitchenAcceptLaterMinutes = entity.KitchenAcceptLaterMinutes,
            KitchenRejectedAtUtc = entity.KitchenRejectedAtUtc,
            KitchenRejectReason = entity.KitchenRejectReason,
            KitchenRejectNote = entity.KitchenRejectNote,
            SubtotalAmount = entity.SubtotalAmount,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            SubmittedAtUtc = entity.SubmittedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,

            Guests = entity.Guests
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(guest => new RestaurantOrderGuestDto
                {
                    Id = guest.Id,
                    OrderId = guest.OrderId,
                    Name = guest.Name,
                    DisplayOrder = guest.DisplayOrder,
                    Note = guest.Note,
                    Items = entity.Items
                        .Where(item => item.OrderGuestId == guest.Id)
                        .OrderBy(item => item.Id)
                        .Select(item => ToOrderItemDto(item, guest.Name))
                        .ToList()
                })
                .ToList(),

            Items = entity.Items
                .OrderBy(x => x.Id)
                .Select(item =>
                {
                    var guestName = entity.Guests
                        .FirstOrDefault(g => g.Id == item.OrderGuestId)
                        ?.Name;

                    return ToOrderItemDto(item, guestName);
                })
                .ToList()
        };
    }

    private static RestaurantPaymentDto ToPaymentDto(RestaurantPayment entity)
    {
        return new RestaurantPaymentDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            TableSessionId = entity.TableSessionId,
            Amount = entity.Amount,
            Currency = entity.Currency,
            Method = (int)entity.Method,
            MethodText = GetPaymentMethodText(entity.Method),
            Status = (int)entity.Status,
            StatusText = GetPaymentStatusText(entity.Status),
            Note = entity.Note,
            PaidAtUtc = entity.PaidAtUtc,
            CancelledAtUtc = entity.CancelledAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
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

    private static string GetPaymentMethodText(RestaurantPaymentMethod method)
    {
        return method switch
        {
            RestaurantPaymentMethod.Cash => "Gotovina",
            RestaurantPaymentMethod.Card => "Kartica",
            RestaurantPaymentMethod.Online => "Online",
            RestaurantPaymentMethod.Other => "Drugo",
            _ => "Nepoznat način"
        };
    }

    private static string GetPaymentStatusText(RestaurantPaymentStatus status)
    {
        return status switch
        {
            RestaurantPaymentStatus.Paid => "Plaćeno",
            RestaurantPaymentStatus.Cancelled => "Otkazano",
            RestaurantPaymentStatus.Refunded => "Refundirano",
            _ => "Nepoznat status"
        };
    }

    private static string FormatDelayMinutes(int minutes)
    {
        if (minutes < 60)
            return $"{minutes} minuta";

        var hours = minutes / 60;
        var restMinutes = minutes % 60;

        if (restMinutes == 0)
            return hours == 1
                ? "1 sat"
                : $"{hours} sata";

        return hours == 1
            ? $"1 sat i {restMinutes} minuta"
            : $"{hours} sata i {restMinutes} minuta";
    }

    private static string GetOrderTypeText(RestaurantOrderType orderType)
    {
        return orderType switch
        {
            RestaurantOrderType.DineIn => "U lokalu",
            RestaurantOrderType.Takeaway => "Za poneti",
            RestaurantOrderType.Delivery => "Dostava",
            _ => "Nepoznat tip"
        };
    }

    private static string GetOrderSourceText(RestaurantOrderSource source)
    {
        return source switch
        {
            RestaurantOrderSource.RestaurantDesk => "Restoran",
            RestaurantOrderSource.KitchenDesk => "Kuhinja",
            RestaurantOrderSource.AndroidCustomer => "Android klijent",
            RestaurantOrderSource.WebCustomer => "Web klijent",
            RestaurantOrderSource.Admin => "Admin",
            RestaurantOrderSource.Other => "Ostalo",
            _ => source.ToString()
        };
    }

    private async Task<int> GetNextDailyOrderNumberAsync(
    long businessId,
    DateOnly orderDateLocal,
    CancellationToken cancellationToken)
    {
        var lastNumber = await DbContext.RestaurantOrders
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.OrderDateLocal == orderDateLocal)
            .MaxAsync(x => (int?)x.DailyOrderNumber, cancellationToken);

        return (lastNumber ?? 0) + 1;
    }

    private static DateOnly GetRestaurantOrderLocalDate(DateTime utcNow)
    {
        var utc = utcNow.Kind switch
        {
            DateTimeKind.Utc => utcNow,
            DateTimeKind.Local => utcNow.ToUniversalTime(),
            _ => DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)
        };

        var timeZone = GetRestaurantTimeZone();
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);

        return DateOnly.FromDateTime(local);
    }

    private static TimeZoneInfo GetRestaurantTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Belgrade");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");
        }
    }

    private static string FormatDisplayOrderNumber(int dailyOrderNumber)
    {
        return dailyOrderNumber <= 0
            ? "-"
            : $"#{dailyOrderNumber}";
    }

    private static DateTime? EnsureUtcOrNull(DateTime? value)
    {
        if (!value.HasValue)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
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

    private static string GetOrderMessageSenderTypeText(RestaurantOrderMessageSenderType senderType)
    {
        return senderType switch
        {
            RestaurantOrderMessageSenderType.System => "Sistem",
            RestaurantOrderMessageSenderType.Restaurant => "Restoran",
            RestaurantOrderMessageSenderType.Kitchen => "Kuhinja",
            RestaurantOrderMessageSenderType.Customer => "Klijent",
            _ => "Nepoznat pošiljalac"
        };
    }

    private static string GetOrderMessageTypeText(RestaurantOrderMessageType messageType)
    {
        return messageType switch
        {
            RestaurantOrderMessageType.Text => "Poruka",
            RestaurantOrderMessageType.OrderCreated => "Porudžbina kreirana",
            RestaurantOrderMessageType.OrderSubmitted => "Porudžbina poslata",
            RestaurantOrderMessageType.KitchenAccepted => "Kuhinja prihvatila",
            RestaurantOrderMessageType.KitchenWaitingProposed => "Kuhinja predložila čekanje",
            RestaurantOrderMessageType.KitchenRejected => "Kuhinja odbila",
            RestaurantOrderMessageType.CustomerAcceptedWaiting => "Klijent prihvatio čekanje",
            RestaurantOrderMessageType.CustomerRejectedWaiting => "Klijent odbio čekanje",
            RestaurantOrderMessageType.OrderPreparing => "U pripremi",
            RestaurantOrderMessageType.OrderReady => "Spremno",
            RestaurantOrderMessageType.OrderServed => "Posluženo",
            RestaurantOrderMessageType.OrderCancelled => "Otkazano",
            RestaurantOrderMessageType.InternalManualMessage => "Interna poruka",
            _ => "Nepoznat tip poruke"
        };
    }

    private static string GetKitchenDecisionStatusText(RestaurantKitchenDecisionStatus status)
    {
        return status switch
        {
            RestaurantKitchenDecisionStatus.None => "Čeka odluku kuhinje",
            RestaurantKitchenDecisionStatus.Accepted => "Kuhinja prihvatila",
            RestaurantKitchenDecisionStatus.AcceptedLater => "Kuhinja predložila čekanje",
            RestaurantKitchenDecisionStatus.Rejected => "Kuhinja odbila",
            RestaurantKitchenDecisionStatus.WaitingAcceptedByCustomer => "Klijent prihvatio čekanje",
            RestaurantKitchenDecisionStatus.WaitingRejectedByCustomer => "Klijent odbio čekanje",
            _ => "Nepoznata odluka kuhinje"
        };
    }

    private static string GetStatusText(RestaurantOrderStatus status)
    {
        return status switch
        {
            RestaurantOrderStatus.Draft => "Nacrt",
            RestaurantOrderStatus.Submitted => "Poslato",
            RestaurantOrderStatus.Preparing => "U pripremi",
            RestaurantOrderStatus.Ready => "Spremno",
            RestaurantOrderStatus.Served => "Posluženo",
            RestaurantOrderStatus.Cancelled => "Otkazano",
            _ => "Nepoznat status"
        };
    }

    private static void AppendNote(RestaurantOrder entity, string? note)
    {
        var normalizedNote = NormalizeText(note, 1000);

        if (string.IsNullOrWhiteSpace(normalizedNote))
            return;

        entity.Note = string.IsNullOrWhiteSpace(entity.Note)
            ? normalizedNote
            : $"{entity.Note}\n{normalizedNote}";
    }

    private static List<long> ParseOperationUnitIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<long>();

        return value
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => long.TryParse(x.Trim(), out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
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

    private sealed class BuildOrderItemResult
    {
        public RestaurantOrderItem? Item { get; private set; }

        public string? Error { get; private set; }

        public static BuildOrderItemResult Ok(RestaurantOrderItem item)
        {
            return new BuildOrderItemResult
            {
                Item = item
            };
        }

        public static BuildOrderItemResult Fail(string error)
        {
            return new BuildOrderItemResult
            {
                Error = error
            };
        }
    }
}