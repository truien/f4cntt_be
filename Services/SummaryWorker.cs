// Workers/SummaryWorker.cs
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using BACKEND.Models;
using BACKEND.Services;

namespace BACKEND.Workers
{
    public class SummaryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IPdfAiService _pdfAi;
        private readonly ILogger<SummaryWorker> _logger;

        public SummaryWorker(
            IServiceScopeFactory scopeFactory,
            IPdfAiService pdfAi,
            ILogger<SummaryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _pdfAi = pdfAi;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<DBContext>();

                var pendings = await ctx.Documents
                    .Where(d => d.Summarystatus == "Pending" && d.PdfUrl != null)
                    .ToListAsync(stoppingToken);

                foreach (var doc in pendings)
                {
                    try
                    {
                        // 1. Mark Working
                        doc.Summarystatus = "Working";
                        ctx.Update(doc);
                        await ctx.SaveChangesAsync(stoppingToken);
                        // 2. Đọc file bytes
                        var wwwRoot = Path.GetFullPath("wwwroot");
#pragma warning disable CS8602 
                        var relPath = doc.PdfUrl.TrimStart('/')
                                            .Replace('/', Path.DirectorySeparatorChar);
#pragma warning restore CS8602 
                        var fullPath = Path.Combine(wwwRoot, relPath);
                        var fileBytes = await File.ReadAllBytesAsync(fullPath, stoppingToken);
                        var fileName = Path.GetFileName(fullPath);
                        // 3. Upload lên PDF.ai → lấy docId
                        var docId = await _pdfAi.UploadFileAsync(fileBytes, fileName);

                        doc.ConversionJobId = docId;  
                        ctx.Update(doc);
                        // 4. Gọi summary với docId
                        var summaryText = await _pdfAi.SummarizeAsync(docId);

                        // 5. Lưu DocumentSummary
                        ctx.DocumentSummaries.Add(new DocumentSummary
                        {
                            DocumentId = doc.Id,
                            SummaryText = summaryText,
                            CreatedAt = DateTime.UtcNow
                        });

                        // 6. Mark Success
                        doc.Summarystatus = "Success";
                        ctx.Update(doc);

                        await ctx.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Document {Id} summarized successfully.", doc.Id);
                    }
                    catch (Exception ex)
                    {
                        doc.Summarystatus = "Error";
                        ctx.Update(doc);
                        await ctx.SaveChangesAsync(stoppingToken);
                        _logger.LogError(ex, "Error summarizing document {Id}", doc.Id);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
            }
        }

    }
}
