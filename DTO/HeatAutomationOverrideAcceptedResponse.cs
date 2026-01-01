namespace HeatHarmony.DTO
{
    public sealed class HeatAutomationOverrideAcceptedResponse
    {
        public required string Message { get; set; }
        public double Temperature { get; set; }
        public int Hours { get; set; }
        public int DelayHours { get; set; }
        public required DateTime RequestedAt { get; set; }
    }
}