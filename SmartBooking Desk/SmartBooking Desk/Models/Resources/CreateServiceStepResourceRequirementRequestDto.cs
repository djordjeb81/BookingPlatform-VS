namespace SmartBooking_Desk.Models.Resources
{
    public class CreateServiceStepResourceRequirementRequestDto
    {
        public long ServiceStepId { get; set; }
        public long ResourceId { get; set; }
        public bool IsRequired { get; set; } = true;
        public int? SequenceOrder { get; set; }
        public int? UsageDurationMin { get; set; }
    }
}