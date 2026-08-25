using System.Text;

namespace RPC.Content;

public class ContentPackReader
{
    private readonly Dictionary<string, ReadOnlyMemory<byte>> _entries = new();

    public bool IsLoaded => _entries.Count > 0;

    public void Load(string rpkPath)
    {
        _entries.Clear();
        var data = File.ReadAllBytes(rpkPath);
        Read(data);
    }

    public void Read(ReadOnlyMemory<byte> data)
    {
        _entries.Clear();
        var span = data.Span;

        if (span.Length < 12) throw new InvalidDataException("RPK file too small");

        if (span[0] != (byte)'R' || span[1] != (byte)'P' || span[2] != (byte)'K' || span[3] != (byte)'1')
            throw new InvalidDataException("Invalid RPK magic");

        var version = BitConverter.ToUInt32(span.Slice(4, 4));
        if (version != 1) throw new InvalidDataException($"Unsupported RPK version: {version}");

        var count = BitConverter.ToUInt32(span.Slice(8, 4));
        var offset = 12;

        for (int i = 0; i < count; i++)
        {
            var dataOffset = BitConverter.ToUInt32(span.Slice(offset, 4));
            var dataLength = BitConverter.ToUInt32(span.Slice(offset + 4, 4));
            var pathLen = BitConverter.ToUInt16(span.Slice(offset + 8, 2));
            offset += 10;

            var path = Encoding.UTF8.GetString(span.Slice(offset, pathLen));
            offset += pathLen;

            // Verify dataOffset matches current position
            if (dataOffset != offset)
                throw new InvalidDataException($"RPK offset mismatch for {path}");

            _entries[path] = data.Slice(offset, (int)dataLength);
            offset += (int)dataLength;
        }
    }

    public bool Contains(string path) => _entries.ContainsKey(path);

    public string? GetString(string path)
    {
        if (!_entries.TryGetValue(path, out var bytes)) return null;
        return Encoding.UTF8.GetString(bytes.Span);
    }

    public ReadOnlyMemory<byte>? GetBytes(string path)
    {
        if (!_entries.TryGetValue(path, out var bytes)) return null;
        return bytes;
    }
}
