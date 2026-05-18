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
public sealed class RestaurantMenuOptionsController : ApiControllerBase
{
    public RestaurantMenuOptionsController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet("item/{menuItemId:long}/groups")]
    public async Task<ActionResult<List<RestaurantMenuItemOptionGroupDto>>> GetGroupsForItem(
        [FromRoute] long menuItemId,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var menuItem = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == menuItemId, cancellationToken);

        if (menuItem is null)
            return NotFound("Artikal menija ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(menuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var query = DbContext.RestaurantMenuItemOptionGroups
            .AsNoTracking()
            .Include(x => x.MenuItem)
            .Include(x => x.Options)
            .Where(x => x.MenuItemId == menuItemId);

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var groups = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var result = groups
            .Select(group => ToGroupDto(group, includeInactive))
            .ToList();

        return Ok(result);
    }

    [HttpGet("groups/{groupId:long}")]
    public async Task<ActionResult<RestaurantMenuItemOptionGroupDto>> GetGroupById(
        [FromRoute] long groupId,
        CancellationToken cancellationToken)
    {
        var group = await DbContext.RestaurantMenuItemOptionGroups
            .AsNoTracking()
            .Include(x => x.MenuItem)
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);

        if (group is null)
            return NotFound("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(group.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToGroupDto(group, includeInactive: true));
    }

    [HttpPost("groups")]
    public async Task<ActionResult<RestaurantMenuItemOptionGroupDto>> CreateGroup(
        [FromBody] CreateRestaurantMenuItemOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (request.MenuItemId <= 0)
            return BadRequest("menuItemId je obavezan.");

        var menuItem = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.MenuItemId, cancellationToken);

        if (menuItem is null)
            return BadRequest("Artikal menija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(menuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateGroupRequestAsync(
            request.MenuItemId,
            request.Name,
            request.MinSelected,
            request.MaxSelected,
            null,
            cancellationToken);

        if (validationResult.Error is not null)
            return BadRequest(validationResult.Error);

        var now = DateTime.UtcNow;

        var entity = new RestaurantMenuItemOptionGroup
        {
            MenuItemId = request.MenuItemId,
            Name = validationResult.Name!,
            IsRequired = request.IsRequired,
            MinSelected = request.MinSelected,
            MaxSelected = request.MaxSelected,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantMenuItemOptionGroups.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.RestaurantMenuItemOptionGroups
            .AsNoTracking()
            .Include(x => x.MenuItem)
            .Include(x => x.Options)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToGroupDto(dtoEntity, includeInactive: true));
    }

    [HttpPut("groups/{groupId:long}")]
    public async Task<ActionResult<RestaurantMenuItemOptionGroupDto>> UpdateGroup(
        [FromRoute] long groupId,
        [FromBody] UpdateRestaurantMenuItemOptionGroupRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItemOptionGroups
            .Include(x => x.MenuItem)
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);

        if (entity is null)
            return NotFound("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateGroupRequestAsync(
            entity.MenuItemId,
            request.Name,
            request.MinSelected,
            request.MaxSelected,
            groupId,
            cancellationToken);

        if (validationResult.Error is not null)
            return BadRequest(validationResult.Error);

        entity.Name = validationResult.Name!;
        entity.IsRequired = request.IsRequired;
        entity.MinSelected = request.MinSelected;
        entity.MaxSelected = request.MaxSelected;
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToGroupDto(entity, includeInactive: true));
    }

    [HttpDelete("groups/{groupId:long}")]
    public async Task<ActionResult> DeleteGroup(
        [FromRoute] long groupId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItemOptionGroups
            .Include(x => x.MenuItem)
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == groupId, cancellationToken);

        if (entity is null)
            return NotFound("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Options.Any())
        {
            return BadRequest("Grupa dodataka ne može da se obriše jer ima opcije. Prvo obrišite opcije.");
        }

        DbContext.RestaurantMenuItemOptionGroups.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpGet("options/{optionId:long}")]
    public async Task<ActionResult<RestaurantMenuItemOptionDto>> GetOptionById(
        [FromRoute] long optionId,
        CancellationToken cancellationToken)
    {
        var option = await DbContext.RestaurantMenuItemOptions
            .AsNoTracking()
            .Include(x => x.OptionGroup)
            .ThenInclude(x => x.MenuItem)
            .FirstOrDefaultAsync(x => x.Id == optionId, cancellationToken);

        if (option is null)
            return NotFound("Dodatak ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(option.OptionGroup.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToOptionDto(option));
    }

    [HttpPost("options")]
    public async Task<ActionResult<RestaurantMenuItemOptionDto>> CreateOption(
        [FromBody] CreateRestaurantMenuItemOptionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.OptionGroupId <= 0)
            return BadRequest("optionGroupId je obavezan.");

        var group = await DbContext.RestaurantMenuItemOptionGroups
            .AsNoTracking()
            .Include(x => x.MenuItem)
            .FirstOrDefaultAsync(x => x.Id == request.OptionGroupId, cancellationToken);

        if (group is null)
            return BadRequest("Grupa dodataka ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(group.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateOptionRequestAsync(
            request.OptionGroupId,
            request.Name,
            null,
            cancellationToken);

        if (validationResult.Error is not null)
            return BadRequest(validationResult.Error);

        var now = DateTime.UtcNow;

        var entity = new RestaurantMenuItemOption
        {
            OptionGroupId = request.OptionGroupId,
            Name = validationResult.Name!,
            PriceDelta = request.PriceDelta,
            IsAvailable = request.IsAvailable,
            IsActive = true,
            DisplayOrder = request.DisplayOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantMenuItemOptions.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToOptionDto(entity));
    }

    [HttpPut("options/{optionId:long}")]
    public async Task<ActionResult<RestaurantMenuItemOptionDto>> UpdateOption(
        [FromRoute] long optionId,
        [FromBody] UpdateRestaurantMenuItemOptionRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItemOptions
            .Include(x => x.OptionGroup)
            .ThenInclude(x => x.MenuItem)
            .FirstOrDefaultAsync(x => x.Id == optionId, cancellationToken);

        if (entity is null)
            return NotFound("Dodatak ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.OptionGroup.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateOptionRequestAsync(
            entity.OptionGroupId,
            request.Name,
            optionId,
            cancellationToken);

        if (validationResult.Error is not null)
            return BadRequest(validationResult.Error);

        entity.Name = validationResult.Name!;
        entity.PriceDelta = request.PriceDelta;
        entity.IsAvailable = request.IsAvailable;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToOptionDto(entity));
    }

    [HttpPost("options/{optionId:long}/availability")]
    public async Task<ActionResult<RestaurantMenuItemOptionDto>> SetOptionAvailability(
        [FromRoute] long optionId,
        [FromBody] SetRestaurantMenuItemOptionAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItemOptions
            .Include(x => x.OptionGroup)
            .ThenInclude(x => x.MenuItem)
            .FirstOrDefaultAsync(x => x.Id == optionId, cancellationToken);

        if (entity is null)
            return NotFound("Dodatak ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.OptionGroup.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        entity.IsAvailable = request.IsAvailable;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToOptionDto(entity));
    }

    [HttpDelete("options/{optionId:long}")]
    public async Task<ActionResult> DeleteOption(
        [FromRoute] long optionId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItemOptions
            .Include(x => x.OptionGroup)
            .ThenInclude(x => x.MenuItem)
            .FirstOrDefaultAsync(x => x.Id == optionId, cancellationToken);

        if (entity is null)
            return NotFound("Dodatak ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.OptionGroup.MenuItem.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.RestaurantMenuItemOptions.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<GroupValidationResult> ValidateGroupRequestAsync(
        long menuItemId,
        string nameValue,
        int minSelected,
        int maxSelected,
        long? currentGroupId,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeRequiredText(nameValue, "Unesite naziv grupe dodataka.");
        if (normalizedName.Error is not null)
        {
            return new GroupValidationResult
            {
                Error = normalizedName.Error
            };
        }

        if (minSelected < 0)
        {
            return new GroupValidationResult
            {
                Error = "Minimalan broj izbora ne može biti negativan."
            };
        }

        if (maxSelected <= 0)
        {
            return new GroupValidationResult
            {
                Error = "Maksimalan broj izbora mora biti veći od 0."
            };
        }

        if (minSelected > maxSelected)
        {
            return new GroupValidationResult
            {
                Error = "Minimalan broj izbora ne može biti veći od maksimalnog."
            };
        }

        var duplicateExists = await DbContext.RestaurantMenuItemOptionGroups
            .AsNoTracking()
            .AnyAsync(x =>
                x.MenuItemId == menuItemId &&
                x.Name == normalizedName.Value &&
                (!currentGroupId.HasValue || x.Id != currentGroupId.Value),
                cancellationToken);

        if (duplicateExists)
        {
            return new GroupValidationResult
            {
                Error = "Grupa dodataka sa ovim nazivom već postoji za ovaj artikal."
            };
        }

        return new GroupValidationResult
        {
            Name = normalizedName.Value
        };
    }

    private async Task<OptionValidationResult> ValidateOptionRequestAsync(
        long optionGroupId,
        string nameValue,
        long? currentOptionId,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeRequiredText(nameValue, "Unesite naziv dodatka.");
        if (normalizedName.Error is not null)
        {
            return new OptionValidationResult
            {
                Error = normalizedName.Error
            };
        }

        var duplicateExists = await DbContext.RestaurantMenuItemOptions
            .AsNoTracking()
            .AnyAsync(x =>
                x.OptionGroupId == optionGroupId &&
                x.Name == normalizedName.Value &&
                (!currentOptionId.HasValue || x.Id != currentOptionId.Value),
                cancellationToken);

        if (duplicateExists)
        {
            return new OptionValidationResult
            {
                Error = "Dodatak sa ovim nazivom već postoji u ovoj grupi."
            };
        }

        return new OptionValidationResult
        {
            Name = normalizedName.Value
        };
    }

    private static RestaurantMenuItemOptionGroupDto ToGroupDto(
        RestaurantMenuItemOptionGroup entity,
        bool includeInactive)
    {
        return new RestaurantMenuItemOptionGroupDto
        {
            Id = entity.Id,
            MenuItemId = entity.MenuItemId,
            MenuItemName = entity.MenuItem.Name,
            Name = entity.Name,
            IsRequired = entity.IsRequired,
            MinSelected = entity.MinSelected,
            MaxSelected = entity.MaxSelected,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            Options = entity.Options
                .Where(x => includeInactive || x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(ToOptionDto)
                .ToList()
        };
    }

    private static RestaurantMenuItemOptionDto ToOptionDto(RestaurantMenuItemOption entity)
    {
        return new RestaurantMenuItemOptionDto
        {
            Id = entity.Id,
            OptionGroupId = entity.OptionGroupId,
            Name = entity.Name,
            PriceDelta = entity.PriceDelta,
            IsAvailable = entity.IsAvailable,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder
        };
    }

    private static TextValidationResult NormalizeRequiredText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new TextValidationResult
            {
                Error = errorMessage
            };
        }

        var trimmed = value.Trim();

        return new TextValidationResult
        {
            Value = trimmed.Length <= 150 ? trimmed : trimmed[..150]
        };
    }

    private sealed class TextValidationResult
    {
        public string? Value { get; set; }

        public string? Error { get; set; }
    }

    private sealed class GroupValidationResult
    {
        public string? Name { get; set; }

        public string? Error { get; set; }
    }

    private sealed class OptionValidationResult
    {
        public string? Name { get; set; }

        public string? Error { get; set; }
    }
}