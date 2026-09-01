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

            var apiKey = GlobalConfig.ApiKey ?? throw new Exception("No ApiKey present in config");
            var trmnlApiKey = GlobalConfig.TrmnlApiKey ?? throw new Exception("No TrmnlApiKey present in config");
            var trmnlCheck = trmnlApiKey.Equals(extractedApiKey);

            if (!apiKey.Equals(extractedApiKey) && !trmnlCheck)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized client.");
                return;
            }

            if (trmnlCheck && context.Request.Method != "GET")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("TRMNL API Key is restricted to GET requests only.");
                return;
            }

            await _next(context);
        }
    }
}
