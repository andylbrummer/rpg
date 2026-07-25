using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPC.Host.Web.Presenters;

/// <summary>
/// A already-serialized fragment of JSON, spliced verbatim into an enclosing document.
///
/// This exists so a presenter can memoise an expensive, rarely-changing part of the state
/// snapshot as bytes rather than as an object graph. Caching the graph alone is not worth much:
/// the rebuild is avoided, but the serializer then has to walk a long-lived, scattered heap every
/// frame, which costs about as much as rebuilding it did. Caching the encoded bytes skips both.
/// </summary>
[JsonConverter(typeof(RawJsonConverter))]
public readonly struct RawJson
{
    private static readonly byte[] EmptyArray = Encoding.UTF8.GetBytes("[]");

    private readonly byte[]? _utf8;

    private RawJson(byte[] utf8) => _utf8 = utf8;

    public static RawJson EmptyJsonArray => new(EmptyArray);

    /// <summary>Serializes <paramref name="value"/> once, to be spliced in later unchanged.</summary>
    public static RawJson Serialize<T>(T value, JsonSerializerOptions options) =>
        new(JsonSerializer.SerializeToUtf8Bytes(value, options));

    public ReadOnlySpan<byte> Utf8 => _utf8 ?? EmptyArray;

    public override string ToString() => Encoding.UTF8.GetString(Utf8);
}

internal sealed class RawJsonConverter : JsonConverter<RawJson>
{
    public override RawJson Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException($"{nameof(RawJson)} is write-only: it carries outbound presentation payloads.");

    public override void Write(Utf8JsonWriter writer, RawJson value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.Utf8, skipInputValidation: true);
}
