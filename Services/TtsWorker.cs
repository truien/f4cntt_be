using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BACKEND.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;

public class TtsWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TtsWorker> _logger;
    private readonly string _ttsApiBase;
    private readonly string _appBase;

    public TtsWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        ILogger<TtsWorker> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _httpFactory = httpFactory;
        _logger = logger;
        _ttsApiBase = config["TtsApi:BaseUrl"]
            ?? throw new ArgumentNullException("TtsApi:BaseUrl configuration is missing");
        _appBase = config["App:BaseUrl"]
            ?? throw new ArgumentNullException("App:BaseUrl configuration is missing");

    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Đảm bảo thư mục lưu audio luôn tồn tại
        var ttsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "tts");
        Directory.CreateDirectory(ttsFolder);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<DBContext>();
            var client = _httpFactory.CreateClient();

            //
            // 1) XỬ LÝ FULL-PDF → TTS
            //
            var docsToTts = await ctx.Documents
                .Where(d => d.TtsStatus == "Pending")
                .ToListAsync(stoppingToken);

            foreach (var doc in docsToTts)
            {
                try
                {
                    doc.TtsStatus = "Working";
                    await ctx.SaveChangesAsync(stoppingToken);

                    // Download PDF
                    var pdfUrl = $"{"http://localhost:5001/"}{doc.PdfUrl}";
                    var pdfBytes = await client.GetByteArrayAsync(pdfUrl, stoppingToken);

                    // Gửi lên API TTS
                    using var form = new MultipartFormDataContent();
                    var bin = new ByteArrayContent(pdfBytes);
                    bin.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
#pragma warning disable CS8604 // Possible null reference argument.
                    form.Add(bin, "file", Path.GetFileName(doc.PdfUrl));
#pragma warning restore CS8604 // Possible null reference argument.

                    var apiUrl = $"{_ttsApiBase}/api/tts/pdf?speed=175&pitch=50&volume=100";
                    var resp = await client.PostAsync(apiUrl, form, stoppingToken);
                    resp.EnsureSuccessStatusCode();

                    var audio = await resp.Content.ReadAsByteArrayAsync(stoppingToken);
                    var fn = $"full_{doc.Id}_{Guid.NewGuid()}.mp3";
                    var path = Path.Combine(ttsFolder, fn);
                    await File.WriteAllBytesAsync(path, audio, stoppingToken);

                    // Cập nhật DB
                    doc.TtsUrl = $"/uploads/tts/{fn}";
                    doc.TtsStatus = "Success";
                    await ctx.SaveChangesAsync(stoppingToken);
                }
                catch
                {
                    doc.TtsStatus = "Error";
                    await ctx.SaveChangesAsync(stoppingToken);
                }
            }

            //
            // 2) XỬ LÝ SUMMARY → TTS
            //
            var docsToSum = await ctx.Documents
                .Where(d => d.SummaryTtsStatus == "Pending")
                .Include(d => d.DocumentSummaries)
                .ToListAsync(stoppingToken);

            foreach (var doc in docsToSum)
            {
                try
                {
                    doc.SummaryTtsStatus = "Working";
                    await ctx.SaveChangesAsync(stoppingToken);

                    // Lấy text summary mới nhất
                    var summaryText = doc.DocumentSummaries
                                         .OrderByDescending(s => s.CreatedAt)
                                         .First().SummaryText;

                    // Gọi API TTS cho text
                    var payload = new { text = summaryText };
                    var json = new StringContent(
                        JsonSerializer.Serialize(payload),
                        Encoding.UTF8,
                        "application/json");

                    var apiUrl = $"{_ttsApiBase}/api/tts?speed=175&pitch=50&volume=100";
                    var resp = await client.PostAsync(apiUrl, json, stoppingToken);
                    resp.EnsureSuccessStatusCode();

                    // Lưu file audio
                    var audio = await resp.Content.ReadAsByteArrayAsync(stoppingToken);
                    var fn = $"sum_{doc.Id}_{Guid.NewGuid()}.mp3";
                    var path = Path.Combine(ttsFolder, fn);
                    await File.WriteAllBytesAsync(path, audio, stoppingToken);

                    // Cập nhật DB
                    doc.SummaryTtsUrl = $"/uploads/tts/{fn}";
                    doc.SummaryTtsStatus = "Success";
                    await ctx.SaveChangesAsync(stoppingToken);
                }
                catch
                {
                    doc.SummaryTtsStatus = "Error";
                    await ctx.SaveChangesAsync(stoppingToken);
                }
            }

            // Chờ 30s rồi chạy lại
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }


}
