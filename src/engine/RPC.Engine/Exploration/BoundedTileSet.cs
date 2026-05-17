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

    public int Count => _set.Count;

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
        return true;
    }

    public void Clear()
    {
        _set.Clear();
        _order.Clear();
    }

    public bool Contains(string key) => _set.Contains(key);

    public IEnumerable<string> AsEnumerable() => _set;

    public IEnumerator<string> GetEnumerator() => _set.GetEnumerator();
}
