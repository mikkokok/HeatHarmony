using HeatHarmony.Config;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HeatHarmony.Providers
{
    public sealed class RequestProvider(ILogger<RequestProvider> logger, IHttpClientFactory httpClientFactory) : IRequestProvider
    {
        private readonly string _serviceName = nameof(RequestProvider);
        private readonly ILogger<RequestProvider> _logger = logger;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly Guid _operationId = Guid.NewGuid();

        public async Task PostAsync<TRequest>(string clientName, string url, TRequest data)
        {
            LogUrl(url, "post");
            var httpClient = _httpClientFactory.CreateClient(clientName);
            try
            {
                var requestContent = SerializeToJson(data);
                _logger.LogInformation($"{DateTime.Now} {_serviceName} {_operationId}:: serialization succeeded");
                using var response = await httpClient.PostAsync(url, requestContent);
                await HandleResponse(response);
                _logger.LogInformation($"{DateTime.Now} {_serviceName} {_operationId}:: sending data to {url} succeeded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{DateTime.Now} {_serviceName} {_operationId}:: Error sending request to {url}");
                throw;
            }
        }

        public async Task<TResult?> GetAsync<TResult>(string clientName, string url)
        {
            LogUrl(url, "get");
            var httpClient = _httpClientFactory.CreateClient(clientName);
            try
            {
                using var response = await httpClient.GetAsync(url);
                await HandleResponse(response);
                var result = await ReadFromJsonASync<TResult>(response.Content);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{DateTime.Now} {_serviceName} {_operationId}:: Error getting from {url}");
                throw;
            }
        }

        public async Task<string> GetStringAsync(string clientName, string url)
        {
            LogUrl(url, "get");
            var httpClient = _httpClientFactory.CreateClient(clientName);
            if (clientName == HttpClientConst.HeishaClient)
            {
                PatchHeishaMonClient(httpClient);
            }
            try
            {
                using var response = await httpClient.GetAsync(url);
                await HandleResponse(response);
                var result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"{DateTime.Now} {_serviceName} {_operationId}:: Error getting from {url}");
                throw;
            }
        }

        private static void PatchHeishaMonClient(HttpClient httpClient)
        {
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/html"));
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xhtml+xml"));
            httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            httpClient.DefaultRequestHeaders.Add("DNT", "1");
        }

        private static async Task HandleResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"{DateTime.Now} Request failed {response.StatusCode} with content {content}");
        }

        private static async Task<T?> ReadFromJsonASync<T>(HttpContent content)
        {
            using var contentStream = await content.ReadAsStreamAsync();
            var data = await JsonSerializer.DeserializeAsync<T>(contentStream);
            return data;
        }

        private static JsonContent SerializeToJson<T>(T data)
        {
            return JsonContent.Create(data);
        }

        private void LogUrl(string url, string method)
        {
            url = Regex.Replace(url, @"(pwd=)[^\n]*", "$1********");
            _logger.LogInformation($"{DateTime.Now} {_serviceName} {_operationId}:: URL: {url}, Method: {method}");
        }
    }
}
