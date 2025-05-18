using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class PointTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// Tham chiếu tới users.id
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// + là cộng, - là trừ
    /// </summary>
    public int ChangeAmount { get; set; }

    /// <summary>
    /// Lý do (download, approval, recharge...)
    /// </summary>
    public string Reason { get; set; } = null!;

    /// <summary>
    /// ID liên quan (document_id, transaction_id, v.v.)
    /// </summary>
    public int? RelatedId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
