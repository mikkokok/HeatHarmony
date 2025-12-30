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
                return Results.Ok(new { status = "pong", serverTime = DateTime.Now });
            })
            .WithName("GetAppHealthStatus")
            .Produces(StatusCodes.Status200OK);

            appStatus.MapGet("/uptime",  ([FromServices] HeatAutomationWorkerProvider provider) =>
            {
                var now = DateTime.Now;
                var uptime = now - provider.StartupLocal;

                string duration = $"Days: {uptime.Days} Hours: {uptime.Hours} Minutes: {uptime.Minutes} Seconds: {uptime.Seconds}";

                var payload = new
                {
                    startupTime = provider.StartupLocal,
                    serverTime = now,
                    uptime = new
                    {
                        ticks = uptime.Ticks,
                        totalSeconds = uptime.TotalSeconds,
                        duration
                    }
                };

                return Results.Ok(payload);
            })
            .WithName("GetAppUptime")
            .Produces(StatusCodes.Status200OK);
        }
    }
}