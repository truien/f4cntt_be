public interface ITranslateService
{
    /// <summary>
    /// Dịch văn bản từ tiếng Anh sang tiếng Việt.
    /// </summary>
    Task<string> ToVietnamese(string text);
}
