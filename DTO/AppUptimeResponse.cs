namespace HeatHarmony.DTO
{
    public sealed class AppUptimeResponse
    {
        public DateTime StartupTime { get; set; }
        public DateTime ServerTime { get; set; }
        public required AppUptimeInfo Uptime { get; set; }
    }
}