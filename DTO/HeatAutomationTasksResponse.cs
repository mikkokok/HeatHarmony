namespace HeatHarmony.DTO
{
    public sealed class HeatAutomationTasksResponse
    {
        public required HeatAutomationTaskDetails OumanAndHeishamonSync { get; set; }
        public required HeatAutomationTaskDetails SetUseWaterBasedOnPrice { get; set; }
        public required HeatAutomationTaskDetails SetInsideTempBasedOnPrice { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}