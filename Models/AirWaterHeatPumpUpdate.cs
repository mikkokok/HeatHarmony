namespace HeatHarmony.Models
{
    public class AirWaterHeatPumpUpdate
    {
        public DateTime Time { get; set; }
        public required string Update { get; set; }
        public required string Status { get; set; }
    }
}
