// Services/LibreTranslateService.cs
using System.Net.Http.Json;
using System.Text.Json;

public class LibreTranslateService : ITranslateService
{
    private readonly HttpClient _http;
    public LibreTranslateService(HttpClient http) => _http = http;

    public async Task<string> ToVietnamese(string text)
    {
        // Gọi endpoint public, không cần key
        var resp = await _http.PostAsJsonAsync("https://libretranslate.de/translate", new
        {
            q = text,
            source = "en",
            target = "vi",
            format = "text"
        });
        resp.EnsureSuccessStatusCode();

        using var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        // Trường translatedText chứa kết quả
        return doc!.RootElement
                  .GetProperty("translatedText")
                  .GetString()!;
    }
}
