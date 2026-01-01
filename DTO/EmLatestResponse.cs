namespace HeatHarmony.DTO
{
    public sealed class EmLatestResponse
    {
        public DateTime LastEnabled { get; set; }
        public bool IsOverridden { get; set; }
        public bool IsRunning { get; set; }
        public bool IsOn { get; set; }
    }
}