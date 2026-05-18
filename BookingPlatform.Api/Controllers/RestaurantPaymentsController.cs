using BookingPlatform.Contracts.Common;
using BookingPlatform.Contracts.Restaurants;
using BookingPlatform.Domain.Restaurants;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class RestaurantPaymentsController : ApiControllerBase
{
    public RestaurantPaymentsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet("table-session/{tableSessionId:long}")]
    public async Task<ActionResult<List<RestaurantPaymentDto>>> GetByTableSession(
        [FromRoute] long tableSessionId,
        CancellationToken cancellationToken)
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

        var items = await DbContext.RestaurantPayments
            .AsNoTracking()
            .Where(x => x.TableSessionId == tableSessionId)
            .OrderByDescending(x => x.PaidAtUtc)
            .Select(x => new RestaurantPaymentDto
            {
                Id = x.Id,
                BusinessId = x.BusinessId,
                TableSessionId = x.TableSessionId,
                Amount = x.Amount,
                Currency = x.Currency,
                Method = (int)x.Method,
                MethodText = GetMethodText(x.Method),
                Status = (int)x.Status,
                StatusText = GetStatusText(x.Status),
                Note = x.Note,
                PaidAtUtc = x.PaidAtUtc,
                CancelledAtUtc = x.CancelledAtUtc,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{paymentId:long}")]
    public async Task<ActionResult<RestaurantPaymentDto>> GetById(
        [FromRoute] long paymentId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (entity is null)
            return NotFound("Uplata ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToDto(entity));
    }

    [HttpPost]
    public async Task<ActionResult<RestaurantPaymentDto>> Create(
        [FromBody] CreateRestaurantPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.TableSessionId <= 0)
            return BadRequest("tableSessionId je obavezan.");

        if (request.Amount <= 0)
            return BadRequest("Iznos mora biti veći od 0.");

        if (!Enum.IsDefined(typeof(RestaurantPaymentMethod), request.Method))
            return BadRequest("Nepoznat način plaćanja.");

        var session = await DbContext.RestaurantTableSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.TableSessionId, cancellationToken);

        if (session is null)
            return NotFound("Zauzeće stola ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(session.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var currency = NormalizeCurrency(request.Currency);
        var now = DateTime.UtcNow;

        var entity = new RestaurantPayment
        {
            BusinessId = session.BusinessId,
            TableSessionId = session.Id,
            Amount = request.Amount,
            Currency = currency,
            Method = (RestaurantPaymentMethod)request.Method,
            Status = RestaurantPaymentStatus.Paid,
            Note = NormalizeText(request.Note, 1000),
            PaidAtUtc = now,
            CancelledAtUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantPayments.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpPost("{paymentId:long}/cancel")]
    public async Task<ActionResult<RestaurantPaymentDto>> Cancel(
        [FromRoute] long paymentId,
        [FromBody] CancelRestaurantPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantPayments
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (entity is null)
            return NotFound("Uplata ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantPaymentStatus.Paid)
            return BadRequest("Samo aktivna uplata može da se otkaže.");

        var now = DateTime.UtcNow;

        entity.Status = RestaurantPaymentStatus.Cancelled;
        entity.CancelledAtUtc = now;
        AppendNote(entity, request.Note);
        entity.UpdatedAtUtc = now;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(entity));
    }

    [HttpDelete("{paymentId:long}")]
    public async Task<ActionResult> Delete(
        [FromRoute] long paymentId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantPayments
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (entity is null)
            return NotFound("Uplata ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Status != RestaurantPaymentStatus.Paid)
            return BadRequest("Otkazana ili refundirana uplata ostaje kao istorija i ne briše se.");

        DbContext.RestaurantPayments.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static RestaurantPaymentDto ToDto(RestaurantPayment entity)
    {
        return new RestaurantPaymentDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            TableSessionId = entity.TableSessionId,
            Amount = entity.Amount,
            Currency = entity.Currency,
            Method = (int)entity.Method,
            MethodText = GetMethodText(entity.Method),
            Status = (int)entity.Status,
            StatusText = GetStatusText(entity.Status),
            Note = entity.Note,
            PaidAtUtc = entity.PaidAtUtc,
            CancelledAtUtc = entity.CancelledAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static string GetMethodText(RestaurantPaymentMethod method)
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

    private static string GetStatusText(RestaurantPaymentStatus status)
    {
        return status switch
        {
            RestaurantPaymentStatus.Paid => "Plaćeno",
            RestaurantPaymentStatus.Cancelled => "Otkazano",
            RestaurantPaymentStatus.Refunded => "Refundirano",
            _ => "Nepoznat status"
        };
    }

    private static void AppendNote(RestaurantPayment entity, string? note)
    {
        var normalizedNote = NormalizeText(note, 1000);

        if (string.IsNullOrWhiteSpace(normalizedNote))
            return;

        entity.Note = string.IsNullOrWhiteSpace(entity.Note)
            ? normalizedNote
            : $"{entity.Note}\n{normalizedNote}";
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

    private static string NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "RSD";

        var trimmed = value.Trim().ToUpperInvariant();

        return trimmed.Length <= 10
            ? trimmed
            : trimmed[..10];
    }
}