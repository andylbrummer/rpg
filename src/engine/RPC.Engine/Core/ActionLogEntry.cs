namespace RPC.Engine;

public record ActionLogEntry(int Turn, int Act, string Category, string Type, Dictionary<string, string> Payload);
