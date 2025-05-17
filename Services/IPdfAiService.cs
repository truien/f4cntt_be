namespace BACKEND.Services
{
    public interface IPdfAiService
    {
        /// <summary>
        /// Upload PDF file bytes, trả về docId của PDF.ai.
        /// </summary>
        Task<string> UploadFileAsync(byte[] fileBytes, string fileName);

        /// <summary>
        /// Gọi summary qua docId.
        /// </summary>
        Task<string> SummarizeAsync(string docId);
    }

}
