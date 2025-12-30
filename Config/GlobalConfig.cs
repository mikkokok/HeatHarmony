using HeatHarmony.Models;

namespace HeatHarmony.Config
{
    public static class GlobalConfig
    {
        public static string? ApiKey { get; set; }
        public static string? HeishaUrl { get; set; }
        public static string? PricesUrl { get; set; }
        public static string? Shelly3EMUrl { get; set; }
        public static string? OilBurnerShellyUrl { get; set; }
        public static ApiDocument? ApiDocumentConfig { get; set; }
        public static List<ShellyTRV>? ShellyTRVConfig { get; set; }
        public static Ouman? OumanConfig { get; set; }

        public class ApiDocument
        {
            public required string Title { get; set; }
            public required string Version { get; set; }
        }

        public class ShellyTRV
        {
            public required string Name { get; set; }
            public required string IP { get; set; }
            public required DateTime UpdatedAt { get; set; }
            public required TRVStatusEnum Status { get; set; }

            public string? Message { get; set; }
            public required int BatteryLevel { get; set; }
            public required double LatestLevel { get; set; }
            public required bool AutoTemperature { get; set; }
        }
        public class Ouman
        {
            public required string Url { get; set; }
            public required string Username { get; set; }
            public required string Password { get; set; }
        }

        public class HeatAutomationConfig
        {
            public const int MaxInitializationMinutes = 30;
            public const int HeatAddition = 3;
            public const decimal CheapPriceThreshold = 0.05m;
            public const decimal ModeratePriceThreshold = 0.10m;
            public const decimal ExpensivePriceThreshold = 0.20m;
            public const int EmergencyHeatingHours = 48;
        }
    }
}
