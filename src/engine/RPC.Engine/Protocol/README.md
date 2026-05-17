# Protocol Module

## Scope
Network protocol types shared between server and client: envelopes, payloads, and heartbeat definitions.

## Public API
- `ProtocolEnvelope` — wire format wrapper (V, Type, Seq, AckSeq, Payload)
- `HelloPayload` / `ErrorPayload` / `HeartbeatPingPayload` / `HeartbeatPongPayload` — specific payload types
- `PlayerAction` — union of all client actions

## Dependencies
- None (this is a leaf module with no engine dependencies)

## Boundary
Keep this free of gameplay logic. It defines only the contract between client and server.
