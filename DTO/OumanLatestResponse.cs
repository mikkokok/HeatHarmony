namespace HeatHarmony.DTO
{
    public sealed class OumanLatestResponse
    {
        public double OutsideTemp { get; set; }
        public double FlowDemand { get; set; }
        public double InsideTempDemand { get; set; }
        public double MinFlowTemp { get; set; }
        public bool AutoTemp { get; set; }
        public double InsideTemp { get; set; }
        public required DateTime ServerTime { get; set; }
    }
}