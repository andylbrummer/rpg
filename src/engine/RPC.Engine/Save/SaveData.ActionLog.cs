namespace RPC.Engine.Save;

public class SaveActionLogEntry
{
    public int Turn { get; set; }
    public int Act { get; set; }
    public string Category { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, string> Payload { get; set; } = new();
}
