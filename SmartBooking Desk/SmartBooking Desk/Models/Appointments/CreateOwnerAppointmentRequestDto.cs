using System;

namespace SmartBooking_Desk.Models.Appointments
{
    public class CreateOwnerAppointmentRequestDto
    {
        public long BusinessId { get; set; }
        public long ServiceId { get; set; }
        public long? PrimaryStaffMemberId { get; set; }
        public long? ResourceId { get; set; }
        public long? BusinessCustomerId { get; set; }
        public DateTime StartAtUtc { get; set; }

        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Notes { get; set; }

        public bool IgnoreAvailabilityRules { get; set; }
        public bool IgnoreWorkingHours { get; set; }
        public bool IgnoreTimeOffBlocks { get; set; }
        public bool IgnoreAppointmentConflicts { get; set; }

        public int? FinalDurationMin { get; set; }
    }
}