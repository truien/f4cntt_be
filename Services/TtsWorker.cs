// Workers/TtsWorker.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BACKEND.Models;
using BACKEND.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NAudio.Lame;
using NAudio.Wave;
using UglyToad.PdfPig;

namespace BACKEND.Workers
{
    public class TtsWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ITtsService _tts;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<TtsWorker> _logger;

        public TtsWorker(
            IServiceScopeFactory scopeFactory,
            ITtsService tts,
            IWebHostEnvironment env,
            ILogger<TtsWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _tts = tts;
            _env = env;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<DBContext>();

                // 1) TTS Summary
                var sumPendings = await ctx.Documents
                    .Where(d => d.Summarystatus == "Success" && d.SummaryTtsStatus == "Pending")
                    .ToListAsync(stoppingToken);
                foreach (var d in sumPendings)
                    await ProcessTts(d.Id, true, ctx, stoppingToken);

                // 2) TTS Full text
                var fullPendings = await ctx.Documents
                    .Where(d => d.Summarystatus == "Success" && d.TtsStatus == "Pending")
                    .ToListAsync(stoppingToken);
                foreach (var d in fullPendings)
                    await ProcessTts(d.Id, false, ctx, stoppingToken);

                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        private async Task ProcessTts(
            int documentId,
            bool isSummary,
            DBContext ctx,
            CancellationToken ct)
        {
            var doc = await ctx.Documents.FindAsync(new object[] { documentId }, ct);
            if (doc == null) return;

            // 1) Lấy nội dung
            string text;
            if (isSummary)
            {
                text = await ctx.DocumentSummaries
                    .Where(s => s.DocumentId == documentId)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => s.SummaryText)
                    .FirstOrDefaultAsync(ct) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("No summary for doc {Id}, skip.", documentId);
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(doc.PdfUrl))
                {
                    _logger.LogWarning("No PDF URL for doc {Id}, skip full TTS.", documentId);
                    return;
                }
                var fullPath = Path.Combine(_env.WebRootPath,
                    doc.PdfUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("PDF not found at {Path} for doc {Id}.", fullPath, documentId);
                    return;
                }
                var sb = new StringBuilder();
                using var pdf = PdfDocument.Open(fullPath);
                foreach (var page in pdf.GetPages())
                    sb.AppendLine(page.Text);
                text = sb.ToString();
            }

            try
            {
                // 2) Đánh dấu đang xử lý
                if (isSummary) doc.SummaryTtsStatus = "Working";
                else doc.TtsStatus = "Working";
                ctx.Update(doc);
                await ctx.SaveChangesAsync(ct);

                // 3) Gọi TTS & ghép MP3
                var audio = await SynthesizeAndMergeAsync(text);

                // 4) Lưu file MP3 & cập nhật đường dẫn
                var subdir = isSummary ? "tts-summaries" : "tts-full";
                var dir = Path.Combine(_env.WebRootPath, "uploads", subdir);
                Directory.CreateDirectory(dir);
                var filename = $"{Guid.NewGuid()}.mp3";
                var filepath = Path.Combine(dir, filename);
                await File.WriteAllBytesAsync(filepath, audio, ct);

                if (isSummary) doc.SummaryTtsUrl = $"/uploads/{subdir}/{filename}";
                else doc.TtsUrl = $"/uploads/{subdir}/{filename}";

                if (isSummary) doc.SummaryTtsStatus = "Success";
                else doc.TtsStatus = "Success";
                ctx.Update(doc);
                await ctx.SaveChangesAsync(ct);

                _logger.LogInformation("TTS completed for doc {Id} (summary={IsSum}).",
                    documentId, isSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error TTS for doc {Id}.", documentId);
                if (isSummary) doc.SummaryTtsStatus = "Error";
                else doc.TtsStatus = "Error";
                ctx.Update(doc);
                await ctx.SaveChangesAsync(ct);
            }
        }

        private List<string> SplitIntoChunks(string text, int maxSize = 400)
        {
            var sentences = Regex.Split(text, @"(?<=[\.\?\!])\s+")
                                    .Where(s => !string.IsNullOrWhiteSpace(s))
                                    .ToList();
            var chunks = new List<string>();
            var sb = new StringBuilder();
            foreach (var s in sentences)
            {
                if (sb.Length + s.Length + 1 <= maxSize)
                    sb.Append(s).Append(' ');
                else
                {
                    chunks.Add(sb.ToString().Trim());
                    sb.Clear();
                    sb.Append(s).Append(' ');
                }
            }
            if (sb.Length > 0) chunks.Add(sb.ToString().Trim());
            return chunks;
        }

        // Helper 2: synth từng chunk rồi merge bằng LameMP3FileWriter
        private async Task<byte[]> SynthesizeAndMergeAsync(string text)
        {
            var chunks = SplitIntoChunks(text, maxSize: 300);
            var buffers = new List<byte[]>();
            foreach (var chunk in chunks)
            {
                buffers.Add(await _tts.SynthesizeAsync(chunk));
                await Task.Delay(200); // tránh rate-limit
            }

            using var output = new MemoryStream();
            LameMP3FileWriter? writer = null;

            foreach (var buf in buffers)
            {
                using var ms = new MemoryStream(buf);
                using var reader = new Mp3FileReader(ms);

                if (writer == null)
                {
                    // decode MP3 -> PCM rồi re-encode
                    writer = new LameMP3FileWriter(output, reader.WaveFormat, LAMEPreset.STANDARD);
                }

                using var pcm = WaveFormatConversionStream.CreatePcmStream(reader);
                var readBuf = new byte[pcm.WaveFormat.AverageBytesPerSecond];
                int read;
                while ((read = pcm.Read(readBuf, 0, readBuf.Length)) > 0)
                {
                    writer.Write(readBuf, 0, read);
                }
            }

            writer?.Flush();
            writer?.Dispose();

            return output.ToArray();
        }
    }
}
