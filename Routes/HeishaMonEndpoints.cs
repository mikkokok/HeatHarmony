using HeatHarmony.Providers;
using Microsoft.AspNetCore.Http;
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
                return Results.Ok(new
                {
                    inletTemp = provider.MainInletTemp,
                    outletTemp = provider.MainOutletTemp,
                    targetTemp = provider.MainTargetTemp,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetLatestHeishaMonReadings")
            .Produces(StatusCodes.Status200OK);

            heisha.MapGet("/task", ([FromServices] HeishaMonProvider provider) =>
            {
                var status = provider.HeishaMonTask?.Status.ToString() ?? "NotStarted";
                var error = provider.HeishaMonTask?.Exception?.Message;
                return Results.Ok(new
                {
                    status,
                    errors = error is null ? [] : new[] { error },
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetHeishaMonProviderTask")
            .Produces(StatusCodes.Status200OK);

            heisha.MapGet("/status", ([FromServices] HeishaMonProvider provider) =>
            {
                return Results.Ok(new
                {
                    changes = provider.Changes,
                    serverTime = DateTime.Now
                });
            })
            .WithName("GetHeishaMonProviderStatus")
            .Produces(StatusCodes.Status200OK);

            heisha.MapPut("/target/{temperature}", async ([FromServices] HeishaMonProvider provider, int temperature) =>
            {
                if (temperature < 20 || temperature > 55)
                {
                    return Results.BadRequest(new { message = "temperature must be between 20 and 55 °C" });
                }
                await provider.SetTargetTemperature(temperature);
                return Results.Accepted(value: new { targetTemp = temperature, requestedAt = DateTime.Now });
            })
            .WithName("SetHeishaMonTargetTemperature")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status400BadRequest);
        }
    }
}
