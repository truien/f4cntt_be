namespace BACKEND.Services
{
    public interface ITranslateService
    {
        /// <summary>
        /// Dịch văn bản tiếng Anh sang tiếng Việt.
        /// </summary>
        Task<string> ToVietnamese(string text);
    }
}
