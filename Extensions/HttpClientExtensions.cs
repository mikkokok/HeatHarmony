using HeatHarmony.Config;
using HeatHarmony.Helpers;

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
                HttpClientConst.Shelly3EMClient,
                HttpClientConst.ShellyPro3Client,
                HttpClientConst.OilBurnerShellyClient
            };

            foreach (var clientName in clientNames)
            {
                builder.AddStandardHttpClient(clientName);
            }

            builder.Services.AddHttpClient(HttpClientConst.RestlessFalconClient, (client) => { client.Timeout = TimeSpan.FromSeconds(30); })
                .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                return new HttpClientHandler
                {
#pragma warning disable
                    ServerCertificateCustomValidationCallback = CertificateValidator.ValidateCertificate
                };
            });

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
