using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Contracts.Restaurants;

namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerRestaurantProfileDto
{
    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? BusinessPhone { get; set; }

    public string? BusinessEmail { get; set; }

    public string? Street { get; set; }

    public string? StreetNumber { get; set; }

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public int BusinessType { get; set; }

    public int BookingMode { get; set; }

    public BusinessFeatureSettingsDto FeatureSettings { get; set; } = new();

    public RestaurantSettingsDto RestaurantSettings { get; set; } = new();

    public List<RestaurantDeliveryZoneDto> DeliveryZones { get; set; } = new();

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public List<CustomerRestaurantWorkingHourDto> WorkingHours { get; set; } = new();

    public List<CustomerRestaurantOperationUnitDto> OperationUnits { get; set; } = new();

    public List<RestaurantMenuCategoryDto> MenuCategories { get; set; } = new();

    public List<RestaurantAddonGroupDto> AddonGroups { get; set; } = new();
}

public sealed class CustomerRestaurantWorkingHourDto
{
    public int DayOfWeek { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public bool IsClosed { get; set; }
}

public sealed class CustomerRestaurantOperationUnitDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public int UnitType { get; set; }

    public string UnitTypeText { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public bool ReceivesCustomerChat { get; set; }

    public List<CustomerRestaurantOperationUnitWorkingHourDto> WorkingHours { get; set; } = new();
}

public sealed class CustomerRestaurantOperationUnitWorkingHourDto
{
    public long Id { get; set; }

    public long OperationUnitId { get; set; }

    public int DayOfWeek { get; set; }

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public bool IsClosed { get; set; }
}
