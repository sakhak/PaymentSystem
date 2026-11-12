using System.Net.Http.Headers;
using System.Text.Json;

namespace PaymentSystem.Services
{
    public class BakongService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<BakongService> _logger;

        public BakongService(
            HttpClient httpClient,
            IConfiguration config,
            ILogger<BakongService> logger
            )
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(logger);

            _httpClient = httpClient;
            _config = config;
            _logger = logger;

            var baseUrl = _config["Bakong:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl) && _httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            }
        }

        public async Task<JsonElement> CheckPaymentAsync(string md5, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(md5))
                throw new ArgumentException("MD5 hash is required.", nameof(md5));

            var token = _config["Bakong:Token"];
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException(
                    "Bakong API token is not configured."
                    );

            if (_httpClient.BaseAddress is null)
            {
                throw new InvalidOperationException(
                    "Bakong base URL is not configured."
                    );
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/check_transaction_by_md5");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { md5 = md5.Trim() });

            using var response = await _httpClient.SendAsync(
                request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Bakong request failed with status {StatusCode}.",
                    (int)response.StatusCode);

                throw new BakongApiException(
                    response.StatusCode,
                    string.Empty);
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken : cancellationToken);
            return result;
        }
    }

    public sealed class BakongApiException : HttpRequestException
    {
        public BakongApiException(
            System.Net.HttpStatusCode statusCode,
            string responseExcerpt)
            : base(
                $"Bakong API returned HTTP {(int)statusCode} ({statusCode}).",
                inner: null,
                statusCode)
        {
            ResponseExcerpt = responseExcerpt;
        }

        public string ResponseExcerpt { get; }
    }
}
