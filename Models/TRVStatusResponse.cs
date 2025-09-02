namespace HeatHarmony.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public sealed class ActionsStats
    {
        public int skipped { get; set; }
    }

    public sealed class Bat
    {
        public int value { get; set; }
        public double voltage { get; set; }
    }

    public sealed class Cloud
    {
        public bool enabled { get; set; }
        public bool connected { get; set; }
    }

    public sealed class FwInfo
    {
        public required string device { get; set; }
        public required string fw { get; set; }
    }

    public sealed class Mqtt
    {
        public bool connected { get; set; }
    }

    public sealed class TRVStatusResponse
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
        public required List<Thermostat> thermostats { get; set; }
        public bool calibrated { get; set; }
        public required Bat bat { get; set; }
        public bool charger { get; set; }
        public required Update update { get; set; }
        public int ram_total { get; set; }
        public int ram_free { get; set; }
        public int fs_size { get; set; }
        public int fs_free { get; set; }
        public int uptime { get; set; }
        public required FwInfo fw_info { get; set; }
        public int ps_mode { get; set; }
        public int dbg_flags { get; set; }
    }
    public sealed class Thermostat
    {
        public double pos { get; set; }
        public required TargetT target_t { get; set; }
        public required Tmp tmp { get; set; }
        public bool schedule { get; set; }
        public int schedule_profile { get; set; }
        public int boost_minutes { get; set; }
        public bool window_open { get; set; }
    }

    public sealed class Update
    {
        public required string status { get; set; }
        public bool has_update { get; set; }
        public required string new_version { get; set; }
        public required string old_version { get; set; }
        public required string beta_version { get; set; }
    }

    public sealed class WifiSta
    {
        public bool connected { get; set; }
        public required string ssid { get; set; }
        public required string ip { get; set; }
        public int rssi { get; set; }
    }
}
