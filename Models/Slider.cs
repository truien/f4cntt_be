using System;
using System.Collections.Generic;

namespace BACKEND.Models;

/// <summary>
/// Bảng lưu thông tin các slide trên trang chủ
/// </summary>
public partial class Slider
{
    public int Id { get; set; }

    /// <summary>
    /// Đường dẫn đến ảnh
    /// </summary>
    public string ImageUrl { get; set; } = null!;

    /// <summary>
    /// Tiêu đề (nếu có)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Mô tả ngắn (nếu có)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// URL khi click vào slide
    /// </summary>
    public string? LinkUrl { get; set; }

    /// <summary>
    /// Thứ tự hiển thị
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 1=hiển thị, 0=ẩn
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Thời điểm tạo
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Thời điểm cập nhật
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
