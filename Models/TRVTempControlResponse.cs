namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public class ExtT
    {
        public bool enabled { get; set; }
        public bool floor_heating { get; set; }
    }

    public class TRVTempControlResponse
    {
        public required TargetTControl target_t { get; set; }
        public bool schedule { get; set; }
        public int schedule_profile { get; set; }
        public required List<string> schedule_profile_names { get; set; }
        public required List<object> schedule_rules { get; set; }
        public double temperature_offset { get; set; }
        public required ExtT ext_t { get; set; }
        public required TAuto t_auto { get; set; }
        public int boost_minutes { get; set; }
        public double valve_min_percent { get; set; }
        public double valve_min_report { get; set; }
        public bool force_close { get; set; }
        public bool calibration_correction { get; set; }
        public bool extra_pressure { get; set; }
        public bool open_window_report { get; set; }
    }

    public class TargetTControl
    {
        public bool enabled { get; set; }
        public double value { get; set; }
        public double value_op { get; set; }
        public required string units { get; set; }
        public bool accelerated_heating { get; set; }
    }

    public class TAuto
    {
        public bool enabled { get; set; }
    }
}
