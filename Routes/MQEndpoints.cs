using HeatHarmony.Models;
using HeatHarmony.MQ;
using Microsoft.AspNetCore.Mvc;

namespace HeatHarmony.Routes
{
    public static partial class ApiMapper
    {
        public static void MapMQEndpoints(this WebApplication app)
        {
            var mq = app.MapGroup("/mq")
                            .WithTags("MQEndpoints");
            mq.MapGet("/status", ([FromServices] MQClient client) =>
            {
                var response = new MQStatusResponse
                {
                    MQStatus = client.Status,
                    ServerTime = DateTime.Now
                };
                return TypedResults.Ok(response);
            })
            .WithName("GetMQStatus")
            .Produces<MQStatusResponse>(StatusCodes.Status200OK);

            mq.MapGet("/task", ([FromServices] MQClient mqClient) =>
            {
                return TypedResults.Ok(mqClient.Initialization?.Exception?.Message);
            })
            .WithName("GetMQClientTask");
        }
    }
}
