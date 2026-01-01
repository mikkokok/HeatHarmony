using HeatHarmony.DTO;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public partial class ApiMapper
    {
        public static void MapAppStatusEndpoints(this WebApplication app)
        {
            var appStatus = app.MapGroup("/appstatus")
                               .WithTags("AppStatusEndpoints");

            appStatus.MapGet("/ping", () =>
            {
                var response = new AppPingResponse
                {
                    Status = "pong",
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetAppHealthStatus")
            .Produces<AppPingResponse>(StatusCodes.Status200OK);

            appStatus.MapGet("/uptime", ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                var now = DateTime.Now;
                var uptime = now - provider.StartupLocal;

                var response = new AppUptimeResponse
                {
                    StartupTime = provider.StartupLocal,
                    ServerTime = now,
                    Uptime = new AppUptimeInfo
                    {
                        Ticks = uptime.Ticks,
                        TotalSeconds = uptime.TotalSeconds,
                        Duration =
                            $"Days: {uptime.Days} Hours: {uptime.Hours} Minutes: {uptime.Minutes} Seconds: {uptime.Seconds}"
                    }
                };

                return Results.Ok(response);
            })
            .WithName("GetAppUptime")
            .Produces<AppUptimeResponse>(StatusCodes.Status200OK);
        }
    }
}