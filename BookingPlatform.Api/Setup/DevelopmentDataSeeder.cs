using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Staff;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BookingPlatform.Api.Setup;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        using var scope = app.Services.CreateScope();

        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<BookingDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevelopmentDataSeeder");

        var options = services.GetRequiredService<IOptions<DevelopmentSeedOptions>>().Value;

        if (!options.Enabled)
        {
            logger.LogInformation("Development seed je isključen.");
            return;
        }

        var now = DateTime.UtcNow;

        var ownerUser = await EnsureUserAsync(
            dbContext,
            options.OwnerEmail,
            options.OwnerFullName,
            options.DefaultPassword,
            now);

        var managerUser = await EnsureUserAsync(
            dbContext,
            options.ManagerEmail,
            options.ManagerFullName,
            options.DefaultPassword,
            now);

        var staffUser = await EnsureUserAsync(
            dbContext,
            options.StaffUserEmail,
            options.StaffUserFullName,
            options.DefaultPassword,
            now);

        var business = await EnsureBusinessAsync(dbContext, options, now);
        await dbContext.SaveChangesAsync();

        await EnsureMembershipAsync(dbContext, ownerUser.Id, business.Id, BusinessUserRole.Owner, now);
        await EnsureMembershipAsync(dbContext, managerUser.Id, business.Id, BusinessUserRole.Manager, now);
        await EnsureMembershipAsync(dbContext, staffUser.Id, business.Id, BusinessUserRole.Staff, now);

        var staffMember = await EnsureStaffMemberAsync(dbContext, business.Id, options, now);
        var resource = await EnsureResourceAsync(dbContext, business.Id, options, now);
        var service = await EnsureServiceAsync(dbContext, business.Id, options, now);

        await dbContext.SaveChangesAsync();

        await EnsureServiceResourceRequirementAsync(
            dbContext,
            service.Id,
            resource.Id,
            options.ServiceRequiresResource,
            now);

        await EnsureBusinessWorkingHoursAsync(dbContext, business.Id);
        await EnsureStaffWorkingHoursAsync(dbContext, staffMember.Id);

        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Development seed završen. Owner={OwnerEmail}, Manager={ManagerEmail}, Staff={StaffEmail}, Password={Password}",
            options.OwnerEmail,
            options.ManagerEmail,
            options.StaffUserEmail,
            options.DefaultPassword);
    }

    private static async Task<AppUser> EnsureUserAsync(
        BookingDbContext dbContext,
        string email,
        string fullName,
        string password,
        DateTime now)
    {
        var normalizedEmail = email.Trim().ToUpperInvariant();

        var user = await dbContext.AppUsers
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

        if (user is null)
        {
            user = new AppUser
            {
                Email = email.Trim(),
                NormalizedEmail = normalizedEmail,
                FullName = fullName.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.AppUsers.Add(user);
            return user;
        }

        user.Email = email.Trim();
        user.FullName = fullName.Trim();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.IsActive = true;
        user.UpdatedAtUtc = now;

        return user;
    }

    private static async Task<Business> EnsureBusinessAsync(
        BookingDbContext dbContext,
        DevelopmentSeedOptions options,
        DateTime now)
    {
        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(x => x.Name == options.BusinessName);

        if (business is null)
        {
            business = new Business
            {
                Name = options.BusinessName.Trim(),
                BusinessType = Enum.GetValues<BusinessType>().First(),
                Description = options.BusinessDescription?.Trim(),
                SlotIntervalMin = options.SlotIntervalMin,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Businesses.Add(business);
            return business;
        }

        business.Description = options.BusinessDescription?.Trim();
        business.SlotIntervalMin = options.SlotIntervalMin;
        business.IsActive = true;
        business.UpdatedAtUtc = now;

        return business;
    }

    private static async Task EnsureMembershipAsync(
        BookingDbContext dbContext,
        long userId,
        long businessId,
        BusinessUserRole role,
        DateTime now)
    {
        var membership = await dbContext.BusinessUserMemberships
            .FirstOrDefaultAsync(x => x.AppUserId == userId && x.BusinessId == businessId);

        if (membership is null)
        {
            membership = new BusinessUserMembership
            {
                AppUserId = userId,
                BusinessId = businessId,
                Role = role,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.BusinessUserMemberships.Add(membership);
            return;
        }

        membership.Role = role;
        membership.IsActive = true;
        membership.UpdatedAtUtc = now;
    }

    private static async Task<StaffMember> EnsureStaffMemberAsync(
        BookingDbContext dbContext,
        long businessId,
        DevelopmentSeedOptions options,
        DateTime now)
    {
        var entity = await dbContext.StaffMembers
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.DisplayName == options.StaffDisplayName);

        if (entity is null)
        {
            entity = new StaffMember
            {
                BusinessId = businessId,
                DisplayName = options.StaffDisplayName.Trim(),
                Title = options.StaffTitle?.Trim(),
                IsBookable = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.StaffMembers.Add(entity);
            return entity;
        }

        entity.Title = options.StaffTitle?.Trim();
        entity.IsBookable = true;
        entity.IsActive = true;
        entity.UpdatedAtUtc = now;

        return entity;
    }

    private static async Task<Resource> EnsureResourceAsync(
        BookingDbContext dbContext,
        long businessId,
        DevelopmentSeedOptions options,
        DateTime now)
    {
        var entity = await dbContext.Resources
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.Name == options.ResourceName);

        if (entity is null)
        {
            entity = new Resource
            {
                BusinessId = businessId,
                Name = options.ResourceName.Trim(),
                ResourceType = Enum.GetValues<ResourceType>().First(),
                Capacity = options.ResourceCapacity,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Resources.Add(entity);
            return entity;
        }

        entity.Capacity = options.ResourceCapacity;
        entity.IsActive = true;
        entity.UpdatedAtUtc = now;

        return entity;
    }

    private static async Task<Service> EnsureServiceAsync(
        BookingDbContext dbContext,
        long businessId,
        DevelopmentSeedOptions options,
        DateTime now)
    {
        var entity = await dbContext.Services
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.Name == options.ServiceName);

        if (entity is null)
        {
            entity = new Service
            {
                BusinessId = businessId,
                Name = options.ServiceName.Trim(),
                Description = options.ServiceDescription?.Trim(),
                BasePrice = options.ServiceBasePrice,
                EstimatedDurationMin = options.ServiceDurationMin,
                BookingStrategyType = Enum.GetValues<BookingStrategyType>().First(),
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.Services.Add(entity);
            return entity;
        }

        entity.Description = options.ServiceDescription?.Trim();
        entity.BasePrice = options.ServiceBasePrice;
        entity.EstimatedDurationMin = options.ServiceDurationMin;
        entity.IsActive = true;
        entity.UpdatedAtUtc = now;

        return entity;
    }

    private static async Task EnsureServiceResourceRequirementAsync(
        BookingDbContext dbContext,
        long serviceId,
        long resourceId,
        bool isRequired,
        DateTime now)
    {
        var entity = await dbContext.ServiceResourceRequirements
            .FirstOrDefaultAsync(x => x.ServiceId == serviceId && x.ResourceId == resourceId);

        if (entity is null)
        {
            entity = new ServiceResourceRequirement
            {
                ServiceId = serviceId,
                ResourceId = resourceId,
                IsRequired = isRequired,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            dbContext.ServiceResourceRequirements.Add(entity);
            return;
        }

        entity.IsRequired = isRequired;
        entity.UpdatedAtUtc = now;
    }

    private static async Task EnsureBusinessWorkingHoursAsync(
        BookingDbContext dbContext,
        long businessId)
    {
        await UpsertBusinessHourAsync(dbContext, businessId, 1, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertBusinessHourAsync(dbContext, businessId, 2, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertBusinessHourAsync(dbContext, businessId, 3, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertBusinessHourAsync(dbContext, businessId, 4, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertBusinessHourAsync(dbContext, businessId, 5, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertBusinessHourAsync(dbContext, businessId, 6, TimeSpan.FromHours(9), TimeSpan.FromHours(14), false);
        await UpsertBusinessHourAsync(dbContext, businessId, 7, TimeSpan.Zero, TimeSpan.Zero, true);
    }

    private static async Task EnsureStaffWorkingHoursAsync(
        BookingDbContext dbContext,
        long staffMemberId)
    {
        await UpsertStaffHourAsync(dbContext, staffMemberId, 1, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertStaffHourAsync(dbContext, staffMemberId, 2, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertStaffHourAsync(dbContext, staffMemberId, 3, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertStaffHourAsync(dbContext, staffMemberId, 4, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertStaffHourAsync(dbContext, staffMemberId, 5, TimeSpan.FromHours(9), TimeSpan.FromHours(17), false);
        await UpsertStaffHourAsync(dbContext, staffMemberId, 6, TimeSpan.FromHours(9), TimeSpan.FromHours(14), false);
        await UpsertStaffHourAsync(dbContext, staffMemberId, 7, TimeSpan.Zero, TimeSpan.Zero, true);
    }

    private static async Task UpsertBusinessHourAsync(
        BookingDbContext dbContext,
        long businessId,
        int dayOfWeek,
        TimeSpan start,
        TimeSpan end,
        bool isClosed)
    {
        var entity = await dbContext.BusinessWorkingHours
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.DayOfWeek == dayOfWeek);

        if (entity is null)
        {
            entity = new BusinessWorkingHour
            {
                BusinessId = businessId,
                DayOfWeek = dayOfWeek
            };

            dbContext.BusinessWorkingHours.Add(entity);
        }

        entity.StartTime = start;
        entity.EndTime = end;
        entity.IsClosed = isClosed;
    }

    private static async Task UpsertStaffHourAsync(
        BookingDbContext dbContext,
        long staffMemberId,
        int dayOfWeek,
        TimeSpan start,
        TimeSpan end,
        bool isClosed)
    {
        var entity = await dbContext.StaffWorkingHours
            .FirstOrDefaultAsync(x => x.StaffMemberId == staffMemberId && x.DayOfWeek == dayOfWeek);

        if (entity is null)
        {
            entity = new StaffWorkingHour
            {
                StaffMemberId = staffMemberId,
                DayOfWeek = dayOfWeek
            };

            dbContext.StaffWorkingHours.Add(entity);
        }

        entity.StartTime = start;
        entity.EndTime = end;
        entity.IsClosed = isClosed;
    }
}