namespace HeatHarmony.DTO
{
    public sealed class AppUptimeInfo
    {
        public long Ticks { get; set; }
        public double TotalSeconds { get; set; }
        public required string Duration { get; set; }
    }
}