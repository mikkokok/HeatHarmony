using HeatHarmony.DTO;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapHeishaMonEndpoints(this WebApplication app)
        {
            var heisha = app.MapGroup("/heishamon")
                            .WithTags("HeishaMonEndpoints");

            heisha.MapGet("/latest", ([FromServices] HeishaMonProvider provider) =>
            {
                var response = new HeishaMonLatestResponse
                {
                    InletTemp = provider.MainInletTemp,
                    OutletTemp = provider.MainOutletTemp,
                    TargetTemp = provider.MainTargetTemp,
                    QuietMode = provider.QuietMode,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetLatestHeishaMonReadings")
            .Produces<HeishaMonLatestResponse>(StatusCodes.Status200OK);

            heisha.MapGet("/task", ([FromServices] HeishaMonProvider provider) =>
            {
                var status = provider.HeishaMonTask?.Status.ToString() ?? "NotStarted";
                var error = provider.HeishaMonTask?.Exception?.Message;

                var response = new HeishaMonTaskResponse
                {
                    Status = status,
                    Errors = error is null ? Array.Empty<string>() : new[] { error },
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetHeishaMonProviderTask")
            .Produces<HeishaMonTaskResponse>(StatusCodes.Status200OK);

            heisha.MapGet("/status", ([FromServices] HeishaMonProvider provider) =>
            {
                var response = new HeishaMonStatusResponse
                {
                    Changes = provider.Changes,
                    ServerTime = DateTime.Now
                };

                return Results.Ok(response);
            })
            .WithName("GetHeishaMonProviderStatus")
            .Produces<HeishaMonStatusResponse>(StatusCodes.Status200OK);
        }
    }
}
