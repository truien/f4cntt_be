public class PdfCoKeyManager
{
    private readonly List<string> _keys;
    private int _idx = 0;
    private readonly object _lock = new();

    public PdfCoKeyManager(IConfiguration config)
    {
        _keys = config.GetSection("PdfCo:ApiKeys")
                      .Get<List<string>>()
               ?? throw new Exception("Missing PdfCo:ApiKeys in config");
    }

    public string CurrentKey
    {
        get
        {
            lock (_lock)
            {
                return _keys[_idx];
            }
        }
    }

    public void Rotate()
    {
        lock (_lock)
        {
            _idx = (_idx + 1) % _keys.Count;
        }
    }
}
