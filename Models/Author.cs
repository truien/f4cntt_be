using System;
using System.Collections.Generic;

namespace BACKEND.Models;

public partial class Author
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
}
