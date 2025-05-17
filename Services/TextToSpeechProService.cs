// Services/TextToSpeechProService.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BACKEND.Services
{
    public class TextToSpeechProService : ITtsService
    {
        private readonly HttpClient _http;
        private readonly ILogger<TextToSpeechProService> _logger;

        public TextToSpeechProService(HttpClient http, ILogger<TextToSpeechProService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<byte[]> SynthesizeAsync(string text)
        {
            // 1) Tạo form-url-encoded payload theo docs
            var form = new Dictionary<string, string>
            {
                ["text"] = text,
                ["ssml"] = "",
                ["voiceId"] = "413",
                ["effectsProfileId"] = "headphone-class-device",
                ["speakingRate"] = "1"
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "api/tts")
            {
                Content = new FormUrlEncodedContent(form)
            };

            // 2) Thêm header RapidAPI
            req.Headers.Add("x-rapidapi-key", "8ee5abcf3dmshf8e255bb4581e28p136c17jsna96fbb306754");
            req.Headers.Add("x-rapidapi-host", "text-to-speech-pro.p.rapidapi.com");

            _logger.LogInformation("TTS PRO POST /api/tts form: {@Form}", form);

            // 3) Gửi request
            using var resp = await _http.SendAsync(req);
            resp.EnsureSuccessStatusCode();

            // 4) Nhận về MP3 binary trực tiếp
            var contentType = resp.Content.Headers.ContentType?.MediaType;
            if (contentType == "audio/mpeg" || contentType == "audio/mp3")
            {
                return await resp.Content.ReadAsByteArrayAsync();
            }

            // 5) Nếu trả về JSON chứa audio_url
            var raw = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("TTS PRO raw JSON: {Raw}", raw);

            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (!root.GetProperty("success").GetBoolean())
                throw new InvalidOperationException("TTS PRO failed: " + raw);

            var audioUrl = root.GetProperty("audio_url").GetString()!;
            _logger.LogInformation("Downloading MP3 from {Url}", audioUrl);
            return await _http.GetByteArrayAsync(audioUrl);
        }
    }
}
