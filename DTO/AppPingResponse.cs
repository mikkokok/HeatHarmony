namespace HeatHarmony.DTO
{
    public sealed class AppPingResponse
    {
        public required string Status { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}