using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; 
using BookingPlatform.Domain.Auth;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/[controller]")]
public sealed class BusinessesController : ApiControllerBase
{
    

    public BusinessesController(BookingDbContext DbContext) : base(DbContext)
    {
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<BusinessDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BusinessDto>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var items = await DbContext.BusinessUserMemberships
            .AsNoTracking()
            .Where(x => x.AppUserId == userId.Value && x.IsActive)
            .Join(
                DbContext.Businesses.AsNoTracking(),
                membership => membership.BusinessId,
                business => business.Id,
(membership, business) => new BusinessDto
{
    Id = business.Id,
    Name = business.Name,
    BusinessType = (int)business.BusinessType,
    Description = business.Description,
    Phone = business.Phone,
    Email = business.Email,
    Street = business.Street,
    StreetNumber = business.StreetNumber,
    City = business.City,
    PostalCode = business.PostalCode,
    Country = business.Country,
    Latitude = business.Latitude,
    Longitude = business.Longitude,
    GooglePlaceId = business.GooglePlaceId,
    SlotIntervalMin = business.SlotIntervalMin,
    IsActive = business.IsActive
})
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> GetById(
            [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var item = await DbContext.Businesses
            .AsNoTracking()
            .Where(x => x.Id == id)
.Select(x => new BusinessDto
{
    Id = x.Id,
    Name = x.Name,
    BusinessType = (int)x.BusinessType,
    Description = x.Description,
    Phone = x.Phone,
    Email = x.Email,
    Street = x.Street,
    StreetNumber = x.StreetNumber,
    City = x.City,
    PostalCode = x.PostalCode,
    Country = x.Country,
    Latitude = x.Latitude,
    Longitude = x.Longitude,
    GooglePlaceId = x.GooglePlaceId,
    SlotIntervalMin = x.SlotIntervalMin,
    IsActive = x.IsActive
})
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
            return NotFound("Radnja ne postoji.");

        return Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BusinessDto>> Create(
            [FromBody] CreateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var userId = TryGetCurrentUserId();

        if (!userId.HasValue)
            return BuildUnauthorized();

        var userExists = await DbContext.AppUsers
            .AnyAsync(x => x.Id == userId.Value && x.IsActive, cancellationToken);

        if (!userExists)
            return BuildUnauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv radnje.");

        if (request.SlotIntervalMin <= 0)
            return BadRequest("Razmak između početaka termina mora biti veći od 0 minuta.");

        if (request.SlotIntervalMin > 180)
            return BadRequest("Razmak između početaka termina je prevelik.");

        var now = DateTime.UtcNow;

        var entity = new Business
        {
            Name = request.Name.Trim(),
            BusinessType = (BusinessType)request.BusinessType,
            Description = request.Description?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            Street = request.Street?.Trim(),
            StreetNumber = request.StreetNumber?.Trim(),
            City = request.City?.Trim(),
            PostalCode = request.PostalCode?.Trim(),
            Country = request.Country?.Trim(),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            GooglePlaceId = request.GooglePlaceId?.Trim(),
            SlotIntervalMin = request.SlotIntervalMin,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.Businesses.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        DbContext.BusinessUserMemberships.Add(new BusinessUserMembership
        {
            AppUserId = userId.Value,
            BusinessId = entity.Id,
            Role = BusinessUserRole.Owner,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await DbContext.SaveChangesAsync(cancellationToken);

        var dto = new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            Street = entity.Street,
            StreetNumber = entity.StreetNumber,
            City = entity.City,
            PostalCode = entity.PostalCode,
            Country = entity.Country,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            GooglePlaceId = entity.GooglePlaceId,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        };

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, dto);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> Update(
            [FromRoute] long id,
        [FromBody] UpdateBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Unesite naziv radnje.");

        if (request.SlotIntervalMin <= 0)
            return BadRequest("Razmak između početaka termina mora biti veći od 0 minuta.");

        if (request.SlotIntervalMin > 180)
            return BadRequest("Razmak između početaka termina je prevelik.");

        entity.Name = request.Name.Trim();
        entity.BusinessType = (BusinessType)request.BusinessType;
        entity.Description = request.Description?.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.Email = request.Email?.Trim();
        entity.Street = request.Street?.Trim();
        entity.StreetNumber = request.StreetNumber?.Trim();
        entity.City = request.City?.Trim();
        entity.PostalCode = request.PostalCode?.Trim();
        entity.Country = request.Country?.Trim();
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.GooglePlaceId = request.GooglePlaceId?.Trim();
        entity.SlotIntervalMin = request.SlotIntervalMin;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            Street = entity.Street,
            StreetNumber = entity.StreetNumber,
            City = entity.City,
            PostalCode = entity.PostalCode,
            Country = entity.Country,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            GooglePlaceId = entity.GooglePlaceId,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/deactivate")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> Deactivate(
            [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (!entity.IsActive)
            return BadRequest("Radnja je već neaktivna.");

        entity.IsActive = false;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            Street = entity.Street,
            StreetNumber = entity.StreetNumber,
            City = entity.City,
            PostalCode = entity.PostalCode,
            Country = entity.Country,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            GooglePlaceId = entity.GooglePlaceId,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        });
    }

    [HttpPost("{id:long}/activate")]
    [ProducesResponseType(typeof(BusinessDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BusinessDto>> Activate(
            [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessOwnerAccessAsync(id, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var entity = await DbContext.Businesses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (entity is null)
            return NotFound("Radnja ne postoji.");

        if (entity.IsActive)
            return BadRequest("Radnja je već aktivna.");

        entity.IsActive = true;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BusinessDto
        {
            Id = entity.Id,
            Name = entity.Name,
            BusinessType = (int)entity.BusinessType,
            Description = entity.Description,
            Phone = entity.Phone,
            Email = entity.Email,
            Street = entity.Street,
            StreetNumber = entity.StreetNumber,
            City = entity.City,
            PostalCode = entity.PostalCode,
            Country = entity.Country,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            GooglePlaceId = entity.GooglePlaceId,
            SlotIntervalMin = entity.SlotIntervalMin,
            IsActive = entity.IsActive
        });
    }

}