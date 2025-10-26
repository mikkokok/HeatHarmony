using HeatHarmony.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapEmEndPoints(this WebApplication app)
        {
            var emEndpoints = app.MapGroup("/em").WithTags("EmEndpoints");
            emEndpoints.MapGet("/latest", ([FromServices] EMProvider eMProvider) =>
            {
                return Results.Ok(new { eMProvider.LastEnabled, eMProvider.IsOverridden, isRunning = eMProvider.IsRunning() });
            }).WithName("GetLatestEM");
            emEndpoints.MapGet("/status", ([FromServices] EMProvider eMProvider) =>
            {
                return Results.Ok(eMProvider.Changes);
            }).WithName("GetEMStatus");

            emEndpoints.MapPut("/enable", async ([FromServices] EMProvider eMProvider) =>
                        {
                            await eMProvider.EnableWaterHeating();
                            return Results.Ok();
                        }).WithName("EnableEMWaterHeating");
            emEndpoints.MapPut("/disable", async ([FromServices] EMProvider eMProvider, [FromQuery(Name = "override")] bool overRide) =>
            {
                if (eMProvider.IsOverridden && !overRide)
                {
                    return Results.BadRequest("Water heating is currently overridden. To disable, set the override parameter to true.");
                }
                await eMProvider.DisableWaterHeating(overRide);
                return Results.Ok();
            }).WithName("DisableEMWaterHeating");
        }
    }
}
