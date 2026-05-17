namespace RPC.Engine.Protocol;

public class HelloPayload
{
    public int ProtocolVersion { get; set; }
    public string SessionId { get; set; } = "";
}
