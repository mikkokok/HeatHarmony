namespace HeatHarmony.DTO
{
#pragma warning disable
    public sealed class FalconResponse
    {
        public int id { get; set; }
        public int sensorId { get; set; }
        public double temperature { get; set; }
        public double humidity { get; set; }
        public double pressure { get; set; }
        public double valvePosition { get; set; }
        public string time { get; set; } = string.Empty;
        public double usedPower { get; set; }
        public double burnerUseTime { get; set; }
        public double powerYield { get; set; }
    }
}
