
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BACKEND.Services
{
    public class PdfAiService : IPdfAiService
    {
        private readonly HttpClient _http;
        private readonly ILogger<PdfAiService> _logger;

        private readonly ITranslateService _translator;

        public PdfAiService(
            HttpClient http,
            ILogger<PdfAiService> logger,
            ITranslateService translator)
        {
            _http = http;
            _logger = logger;
            _translator = translator;
        }

        public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName)
        {
            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(fileBytes);
            byteContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/pdf");
            content.Add(byteContent, "file", fileName);

            _logger.LogInformation("Uploading PDF to PDF.ai…");
            var resp = await _http.PostAsync("upload/file", content);
            resp.EnsureSuccessStatusCode();

            var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            var docId = doc!.RootElement.GetProperty("docId").GetString()!;
            _logger.LogInformation("Uploaded: docId={DocId}", docId);
            return docId;
        }

        public async Task<string> SummarizeAsync(string docId)
        {
            _logger.LogInformation("Requesting summary for docId={DocId}", docId);
            var resp = await _http.PostAsJsonAsync("summary", new { docId });
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
            var english = json!
                .RootElement
                .GetProperty("content")
                .GetString()!;
            _logger.LogInformation("Received English summary for docId={DocId}", docId);

            // 2) Dịch sang tiếng Việt bằng LibreTranslate
            var vietnamese = await _translator.ToVietnamese(english);
            _logger.LogInformation("Translated summary to Vietnamese for docId={DocId}", docId);

            return vietnamese;
        }
    }

}
