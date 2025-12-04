using HeatHarmony.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapTRVEndPoints(this WebApplication app)
        {
            var trv = app.MapGroup("/trv")
                         .WithTags("TRVEndPoints");

            trv.MapGet("/latest", ([FromServices] TRVProvider trvProvider) =>
            {
                var devices = trvProvider.GetDevices();
                return Results.Ok(new
                {
                    devices,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetLatestTRVReadings")
            .Produces(StatusCodes.Status200OK);

            trv.MapGet("/task", ([FromServices] TRVProvider trvProvider) =>
            {
                var status = trvProvider.TRVTask?.Status.ToString() ?? "NotStarted";
                var error = trvProvider.TRVTask?.Exception?.Message;

                return Results.Ok(new
                {
                    status,
                    errors = error is null ? Array.Empty<string>() : new[] { error },
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetTRVProviderTask")
            .Produces(StatusCodes.Status200OK);
        }
    }
}
