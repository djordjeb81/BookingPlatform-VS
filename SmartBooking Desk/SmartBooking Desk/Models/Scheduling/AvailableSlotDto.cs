using System;

namespace SmartBooking_Desk.Models.Scheduling
{
    public class AvailableSlotDto
    {
        public DateTime StartAtUtc { get; set; }
        public DateTime EndAtUtc { get; set; }
        public string? StartLabel { get; set; }
        public string? EndLabel { get; set; }

        public string DisplayLabel
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(StartLabel) && !string.IsNullOrWhiteSpace(EndLabel))
                    return $"{StartLabel} - {EndLabel}";

                var localStart = StartAtUtc.ToLocalTime();
                var localEnd = EndAtUtc.ToLocalTime();
                return $"{localStart:HH:mm} - {localEnd:HH:mm}";
            }
        }
    }
}