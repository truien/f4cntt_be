using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

public interface IHuggingFaceService
{
    Task<string> SummarizeAsync(string text);
}
public class HuggingFaceService : IHuggingFaceService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public HuggingFaceService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;

        _http.BaseAddress = new Uri("https://api-inference.huggingface.co/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["HuggingFace:ApiToken"]);
    }

    public async Task<string> SummarizeAsync(string text)
    {
        var payload = new { inputs = text };
        var resp = await _http.PostAsJsonAsync("models/facebook/bart-large-cnn", payload);
        var errorBody = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"HF API lỗi {resp.StatusCode}: {errorBody}");
        }
        resp.EnsureSuccessStatusCode();
        var arr = await resp.Content.ReadFromJsonAsync<List<Dictionary<string, string>>>();
        if (arr != null && arr.Count > 0 && arr[0].TryGetValue("summary_text", out var s))
            return s.Trim();

        throw new Exception("HF Inference trả về format không đúng.");
    }
}
