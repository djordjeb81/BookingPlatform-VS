namespace BookingPlatform.Api.Setup;

public sealed class DevelopmentSeedOptions
{
    public bool Enabled { get; set; } = true;

    public string DefaultPassword { get; set; } = "test123";

    public string BusinessName { get; set; } = "Demo Salon";
    public string BusinessDescription { get; set; } = "Development seed business";
    public int SlotIntervalMin { get; set; } = 30;

    public string OwnerEmail { get; set; } = "owner@dev.local";
    public string OwnerFullName { get; set; } = "Demo Owner";

    public string ManagerEmail { get; set; } = "manager@dev.local";
    public string ManagerFullName { get; set; } = "Demo Manager";

    public string StaffUserEmail { get; set; } = "staff@dev.local";
    public string StaffUserFullName { get; set; } = "Demo Staff User";

    public string StaffDisplayName { get; set; } = "Milica";
    public string StaffTitle { get; set; } = "Frizer";

    public string ResourceName { get; set; } = "Stolica 1";
    public int? ResourceCapacity { get; set; } = 1;

    public string ServiceName { get; set; } = "Sisanje";
    public string? ServiceDescription { get; set; } = "Demo usluga";
    public decimal ServiceBasePrice { get; set; } = 1200;
    public int ServiceDurationMin { get; set; } = 30;

    public bool ServiceRequiresResource { get; set; } = true;
}