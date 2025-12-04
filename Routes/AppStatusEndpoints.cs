namespace HeatHarmony.Routes
{
    public partial class ApiMapper
    {
        private static readonly DateTime StartupLocal = DateTime.Now;

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

            appStatus.MapGet("/uptime", () =>
            {
                var now = DateTime.Now;
                var uptime = now - StartupLocal;

                string isoDuration = $"P{uptime.Days}DT{uptime.Hours}H{uptime.Minutes}M{uptime.Seconds}S";

                var payload = new
                {
                    startupTime = StartupLocal,
                    serverTime = now,
                    uptime = new
                    {
                        ticks = uptime.Ticks,
                        totalSeconds = uptime.TotalSeconds,
                        iso8601 = isoDuration
                    }
                };

                return Results.Ok(payload);
            })
            .WithName("GetAppUptime")
            .Produces(StatusCodes.Status200OK);
        }
    }
}
