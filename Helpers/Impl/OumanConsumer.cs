namespace HeatHarmony.Helpers.Impl
{
    public class OumanConsumer
    {        
        public int LatestReading { get; private set; }

        private const string UrlKey = "OumanUrl";
        private readonly HttpClient _httpClient;
        private readonly string? _pollingUrl;
        private readonly IConfiguration _configuration;

        public OumanConsumer(IConfiguration config)
        {
            _configuration = config;
            _httpClient = new HttpClient()
            {
                Timeout = new TimeSpan(0, 0, 30)
            };
            _pollingUrl = _configuration[key: UrlKey];
        }

        public async Task UpdateLatestReading()
        {
            string pollingUrl = _pollingUrl ?? string.Empty;
            pollingUrl += "request?S_275_85;";
            HttpResponseMessage response = await _httpClient.GetAsync(pollingUrl);
            response.EnsureSuccessStatusCode();
            string responseContent = await response.Content.ReadAsStringAsync();
            LatestReading = StripValue(responseContent);
        }

        private static int StripValue(string content)
        {
            var readingAsString = content.Split("=")[1].Replace(";", " ");
            var reading = double.Parse(readingAsString);
            return (int)reading;
        }
    }
}
