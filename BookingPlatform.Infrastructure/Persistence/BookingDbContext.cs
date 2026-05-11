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
    }
}