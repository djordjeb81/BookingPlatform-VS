namespace SmartBooking_Desk.Models.Resources
{
    public class UpdateServiceStepResourceRequirementRequestDto
    {
        public bool IsRequired { get; set; }
        public int? SequenceOrder { get; set; }
        public int? UsageDurationMin { get; set; }
    }
}