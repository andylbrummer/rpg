using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPC.Engine.Content;

public static class ContentJsonOptions
{
    public static readonly JsonSerializerOptions Standard = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };
}
