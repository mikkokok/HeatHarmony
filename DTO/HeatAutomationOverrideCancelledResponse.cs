namespace HeatHarmony.DTO
{
    public sealed class HeatAutomationOverrideCancelledResponse
    {
        public required string Message { get; set; }
        public required DateTime CancelledAt { get; set; }
    }
}