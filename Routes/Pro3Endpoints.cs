using HeatHarmony.Models;
using HeatHarmony.DTO;
using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapPro3Endpoints(this WebApplication app)
        {
            var pro3 = app.MapGroup("/pro3").WithTags("Pro3Endpoints");

            pro3.MapGet("/status", ([FromServices] Pro3Provider provider) =>
            {
                return Results.Ok(provider.GetDeviceStatus());
            })
            .WithName("GetPro3Status")
            .Produces<List<Pro3SetResponse>>(StatusCodes.Status200OK);

            pro3.MapPost("/override", ([FromServices] Pro3Provider provider, [FromQuery] int outputAmount, [FromQuery] bool output, [FromQuery] int durationMinutes) =>
            {
                if (outputAmount < 1 || outputAmount > 3)
                {
                    return Results.BadRequest("Output amount must be between 1 and 3.");
                }
                if (durationMinutes < 1 || durationMinutes > 1440)
                {
                    return Results.BadRequest("Duration must be between 1 and 1440 minutes.");
                }
                provider.OverrideOutput(outputAmount, output, durationMinutes);
                return Results.Ok(new Pro3OverrideAcceptedResponse
                {
                    OutputAmount = outputAmount,
                    Output = output,
                    DurationMinutes = durationMinutes,
                    Until = DateTime.Now.AddMinutes(durationMinutes)
                });
            })
            .WithName("OverridePro3Output")
            .Produces<Pro3OverrideAcceptedResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

            pro3.MapPost("/override/cancel", ([FromServices] Pro3Provider provider) =>
            {
                if (!provider.IsOverridden)
                {
                    return Results.BadRequest("No override is currently active.");
                }
                provider.CancelOverride();
                return Results.Ok(new Pro3OverrideCancelledResponse { Message = "Override cancelled" });
            })
            .WithName("CancelPro3Override")
            .Produces<Pro3OverrideCancelledResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

            pro3.MapGet("/override/status", ([FromServices] Pro3Provider provider) =>
            {
                return Results.Ok(new Pro3OverrideStatusResponse
                {
                    IsOverridden = provider.IsOverridden,
                    Until = provider.OverrideUntil,
                    OutputAmount = provider.OverrideOutputAmount,
                    OutputState = provider.OverrideOutputState
                });
            })
            .WithName("GetPro3OverrideStatus")
            .Produces<Pro3OverrideStatusResponse>(StatusCodes.Status200OK);
        }
    }
}
