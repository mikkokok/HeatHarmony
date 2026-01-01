using HeatHarmony.DTO;
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
                var response = new OilBurnerLatestResponse
                {
                    IsRunning = oilBurnerProvider.IsEnabled,
                };

                return Results.Ok(response);
            })
            .WithName("GetLatestOilBurner")
            .Produces<OilBurnerLatestResponse>(StatusCodes.Status200OK);

            oilBurner.MapGet("/changes", ([FromServices] OilBurnerProvider oilBurnerProvider) =>
            {
                var response = new OilBurnerChangesResponse
                {
                    Changes = oilBurnerProvider.Changes
                };

                return Results.Ok(response);
            })
            .WithName("GetOilBurnerChanges")
            .Produces<OilBurnerChangesResponse>(StatusCodes.Status200OK);

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
