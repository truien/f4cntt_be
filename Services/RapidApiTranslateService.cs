using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BACKEND.Services
{
    public class RapidApiTranslateService : ITranslateService
    {
        private readonly HttpClient _http;
        private readonly ILogger<RapidApiTranslateService> _logger;
        public RapidApiTranslateService(
            HttpClient http,
            ILogger<RapidApiTranslateService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<string> ToVietnamese(string text)
        {
            var url = $"/external-api/free-google-translator" +
                      $"?from=en&to=vi&query={Uri.EscapeDataString(text)}";

            _logger.LogInformation("Calling RapidAPI Translator POST {Url}", url);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { translate = text }),
                    Encoding.UTF8,
                    "application/json"
                )
            };

            var resp = await _http.SendAsync(request);
            resp.EnsureSuccessStatusCode();

            var raw = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("RapidAPI translate raw response: {Raw}", raw);

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            // 1) Thử lấy các key cũ (nếu API thay đổi)
            if (root.TryGetProperty("v", out var vArr) && vArr.GetArrayLength() > 0)
                return vArr[0].GetProperty("dest").GetString()!;

            if (root.TryGetProperty("data", out var dataArr) && dataArr.GetArrayLength() > 0)
                return dataArr[0].GetProperty("translatedText").GetString()!;

            // 2) Lấy chính xác trường "translation"
            if (root.TryGetProperty("translation", out var trans))
                return trans.GetString()!;

            throw new InvalidOperationException("Unexpected translate response format: " + raw);
        }

    }
}
