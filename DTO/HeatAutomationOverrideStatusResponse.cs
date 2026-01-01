namespace HeatHarmony.DTO
{
    public sealed class HeatAutomationOverrideStatusResponse
    {
        public bool IsActive { get; set; }
        public double TargetTemp { get; set; }
        public DateTime? Until { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}