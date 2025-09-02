namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public sealed class TRVThermoResponse
    {
        public double pos { get; set; }
        public required TargetT target_t { get; set; }
        public required Tmp tmp { get; set; }
        public bool schedule { get; set; }
        public int schedule_profile { get; set; }
        public int boost_minutes { get; set; }
        public bool window_open { get; set; }
    }

    public sealed class TargetT
    {
        public bool enabled { get; set; }
        public double value { get; set; }
        public double value_op { get; set; }
        public required string units { get; set; }
    }

    public sealed class Tmp
    {
        public double value { get; set; }
        public required string units { get; set; }
        public bool is_valid { get; set; }
    }
}
