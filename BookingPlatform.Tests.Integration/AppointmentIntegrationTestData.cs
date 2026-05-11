using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Staff;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Infrastructure.Persistence;
using BookingPlatform.Domain.Resources;

namespace BookingPlatform.Tests.Integration;

internal sealed class AppointmentSeedIds
{
    public long BusinessId { get; set; }
    public long StaffMemberId { get; set; }
    public long ServiceId { get; set; }
    public long ResourceId { get; set; }
}

internal static class AppointmentIntegrationTestData
{
    public static async Task<AppointmentSeedIds> SeedBasicSchedulingScenarioAsync(BookingDbContext db)
    {
        if (db.Businesses.Any())
        {
            var existingBusiness = db.Businesses.OrderBy(x => x.Id).First();
            var existingStaff = db.StaffMembers.OrderBy(x => x.Id).First();
            var existingService = db.Services.OrderBy(x => x.Id).First();
            var existingResource = db.Resources.OrderBy(x => x.Id).FirstOrDefault();

            return new AppointmentSeedIds
            {
                BusinessId = existingBusiness.Id,
                StaffMemberId = existingStaff.Id,
                ServiceId = existingService.Id,
                ResourceId = existingResource?.Id ?? 0
            };
        }

        var now = DateTime.UtcNow;

        var business = new Business
        {
            Name = "Salon Test",
            BusinessType = BusinessType.HairSalon,
            Description = "Test radnja",
            Phone = "0600000000",
            Email = "test@example.com",
            SlotIntervalMin = 30,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        var staff = new StaffMember
        {
            BusinessId = business.Id,
            DisplayName = "Ana",
            Title = "Frizer",
            IsBookable = true,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();

        var service = new Service
        {
            BusinessId = business.Id,
            Name = "Šišanje",
            Description = "Standardno šišanje",
            BasePrice = 800,
            EstimatedDurationMin = 30,
            BookingStrategyType = (BookingStrategyType)2,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Services.Add(service);
        await db.SaveChangesAsync();

        var resource = new Resource
        {
            BusinessId = business.Id,
            Name = "Stolica 1",
            ResourceType = (ResourceType)2,
            Capacity = 1,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        db.ServiceResourceRequirements.Add(new ServiceResourceRequirement
        {
            ServiceId = service.Id,
            ResourceId = resource.Id,
            IsRequired = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync();



        for (var day = 0; day <= 6; day++)
        {
            var mappedDay = day == 0 ? 7 : day;
            var isSunday = mappedDay == 7;

            db.BusinessWorkingHours.Add(new BusinessWorkingHour
            {
                BusinessId = business.Id,
                DayOfWeek = mappedDay,
                StartTime = isSunday ? TimeSpan.Zero : TimeSpan.FromHours(9),
                EndTime = isSunday ? TimeSpan.Zero : TimeSpan.FromHours(17),
                IsClosed = isSunday
            });

            db.StaffWorkingHours.Add(new StaffWorkingHour
            {
                StaffMemberId = staff.Id,
                DayOfWeek = mappedDay,
                StartTime = isSunday ? TimeSpan.Zero : TimeSpan.FromHours(9),
                EndTime = isSunday ? TimeSpan.Zero : TimeSpan.FromHours(17),
                IsClosed = isSunday
            });
        }

        await db.SaveChangesAsync();

        return new AppointmentSeedIds
        {
            BusinessId = business.Id,
            StaffMemberId = staff.Id,
            ServiceId = service.Id,
            ResourceId = resource.Id
        };
    }

    public static async Task<long> AddResourceAsync(
    BookingDbContext db,
    long businessId,
    string name,
    bool isActive = true)
    {
        var now = DateTime.UtcNow;

        var resource = new Resource
        {
            BusinessId = businessId,
            Name = name,
            ResourceType = (ResourceType)2,
            Capacity = 1,
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        return resource.Id;
    }

    public static async Task EnsureServiceResourceRequirementAsync(
        BookingDbContext db,
        long serviceId,
        long resourceId,
        bool isRequired)
    {
        var existing = db.ServiceResourceRequirements
            .FirstOrDefault(x => x.ServiceId == serviceId && x.ResourceId == resourceId);

        if (existing is null)
        {
            db.ServiceResourceRequirements.Add(new ServiceResourceRequirement
            {
                ServiceId = serviceId,
                ResourceId = resourceId,
                IsRequired = isRequired,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.IsRequired = isRequired;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public static async Task ResetAsync(BookingDbContext db)
    {
        db.AppointmentAuditLogs.RemoveRange(db.AppointmentAuditLogs);
        db.AppointmentChangeRequests.RemoveRange(db.AppointmentChangeRequests);
        db.Appointments.RemoveRange(db.Appointments);
        db.TimeOffBlocks.RemoveRange(db.TimeOffBlocks);
        db.ServiceResourceRequirements.RemoveRange(db.ServiceResourceRequirements);
        db.Resources.RemoveRange(db.Resources);
        db.ServiceSteps.RemoveRange(db.ServiceSteps);
        db.Services.RemoveRange(db.Services);
        db.StaffWorkingHours.RemoveRange(db.StaffWorkingHours);
        db.BusinessWorkingHours.RemoveRange(db.BusinessWorkingHours);
        db.StaffMembers.RemoveRange(db.StaffMembers);
        db.Businesses.RemoveRange(db.Businesses);

        await db.SaveChangesAsync();
    }
}