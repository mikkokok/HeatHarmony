namespace HeatHarmony.DTO
{
    public sealed class HeatAutomationStatusResponse
    {
        public bool IsWorkerRunning { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}