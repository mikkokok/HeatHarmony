using System.Text.Json.Serialization;

namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public sealed class EMStatusResponse
    {
        public required WifiSta wifi_sta { get; set; }
        public required Cloud cloud { get; set; }
        public required Mqtt mqtt { get; set; }
        public required string time { get; set; }
        public int unixtime { get; set; }
        public int serial { get; set; }
        public bool has_update { get; set; }
        public required string mac { get; set; }
        public int cfg_changed_cnt { get; set; }
        public required ActionsStats actions_stats { get; set; }
        public required List<EMRelayResponse> relays { get; set; }
        public required List<EMeter> emeters { get; set; }
        public double total_power { get; set; }
        public required EmeterN emeter_n { get; set; }
        public bool fs_mounted { get; set; }
        public int v_data { get; set; }
        public int ct_calst { get; set; }
        public required Update update { get; set; }
        public int ram_total { get; set; }
        public int ram_free { get; set; }
        public int fs_size { get; set; }
        public int fs_free { get; set; }
        public int uptime { get; set; }
    }

    public sealed class EMeter
    {
        public double power { get; set; }
        public double pf { get; set; }
        public double current { get; set; }
        public double voltage { get; set; }
        public bool is_valid { get; set; }
        public double total { get; set; }
        public double total_returned { get; set; }
    }

    public sealed class EmeterN
    {
        public double current { get; set; }
        public double ixsum { get; set; }
        public bool mismatch { get; set; }
        public bool is_valid { get; set; }
    }
}
