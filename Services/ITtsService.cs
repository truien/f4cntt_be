// Services/ITtsService.cs
using System.Threading.Tasks;

namespace BACKEND.Services
{
    /// <summary>
    /// Interface chung cho mọi dịch vụ TTS.
    /// </summary>
    public interface ITtsService
    {
        /// <summary>
        /// Chuyển text thành byte[] MP3.
        /// </summary>
        Task<byte[]> SynthesizeAsync(string text);
    }
}
