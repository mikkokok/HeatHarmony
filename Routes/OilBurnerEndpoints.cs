using HeatHarmony.Models;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapOilBurnerEndPoints(this WebApplication app)
        {
            var oilBurner = app.MapGroup("/oilburner")
                             .WithTags("OilBurnerEndpoints");
            oilBurner.MapGet("/latest", ([FromServices] OilBurnerProvider oilBurnerProvider) =>
            {
                var isRunning = oilBurnerProvider.IsEnabled;
                return Results.Ok(new
                {
                    isRunning,
                });
            })
            .WithName("GetLatestOilBurner")
            .Produces(StatusCodes.Status200OK);
            oilBurner.MapGet("/changes", ([FromServices] OilBurnerProvider oilBurnerProvider) =>
            {
                return Results.Ok(oilBurnerProvider.Changes);
            })
            .WithName("GetOilBurnerChanges")
            .Produces<List<HarmonyChange>>(StatusCodes.Status200OK);

            oilBurner.MapPost("/enable", async ([FromServices] OilBurnerProvider oilBurnerProvider) =>
            {
                await oilBurnerProvider.SetOilBurnerStatus(true);
                return Results.Accepted();
            })
            .WithName("EnableOilBurner")
            .Produces(StatusCodes.Status202Accepted);
            oilBurner.MapPost("/disable", async ([FromServices] OilBurnerProvider oilBurnerProvider) =>
            {
                await oilBurnerProvider.SetOilBurnerStatus(false);
                return Results.Accepted();
            })
            .WithName("DisableOilBurner")
            .Produces(StatusCodes.Status202Accepted);
        }
    }
}
