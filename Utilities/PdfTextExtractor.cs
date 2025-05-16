using System.Text;
using UglyToad.PdfPig;

namespace BACKEND.Utilities
{
    public static class PdfTextExtractor
    {
        /// <summary>
        /// Đọc toàn bộ text từ file PDF
        /// </summary>
        /// <param name="filePath">Đường dẫn đầy đủ đến file .pdf</param>
        /// <returns>Chuỗi text của tất cả trang</returns>
        public static string ExtractText(string filePath)
        {
            var sb = new StringBuilder();

            // Mở tài liệu
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    // Lấy text mỗi trang rồi append
                    sb.AppendLine(page.Text);
                }
            }

            return sb.ToString();
        }
    }
}
