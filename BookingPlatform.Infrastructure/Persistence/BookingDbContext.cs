using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Domain.Licensing;
using BookingPlatform.Domain.Customers;
using BookingPlatform.Domain.Chat;
using BookingPlatform.Domain.Push;
using BookingPlatform.Domain.Restaurants;

namespace BookingPlatform.Infrastructure.Persistence;

public sealed class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<BusinessUserMembership> BusinessUserMemberships => Set<BusinessUserMembership>();
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<BusinessFeatureSettings> BusinessFeatureSettings => Set<BusinessFeatureSettings>();
    public DbSet<BusinessCustomer> BusinessCustomers => Set<BusinessCustomer>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<StaffResourceAssignment> StaffResourceAssignments => Set<StaffResourceAssignment>();
    public DbSet<StaffServiceAssignment> StaffServiceAssignments => Set<StaffServiceAssignment>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceResourceRequirement> ServiceResourceRequirements => Set<ServiceResourceRequirement>();
    public DbSet<ServiceResourceUsage> ServiceResourceUsages => Set<ServiceResourceUsage>();
    public DbSet<ServiceResourceUsageStaffMember> ServiceResourceUsageStaffMembers => Set<ServiceResourceUsageStaffMember>();
    public DbSet<ServiceStep> ServiceSteps => Set<ServiceStep>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentStaffUsage> AppointmentStaffUsages => Set<AppointmentStaffUsage>();
    public DbSet<ReservationHold> ReservationHolds => Set<ReservationHold>();
    public DbSet<BusinessWorkingHour> BusinessWorkingHours => Set<BusinessWorkingHour>();
    public DbSet<StaffWorkingHour> StaffWorkingHours => Set<StaffWorkingHour>();
    public DbSet<StaffScheduleRule> StaffScheduleRules => Set<StaffScheduleRule>();
    public DbSet<StaffScheduleOverride> StaffScheduleOverrides => Set<StaffScheduleOverride>();
    public DbSet<AppointmentChangeRequest> AppointmentChangeRequests => Set<AppointmentChangeRequest>();
    public DbSet<TimeOffBlock> TimeOffBlocks => Set<TimeOffBlock>();
    public DbSet<AppointmentAuditLog> AppointmentAuditLogs => Set<AppointmentAuditLog>();
    public DbSet<LicensedDevice> LicensedDevices => Set<LicensedDevice>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UserPushToken> UserPushTokens => Set<UserPushToken>();
    public DbSet<ResourceGroup> ResourceGroups => Set<ResourceGroup>();
    public DbSet<RestaurantArea> RestaurantAreas => Set<RestaurantArea>();
    public DbSet<RestaurantLayoutElement> RestaurantLayoutElements => Set<RestaurantLayoutElement>();
    public DbSet<RestaurantTableSession> RestaurantTableSessions => Set<RestaurantTableSession>();
    public DbSet<RestaurantMenuCategory> RestaurantMenuCategories => Set<RestaurantMenuCategory>();
    public DbSet<RestaurantMenuItem> RestaurantMenuItems => Set<RestaurantMenuItem>();
    public DbSet<RestaurantMenuItemOptionGroup> RestaurantMenuItemOptionGroups => Set<RestaurantMenuItemOptionGroup>();
    public DbSet<RestaurantMenuItemOption> RestaurantMenuItemOptions => Set<RestaurantMenuItemOption>();
    public DbSet<RestaurantOrder> RestaurantOrders => Set<RestaurantOrder>();
    public DbSet<RestaurantOrderMessage> RestaurantOrderMessages => Set<RestaurantOrderMessage>();
    public DbSet<RestaurantOrderMessageRecipient> RestaurantOrderMessageRecipients => Set<RestaurantOrderMessageRecipient>();
    public DbSet<RestaurantOrderItem> RestaurantOrderItems => Set<RestaurantOrderItem>();
    public DbSet<RestaurantOperationUnit> RestaurantOperationUnits => Set<RestaurantOperationUnit>();

    public DbSet<RestaurantOperationUnitWorkingHour> RestaurantOperationUnitWorkingHours => Set<RestaurantOperationUnitWorkingHour>();
    public DbSet<RestaurantOrderItemOption> RestaurantOrderItemOptions => Set<RestaurantOrderItemOption>();
    public DbSet<RestaurantPayment> RestaurantPayments => Set<RestaurantPayment>();
    public DbSet<RestaurantTableReservation> RestaurantTableReservations => Set<RestaurantTableReservation>();
    public DbSet<RestaurantAreaReservation> RestaurantAreaReservations => Set<RestaurantAreaReservation>();
    public DbSet<RestaurantOrderGuest> RestaurantOrderGuests => Set<RestaurantOrderGuest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);

        modelBuilder.Entity<ServiceStepResourceRequirement>(entity =>
        {
            entity.HasIndex(x => new { x.ServiceStepId, x.ResourceId })
                .IsUnique();
        });

        modelBuilder.Entity<UserPushToken>(entity =>
        {
            entity.ToTable("user_push_tokens");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Token)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(x => x.Platform)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(x => x.DeviceName)
                .HasMaxLength(200);

            entity.HasIndex(x => x.AppUserId);

            entity.HasIndex(x => x.Token)
                .IsUnique();
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.Property(x => x.ActionType)
                .HasMaxLength(100);

            entity.HasIndex(x => x.AppointmentId);

            entity.HasIndex(x => x.ChangeRequestId);
        });

        modelBuilder.Entity<AppointmentStaffUsage>(entity =>
        {
            entity.ToTable("appointment_staff_usages");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.AppointmentId);

            entity.HasIndex(x => x.StaffMemberId);

            entity.HasIndex(x => new { x.StaffMemberId, x.AppointmentId });

            entity.HasOne(x => x.Appointment)
                .WithMany(x => x.StaffUsages)
                .HasForeignKey(x => x.AppointmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.StaffMember)
                .WithMany()
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResourceGroup>(entity =>
        {
            entity.ToTable("resource_groups");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);

            entity.HasIndex(x => new { x.BusinessId, x.Name })
                .IsUnique();

            entity.HasOne(x => x.Business)
                .WithMany()
                .HasForeignKey(x => x.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServiceResourceUsageStaffMember>(entity =>
        {
            entity.ToTable("service_resource_usage_staff_members");

            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.ServiceResourceUsageId, x.StaffMemberId })
                .IsUnique();

            entity.HasIndex(x => x.StaffMemberId);

            entity.HasOne(x => x.ServiceResourceUsage)
                .WithMany(x => x.StaffMembers)
                .HasForeignKey(x => x.ServiceResourceUsageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.StaffMember)
                .WithMany()
                .HasForeignKey(x => x.StaffMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.HasOne(x => x.ResourceGroup)
                .WithMany(x => x.Resources)
                .HasForeignKey(x => x.ResourceGroupId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => x.ResourceGroupId);
        });

        modelBuilder.Entity<RestaurantOperationUnit>(entity =>
        {
            entity.ToTable("restaurant_operation_units");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(x => x.UnitType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.IsActive)
                .HasDefaultValue(true);

            entity.Property(x => x.DisplayOrder)
                .HasDefaultValue(0);

            entity.Property(x => x.ReceivesCustomerChat)
    .HasDefaultValue(false);

            entity.HasIndex(x => x.BusinessId);

            entity.HasIndex(x => new { x.BusinessId, x.UnitType });

            entity.HasIndex(x => new { x.BusinessId, x.Name })
                .IsUnique();

            entity.HasIndex(x => new { x.BusinessId, x.DisplayOrder });

            entity.HasOne(x => x.Business)
                .WithMany()
                .HasForeignKey(x => x.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RestaurantOperationUnitWorkingHour>(entity =>
        {
            entity.ToTable("restaurant_operation_unit_working_hours");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.DayOfWeek)
                .IsRequired();

            entity.Property(x => x.StartTime)
                .IsRequired();

            entity.Property(x => x.EndTime)
                .IsRequired();

            entity.Property(x => x.IsClosed)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.BusinessId);

            entity.HasIndex(x => x.OperationUnitId);

            entity.HasIndex(x => new { x.OperationUnitId, x.DayOfWeek })
                .IsUnique();

            entity.HasOne(x => x.Business)
                .WithMany()
                .HasForeignKey(x => x.BusinessId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.OperationUnit)
                .WithMany(x => x.WorkingHours)
                .HasForeignKey(x => x.OperationUnitId)
                .OnDelete(DeleteBehavior.Cascade);


        });

        modelBuilder.Entity<RestaurantOrder>(entity =>
        {
            entity.Property(x => x.OrderSource)
                .HasConversion<int>()
                .IsRequired();
        });

        modelBuilder.Entity<RestaurantOrderMessage>(entity =>
        {
            entity.ToTable("restaurant_order_messages");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.SenderType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.SenderOperationUnitId)
    .IsRequired(false);

            entity.Property(x => x.MessageType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Text)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(x => x.ActionKey)
                .HasMaxLength(100);

            entity.Property(x => x.IsActionRequired)
                .HasDefaultValue(false);

            entity.Property(x => x.IsActionCompleted)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.BusinessId);

            entity.HasIndex(x => x.OrderId);

            entity.HasIndex(x => new { x.OrderId, x.CreatedAtUtc });

            entity.HasIndex(x => new { x.BusinessId, x.IsActionRequired, x.IsActionCompleted });

            entity.HasIndex(x => x.SenderOperationUnitId);

            entity.HasIndex(x => new { x.BusinessId, x.SenderOperationUnitId, x.Id });

            entity.HasOne(x => x.Order)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);
        });

        modelBuilder.Entity<RestaurantOrderMessageRecipient>(entity =>
        {
            entity.ToTable("restaurant_order_message_recipients");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RecipientType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.RecipientOperationUnitId)
                .IsRequired(false);

            entity.Property(x => x.IsRead)
                .HasDefaultValue(false);

            entity.HasIndex(x => x.BusinessId);

            entity.HasIndex(x => x.MessageId);

            entity.HasIndex(x => x.RecipientOperationUnitId);

            entity.HasIndex(x => new
            {
                x.BusinessId,
                x.RecipientType,
                x.RecipientOperationUnitId,
                x.MessageId
            });

            entity.HasIndex(x => new
            {
                x.BusinessId,
                x.RecipientOperationUnitId,
                x.IsRead
            });

            entity.HasOne(x => x.Message)
                .WithMany(x => x.Recipients)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

    }


    }