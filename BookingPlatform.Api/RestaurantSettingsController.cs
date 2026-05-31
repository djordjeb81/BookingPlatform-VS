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
public sealed class RestaurantSettingsController : ApiControllerBase
{
    public RestaurantSettingsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet("{businessId:long}")]
    public async Task<ActionResult<RestaurantSettingsDto>> GetByBusiness(
        [FromRoute] long businessId,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var settings = await GetOrCreateSettingsAsync(businessId, cancellationToken);

        return Ok(ToDto(settings));
    }

    [HttpPut("{businessId:long}")]
    public async Task<ActionResult<RestaurantSettingsDto>> Update(
        [FromRoute] long businessId,
        [FromBody] UpdateRestaurantSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (request.PreparationReminderBufferMin < 0)
            return BadRequest("Rezerva za pripremu ne može biti negativna.");

        if (request.ScheduledOrderMinLeadTimeMin < 0)
            return BadRequest("Minimalno vreme unapred ne može biti negativno.");

        if (request.ScheduledOrderMaxDaysAhead < 0)
            return BadRequest("Broj dana unapred ne može biti negativan.");

        var settings = await GetOrCreateSettingsAsync(businessId, cancellationToken);

        settings.PreparationReminderBufferMin = request.PreparationReminderBufferMin;
        settings.ScheduledOrderMinLeadTimeMin = request.ScheduledOrderMinLeadTimeMin;
        settings.ScheduledOrderMaxDaysAhead = request.ScheduledOrderMaxDaysAhead;
        settings.IsScheduledOrderingEnabled = request.IsScheduledOrderingEnabled;
        settings.IsDeliveryEnabled = request.IsDeliveryEnabled;
        settings.IsDeliveryLocationRequired = request.IsDeliveryLocationRequired;
        settings.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(settings));
    }

    private async Task<RestaurantSettings> GetOrCreateSettingsAsync(
        long businessId,
        CancellationToken cancellationToken)
    {
        var settings = await DbContext.RestaurantSettings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, cancellationToken);

        if (settings is not null)
            return settings;

        var now = DateTime.UtcNow;

        settings = new RestaurantSettings
        {
            BusinessId = businessId,
            PreparationReminderBufferMin = 10,
            ScheduledOrderMinLeadTimeMin = 30,
            ScheduledOrderMaxDaysAhead = 7,
            IsScheduledOrderingEnabled = true,
            IsDeliveryEnabled = true,
            IsDeliveryLocationRequired = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantSettings.Add(settings);
        await DbContext.SaveChangesAsync(cancellationToken);

        return settings;
    }

    private static RestaurantSettingsDto ToDto(RestaurantSettings entity)
    {
        return new RestaurantSettingsDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            PreparationReminderBufferMin = entity.PreparationReminderBufferMin,
            ScheduledOrderMinLeadTimeMin = entity.ScheduledOrderMinLeadTimeMin,
            ScheduledOrderMaxDaysAhead = entity.ScheduledOrderMaxDaysAhead,
            IsScheduledOrderingEnabled = entity.IsScheduledOrderingEnabled,
            IsDeliveryEnabled = entity.IsDeliveryEnabled,
            IsDeliveryLocationRequired = entity.IsDeliveryLocationRequired
        };
    }
}