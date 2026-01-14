namespace HeatHarmony.DTO
{
    public sealed class HeishaMonLatestResponse
    {
        public double InletTemp { get; set; }
        public double OutletTemp { get; set; }
        public int TargetTemp { get; set; }
        public int QuietMode { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}