using HeatHarmony.Config;

namespace HeatHarmony.Extensions
{
    public static class HttpClientExtensions
    {
        public static IHostApplicationBuilder AddHttpClients(this IHostApplicationBuilder builder)
        {
            var clientNames = new[]
            {
                HttpClientConst.PriceClient,
                HttpClientConst.OumanClient,
                HttpClientConst.HeishaClient,
                HttpClientConst.ShellyClient,
                HttpClientConst.Shelly3EMClient
            };

            foreach (var clientName in clientNames)
            {
                builder.AddStandardHttpClient(clientName);
            }

            return builder;
        }

        private static IHostApplicationBuilder AddStandardHttpClient(
            this IHostApplicationBuilder builder, 
            string clientName)
        {
            builder.Services.AddHttpClient(clientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                MaxConnectionsPerServer = 10
            });

            return builder;
        }
    }
}
