using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using System.IO;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore;
using BACKEND.Models;
using System.Net.Http.Headers;

public class PdfConversionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly PdfCoKeyManager _keyManager;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PdfConversionWorker> _logger;

    public PdfConversionWorker(IServiceScopeFactory scopeFactory, IHttpClientFactory httpFactory,
        PdfCoKeyManager keyManager, IWebHostEnvironment env, ILogger<PdfConversionWorker> logger)
    {
        _keyManager = keyManager;
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<DBContext>();
            var client = _httpFactory.CreateClient("PdfCo");
            client.DefaultRequestHeaders.Remove("x-api-key");
            client.DefaultRequestHeaders.Add("x-api-key", _keyManager.CurrentKey);

            // Lấy tất cả document mới (Pending)
            var pendings = await ctx.Documents
                .Where(d => d.ConversionStatus == "Pending")
                .ToListAsync(stoppingToken);

            foreach (var doc in pendings)
            {
                try
                {
                    // đánh dấu đang xử lý
                    doc.ConversionStatus = "Working";
                    ctx.Update(doc);
                    await ctx.SaveChangesAsync(stoppingToken);

                    // đường dẫn file gốc
                    var originalPath = Path.Combine(_env.WebRootPath,
                        "uploads", "originals",
                        Path.GetFileName(doc.FileUrl));

                    // 1) Upload & convert sync bằng helper
                    var pdfUrlRemote = await ConvertFileToPdfViaUploadAsync(
                        originalPath,
                        /*contentType*/ "application/octet-stream",
                        Path.GetFileName(doc.FileUrl));

                    // 2) Download & lưu PDF
                    var pdfDir = Path.Combine(_env.WebRootPath, "uploads", "pdfs");
                    Directory.CreateDirectory(pdfDir);
                    var pdfName = Path.GetFileName(new Uri(pdfUrlRemote).LocalPath);
                    var pdfLocal = Path.Combine(pdfDir, pdfName);
                    await DownloadFileAsync(pdfUrlRemote, pdfLocal);

                    // 3) Đếm trang + previewLimit
                    using var pdfDoc = PdfDocument.Open(pdfLocal);
                    doc.TotalPages = pdfDoc.NumberOfPages;
                    doc.PreviewPageLimit = Math.Max(1, doc.TotalPages.Value / 3);
                    doc.PdfUrl = $"/uploads/pdfs/{pdfName}";
                    doc.ConversionStatus = "Success";

                    ctx.Update(doc);
                    await ctx.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Convert lỗi document {Id}", doc.Id);
                    // có thể set doc.ConversionStatus = "Pending" lại hoặc "Failed"
                    doc.ConversionStatus = "Failed";
                    ctx.Update(doc);
                    await ctx.SaveChangesAsync(stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);
        }
    }
    private async Task<string> ConvertFileToPdfViaUploadAsync(
    string filePath, string contentType, string fileName)
    {
        // Tạo client và gán API key ở đây
        var client = _httpFactory.CreateClient("PdfCo");
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Add("x-api-key", _keyManager.CurrentKey);

        // 1) Upload file gốc để lấy URL
        using var uploadContent = new MultipartFormDataContent();
        await using var fs = File.OpenRead(filePath);
        var streamContent = new StreamContent(fs);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        uploadContent.Add(streamContent, "file", fileName);

        var uploadResp = await client.PostAsync("file/upload", uploadContent);
        uploadResp.EnsureSuccessStatusCode();
        var uploadJson = JsonDocument.Parse(
            await uploadResp.Content.ReadAsStringAsync()
        );
        var fileUrl = uploadJson.RootElement.GetProperty("url").GetString()!;

        // 2) Convert từ URL sang PDF
        var convertPayload = new
        {
            name = Path.GetFileNameWithoutExtension(fileName) + ".pdf",
            url = fileUrl,
            async = false
        };
        var convertResp = await client.PostAsJsonAsync(
            "pdf/convert/from/doc", convertPayload
        );
        convertResp.EnsureSuccessStatusCode();

        using var convertJson = JsonDocument.Parse(
            await convertResp.Content.ReadAsStringAsync()
        );
        var urlElem = convertJson.RootElement.GetProperty("url");

        // Lấy URL PDF trả về
        string pdfUrl = urlElem.ValueKind == JsonValueKind.Array
            ? urlElem[0].GetString()!
            : urlElem.GetString()!;

        return pdfUrl;
    }

    // Tải file từ URL về localPath
    private async Task DownloadFileAsync(string url, string localPath)
    {
        var client = _httpFactory.CreateClient("PdfCo");
        var bytes = await client.GetByteArrayAsync(url);
        await System.IO.File.WriteAllBytesAsync(localPath, bytes);
    }

}
