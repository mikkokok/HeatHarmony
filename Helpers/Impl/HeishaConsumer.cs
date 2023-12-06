using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;

namespace HeatHarmony.Helpers.Impl
{
    public class HeishaConsumer
    {
        private const string UrlKey = "HeishaUrl";
        private readonly HttpClient _httpClient;
        private readonly string? _heishaUrl;
        private readonly IConfiguration _configuration;
        public HeishaConsumer(IConfiguration config)
        {
            _configuration = config;
            _heishaUrl = _configuration[key: UrlKey];
            _httpClient = new()
            {
                Timeout = new TimeSpan(0, 0, 20)
            };
        }
        internal async Task UpdateDemand(double newDemand)
        {
            var tempUrl = _heishaUrl;
            tempUrl += $"?SetZ1HeatRequestTemperature={newDemand}";
            var uriBuilder = new UriBuilder(tempUrl)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = 80
            };
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query["SetZ1HeatRequestTemperature"] = newDemand.ToString();
            uriBuilder.Query = query.ToString();
            using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
            request.Headers.Add("DNT", "1");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }
    }
}