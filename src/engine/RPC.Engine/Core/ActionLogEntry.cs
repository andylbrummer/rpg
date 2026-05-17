namespace RPC.Engine;

public record ActionLogEntry(int Turn, string Category, string Type, Dictionary<string, string> Payload);
