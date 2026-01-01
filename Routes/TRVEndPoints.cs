using HeatHarmony.DTO;
using HeatHarmony.Providers;
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

                var response = new TRVLatestResponse
                {
                    Devices = devices,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetLatestTRVReadings")
            .Produces<TRVLatestResponse>(StatusCodes.Status200OK);

            trv.MapGet("/task", ([FromServices] TRVProvider trvProvider) =>
            {
                var status = trvProvider.TRVTask?.Status.ToString() ?? "NotStarted";
                var error = trvProvider.TRVTask?.Exception?.Message;

                var response = new TRVTaskResponse
                {
                    Status = status,
                    Errors = error is null ? Array.Empty<string>() : new[] { error },
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetTRVProviderTask")
            .Produces<TRVTaskResponse>(StatusCodes.Status200OK);
        }
    }
}
