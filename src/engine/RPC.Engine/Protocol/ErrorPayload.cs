namespace RPC.Engine.Protocol;

public class ErrorPayload
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public bool Recoverable { get; set; }
}
