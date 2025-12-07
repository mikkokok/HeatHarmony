using HeatHarmony.Config;

namespace HeatHarmony.Routes.Middlewares
{
    public class ApiKeyMiddleware(RequestDelegate next)
    {
        private readonly RequestDelegate _next = next;
        private readonly List<string> byPassedPaths =
        [
            "/appstatus/ping",
            "/appstatus/uptime"
        ];

        public async Task InvokeAsync(HttpContext context)
        {
            if (byPassedPaths.Contains(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue(GlobalConst.ApiKeyHeaderName, out var extractedApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("API Key was not provided.");
                return;
            }

            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = GlobalConfig.ApiKey ?? throw new Exception("No ApiKey present in config");

            if (!apiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized client.");
                return;
            }

            await _next(context);
        }
    }
}
