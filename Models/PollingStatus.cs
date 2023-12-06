namespace HeatHarmony.Models
{
    public class PollingStatus
    {
        public DateTime Time { get; set; }
        public required string StatusReason { get; set; }
        public required bool Status { get; set; }
        public required string Poller {  get; set; }
    }
}