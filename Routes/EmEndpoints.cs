using HeatHarmony.Providers;
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
                return Results.Ok(new { eMProvider.LastEnabled, isRunning = eMProvider.IsRunning()});
            }).WithName("GetLatestEMStatus");
            emEndpoints.MapPut("/enable", async ([FromServices] EMProvider eMProvider) =>
            {
                await eMProvider.EnableWaterHeating();
                return Results.Ok();
            }).WithName("EnableEMWaterHeating");
            emEndpoints.MapPut("/disable", async ([FromServices] EMProvider eMProvider) =>
            {
                await eMProvider.DisableWaterHeating();
                return Results.Ok();
            }).WithName("DisableEMWaterHeating");
        }
    }
}
