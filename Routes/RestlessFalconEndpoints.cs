using HeatHarmony.Providers;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapRestlessFalconEndpoints(this WebApplication app)
        {
            var restlessFalcon = app.MapGroup("/restlessfalcon")
                                        .WithTags("RestlessFalconEndpoints");
            restlessFalcon.MapGet("/avgtemp", ([FromQuery] int days, [FromServices] RestlessFalconProvider provider) =>
            {
                var avgTemp = provider.GetAvgTemperature(days).Result;
                if (avgTemp.HasValue)
                {
                    return Results.Ok(new { AverageTemperature = avgTemp.Value });
                }
                else
                {
                    return Results.NotFound(new { Message = $"Average temperature data not available for the past {days} days." });
                }
            })
            .WithName("GetRestlessFalconAvgTemperature")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
