namespace HeatHarmony.DTO
{
    public sealed class HeatAutomationTaskDetails
    {
        public required string Status { get; set; }
        public required IReadOnlyList<string> Errors { get; set; }
    }
}