namespace HeatHarmony.Models
{
    public sealed class MQStatusResponse
    {
        public MQStatusEnum MQStatus { get; set; }
        public DateTime ServerTime { get; set; }
    }
}