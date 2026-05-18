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
public sealed class RestaurantMenuController : ApiControllerBase
{
    public RestaurantMenuController(BookingDbContext dbContext) : base(dbContext)
    {
    }

    [HttpGet("business/{businessId:long}")]
    public async Task<ActionResult<List<RestaurantMenuCategoryDto>>> GetMenuByBusiness(
    [FromRoute] long businessId,
    [FromQuery] bool includeInactive = false,
    CancellationToken cancellationToken = default)
    {
        if (businessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var categoriesQuery = DbContext.RestaurantMenuCategories
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.OptionGroups)
                    .ThenInclude(x => x.Options)
            .Where(x => x.BusinessId == businessId);

        if (!includeInactive)
        {
            categoriesQuery = categoriesQuery.Where(x => x.IsActive);
        }

        var categories = await categoriesQuery
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var result = categories
            .Select(category => new RestaurantMenuCategoryDto
            {
                Id = category.Id,
                BusinessId = category.BusinessId,
                Name = category.Name,
                Description = category.Description,
                DisplayOrder = category.DisplayOrder,
                IsActive = category.IsActive,
                Items = category.Items
                    .Where(item => includeInactive || item.IsActive)
                    .OrderBy(item => item.DisplayOrder)
                    .ThenBy(item => item.Name)
                    .Select(item => ToItemDto(item, category.Name, includeInactive))
                    .ToList()
            })
            .ToList();

        return Ok(result);
    }

    [HttpGet("categories/{categoryId:long}")]
    public async Task<ActionResult<RestaurantMenuCategoryDto>> GetCategoryById(
    [FromRoute] long categoryId,
    [FromQuery] bool includeInactive = true,
    CancellationToken cancellationToken = default)
    {
        var category = await DbContext.RestaurantMenuCategories
            .AsNoTracking()
            .Include(x => x.Items)
                .ThenInclude(x => x.OptionGroups)
                    .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
            return NotFound("Kategorija menija ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(category.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToCategoryDto(category, includeInactive));
    }

    [HttpPost("categories")]
    public async Task<ActionResult<RestaurantMenuCategoryDto>> CreateCategory(
        [FromBody] CreateRestaurantMenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeRequiredText(request.Name, "Unesite naziv kategorije.");
        if (name.Error is not null)
            return BadRequest(name.Error);

        var description = NormalizeText(request.Description, 1000);

        var duplicateExists = await DbContext.RestaurantMenuCategories
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == request.BusinessId &&
                x.Name == name.Value,
                cancellationToken);

        if (duplicateExists)
            return BadRequest("Kategorija sa ovim nazivom već postoji.");

        var now = DateTime.UtcNow;

        var entity = new RestaurantMenuCategory
        {
            BusinessId = request.BusinessId,
            Name = name.Value!,
            Description = description,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantMenuCategories.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToCategoryDto(entity, includeInactive: true));
    }

    [HttpPut("categories/{categoryId:long}")]
    public async Task<ActionResult<RestaurantMenuCategoryDto>> UpdateCategory(
        [FromRoute] long categoryId,
        [FromBody] UpdateRestaurantMenuCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuCategories
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (entity is null)
            return NotFound("Kategorija menija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var name = NormalizeRequiredText(request.Name, "Unesite naziv kategorije.");
        if (name.Error is not null)
            return BadRequest(name.Error);

        var duplicateExists = await DbContext.RestaurantMenuCategories
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == entity.BusinessId &&
                x.Id != entity.Id &&
                x.Name == name.Value,
                cancellationToken);

        if (duplicateExists)
            return BadRequest("Kategorija sa ovim nazivom već postoji.");

        entity.Name = name.Value!;
        entity.Description = NormalizeText(request.Description, 1000);
        entity.DisplayOrder = request.DisplayOrder;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToCategoryDto(entity, includeInactive: true));
    }

    [HttpDelete("categories/{categoryId:long}")]
    public async Task<ActionResult> DeleteCategory(
        [FromRoute] long categoryId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuCategories
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (entity is null)
            return NotFound("Kategorija menija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        if (entity.Items.Any())
        {
            return BadRequest("Kategorija ne može da se obriše jer ima artikle. Prvo obrišite ili premestite artikle.");
        }

        DbContext.RestaurantMenuCategories.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }


    [HttpGet("items/{itemId:long}")]
    public async Task<ActionResult<RestaurantMenuItemDto>> GetItemById(
    [FromRoute] long itemId,
    [FromQuery] bool includeInactive = true,
    CancellationToken cancellationToken = default)
    {
        var item = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.OptionGroups)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        if (item is null)
            return NotFound("Artikal menija ne postoji.");

        var accessResult = await EnsureBusinessReadAccessAsync(item.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        return Ok(ToItemDto(item, item.Category.Name, includeInactive));
    }

    [HttpPost("items")]
    public async Task<ActionResult<RestaurantMenuItemDto>> CreateItem(
        [FromBody] CreateRestaurantMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BusinessId <= 0)
            return BadRequest("businessId je obavezan.");

        var accessResult = await EnsureBusinessWriteAccessAsync(request.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateItemRequestAsync(
            request.BusinessId,
            request.CategoryId,
            request.Name,
            request.Price,
            request.Currency,
            null,
            cancellationToken);

        if (validationResult.Error is not null)
            return BadRequest(validationResult.Error);

        var now = DateTime.UtcNow;

        var entity = new RestaurantMenuItem
        {
            BusinessId = request.BusinessId,
            CategoryId = request.CategoryId,
            Name = validationResult.Name!,
            Description = NormalizeText(request.Description, 2000),
            Price = request.Price,
            Currency = NormalizeCurrency(request.Currency),
            IsAvailable = request.IsAvailable,
            SendToKitchen = request.SendToKitchen,
            IsActive = true,
            DisplayOrder = request.DisplayOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        DbContext.RestaurantMenuItems.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.RestaurantMenuItems
     .AsNoTracking()
     .Include(x => x.Category)
     .Include(x => x.OptionGroups)
         .ThenInclude(x => x.Options)
     .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToItemDto(dtoEntity, dtoEntity.Category.Name, includeInactive: true));
    }

    [HttpPut("items/{itemId:long}")]
    public async Task<ActionResult<RestaurantMenuItemDto>> UpdateItem(
        [FromRoute] long itemId,
        [FromBody] UpdateRestaurantMenuItemRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItems
            .Include(x => x.Category)
            .Include(x => x.OptionGroups)
                .ThenInclude(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        if (entity is null)
            return NotFound("Artikal menija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        var validationResult = await ValidateItemRequestAsync(
            entity.BusinessId,
            request.CategoryId,
            request.Name,
            request.Price,
            request.Currency,
            itemId,
            cancellationToken);

        if (validationResult.Error is not null)
            return BadRequest(validationResult.Error);

        entity.CategoryId = request.CategoryId;
        entity.Name = validationResult.Name!;
        entity.Description = NormalizeText(request.Description, 2000);
        entity.Price = request.Price;
        entity.Currency = NormalizeCurrency(request.Currency);
        entity.IsAvailable = request.IsAvailable;
        entity.SendToKitchen = request.SendToKitchen;
        entity.IsActive = request.IsActive;
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        var dtoEntity = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.OptionGroups)
                .ThenInclude(x => x.Options)
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return Ok(ToItemDto(dtoEntity, dtoEntity.Category.Name, includeInactive: true));
    }

    [HttpPost("items/{itemId:long}/availability")]
    public async Task<ActionResult<RestaurantMenuItemDto>> SetItemAvailability(
        [FromRoute] long itemId,
        [FromBody] SetRestaurantMenuItemAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItems
            .Include(x => x.Category)
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        if (entity is null)
            return NotFound("Artikal menija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        entity.IsAvailable = request.IsAvailable;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToItemDto(entity, entity.Category.Name, includeInactive: true));
    }

    [HttpDelete("items/{itemId:long}")]
    public async Task<ActionResult> DeleteItem(
        [FromRoute] long itemId,
        CancellationToken cancellationToken)
    {
        var entity = await DbContext.RestaurantMenuItems
            .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

        if (entity is null)
            return NotFound("Artikal menija ne postoji.");

        var accessResult = await EnsureBusinessWriteAccessAsync(entity.BusinessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        DbContext.RestaurantMenuItems.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<ItemValidationResult> ValidateItemRequestAsync(
        long businessId,
        long categoryId,
        string nameValue,
        decimal price,
        string currency,
        long? currentItemId,
        CancellationToken cancellationToken)
    {
        if (categoryId <= 0)
        {
            return new ItemValidationResult
            {
                Error = "categoryId je obavezan."
            };
        }

        var normalizedName = NormalizeRequiredText(nameValue, "Unesite naziv artikla.");
        if (normalizedName.Error is not null)
        {
            return new ItemValidationResult
            {
                Error = normalizedName.Error
            };
        }

        if (price < 0)
        {
            return new ItemValidationResult
            {
                Error = "Cena ne može biti negativna."
            };
        }

        var normalizedCurrency = NormalizeCurrency(currency);
        if (string.IsNullOrWhiteSpace(normalizedCurrency))
        {
            return new ItemValidationResult
            {
                Error = "Valuta je obavezna."
            };
        }

        var categoryExists = await DbContext.RestaurantMenuCategories
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == categoryId &&
                x.BusinessId == businessId,
                cancellationToken);

        if (!categoryExists)
        {
            return new ItemValidationResult
            {
                Error = "Izabrana kategorija ne postoji ili ne pripada ovoj radnji."
            };
        }

        var duplicateExists = await DbContext.RestaurantMenuItems
            .AsNoTracking()
            .AnyAsync(x =>
                x.BusinessId == businessId &&
                x.CategoryId == categoryId &&
                x.Name == normalizedName.Value &&
                (!currentItemId.HasValue || x.Id != currentItemId.Value),
                cancellationToken);

        if (duplicateExists)
        {
            return new ItemValidationResult
            {
                Error = "Artikal sa ovim nazivom već postoji u izabranoj kategoriji."
            };
        }

        return new ItemValidationResult
        {
            Name = normalizedName.Value
        };
    }

    private static RestaurantMenuCategoryDto ToCategoryDto(
        RestaurantMenuCategory entity,
        bool includeInactive)
    {
        return new RestaurantMenuCategoryDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            Name = entity.Name,
            Description = entity.Description,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            Items = entity.Items
                .Where(x => includeInactive || x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(x => ToItemDto(x, entity.Name, includeInactive))
                .ToList()
        };
    }

    private static RestaurantMenuItemDto ToItemDto(
        RestaurantMenuItem entity,
        string categoryName,
        bool includeInactive)
    {
        return new RestaurantMenuItemDto
        {
            Id = entity.Id,
            BusinessId = entity.BusinessId,
            CategoryId = entity.CategoryId,
            CategoryName = categoryName,
            Name = entity.Name,
            Description = entity.Description,
            Price = entity.Price,
            Currency = entity.Currency,
            IsAvailable = entity.IsAvailable,
            SendToKitchen = entity.SendToKitchen,
            IsActive = entity.IsActive,
            DisplayOrder = entity.DisplayOrder,
            OptionGroups = entity.OptionGroups
                .Where(x => includeInactive || x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(group => new RestaurantMenuItemOptionGroupDto
                {
                    Id = group.Id,
                    MenuItemId = group.MenuItemId,
                    MenuItemName = entity.Name,
                    Name = group.Name,
                    IsRequired = group.IsRequired,
                    MinSelected = group.MinSelected,
                    MaxSelected = group.MaxSelected,
                    DisplayOrder = group.DisplayOrder,
                    IsActive = group.IsActive,
                    Options = group.Options
                        .Where(option => includeInactive || option.IsActive)
                        .OrderBy(option => option.DisplayOrder)
                        .ThenBy(option => option.Name)
                        .Select(option => new RestaurantMenuItemOptionDto
                        {
                            Id = option.Id,
                            OptionGroupId = option.OptionGroupId,
                            Name = option.Name,
                            PriceDelta = option.PriceDelta,
                            IsAvailable = option.IsAvailable,
                            IsActive = option.IsActive,
                            DisplayOrder = option.DisplayOrder
                        })
                        .ToList()
                })
                .ToList()
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
            Value = trimmed
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

    private static string NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "RSD";

        var trimmed = value.Trim().ToUpperInvariant();

        return trimmed.Length <= 10
            ? trimmed
            : trimmed[..10];
    }

    private sealed class TextValidationResult
    {
        public string? Value { get; set; }

        public string? Error { get; set; }
    }

    private sealed class ItemValidationResult
    {
        public string? Name { get; set; }

        public string? Error { get; set; }
    }
}