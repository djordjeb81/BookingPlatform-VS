namespace SmartBooking_Desk.Models.Resources
{
    public class CreateServiceResourceRequirementRequestDto
    {
        public long ServiceId { get; set; }
        public long ResourceId { get; set; }
        public bool IsRequired { get; set; } = true;
    }
}