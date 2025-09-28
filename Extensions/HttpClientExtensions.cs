using HeatHarmony.Config;

namespace HeatHarmony.Extensions
{
    public static class HttpClientExtensions
    {
        public static IHostApplicationBuilder AddHttpClients(this IHostApplicationBuilder builder)
        {
            builder.Services.AddHttpClient(HttpClientConst.PriceClient, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
            MaxConnectionsPerServer = 10
            });
            builder.Services.AddHttpClient(HttpClientConst.OumanClient, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = 10
            });
            builder.Services.AddHttpClient(HttpClientConst.HeishaClient, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = 10
            });
            builder.Services.AddHttpClient(HttpClientConst.ShellyClient, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = 10
            });

            return builder;
        }
    }
}
