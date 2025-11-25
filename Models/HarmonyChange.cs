namespace HeatHarmony.Models
{
    public class HarmonyChange
    {
        public DateTime Time { get; set; }
        public Provider Provider { get; set; }
        public HarmonyChangeType ChangeType { get; set; }
        public string? Description { get; set; }
    }

    public enum HarmonyChangeType
    {
        InsideTemp,
        DisableWaterHeating,
        EnableWaterHeating,
        SetMinFlowTemp,
        SetInsideTemp,
        SetMaximumFlow,
        SetAutoDriveOn,
        SetDefault,
        SetConservativeHeating,
        OverrideEnable,
        SetTargetTemp
    }
}
