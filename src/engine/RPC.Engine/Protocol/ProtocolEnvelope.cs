namespace RPC.Engine.Protocol;

public class ProtocolEnvelope
{
    public int V { get; set; }
    public string Type { get; set; } = "";
    public int Seq { get; set; }
    public int? AckSeq { get; set; }
    public object? Payload { get; set; }
}
