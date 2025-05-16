public class SummarizeRequest
{
    public int? DocumentId { get; set; }  // nếu tóm tắt từ tài liệu
    public string? Text { get; set; }  // nếu tóm tắt trực tiếp từ chuỗi
}

public class SummarizeResponse
{
    public string Summary { get; set; } = null!;
}
