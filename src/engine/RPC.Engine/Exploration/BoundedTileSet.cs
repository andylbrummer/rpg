namespace RPC.Engine;

public class BoundedTileSet
{
    private readonly HashSet<string> _set;
    private readonly Queue<string> _order;
    private readonly int _max;

    public BoundedTileSet(HashSet<string> set, Queue<string> order, int max)
    {
        _set = set;
        _order = order;
        _max = max;
    }

    private int _version;

    public int Count => _set.Count;

    /// <summary>
    /// Incremented on every change to the set. Count alone is not a sound staleness key for
    /// derived views: the eviction path removes one key as it adds another, leaving Count
    /// unchanged while the contents differ.
    /// </summary>
    public int Version => _version;

    public bool Add(string key)
    {
        if (_set.Contains(key)) return false;
        if (_set.Count >= _max)
        {
            var oldest = _order.Dequeue();
            _set.Remove(oldest);
        }
        _set.Add(key);
        _order.Enqueue(key);
        _version++;
        return true;
    }

    public void Clear()
    {
        _set.Clear();
        _order.Clear();
        _version++;
    }

    public bool Contains(string key) => _set.Contains(key);

    public IEnumerable<string> AsEnumerable() => _set;

    public IEnumerator<string> GetEnumerator() => _set.GetEnumerator();
}
