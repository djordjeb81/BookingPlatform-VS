using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Services;
using BookingPlatform.Domain.Staff;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Domain.Scheduling;

namespace BookingPlatform.Infrastructure.Persistence;

public sealed class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceStep> ServiceSteps => Set<ServiceStep>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ReservationHold> ReservationHolds => Set<ReservationHold>();
    public DbSet<BusinessWorkingHour> BusinessWorkingHours => Set<BusinessWorkingHour>();
    public DbSet<StaffWorkingHour> StaffWorkingHours => Set<StaffWorkingHour>();
    public DbSet<AppointmentChangeRequest> AppointmentChangeRequests => Set<AppointmentChangeRequest>();
    public DbSet<TimeOffBlock> TimeOffBlocks => Set<TimeOffBlock>();
    public DbSet<AppointmentAuditLog> AppointmentAuditLogs => Set<AppointmentAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}