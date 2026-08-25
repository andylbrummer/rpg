# Protocol Contract & Type Generation

This directory holds the **client-side protocol contract** and the generator that
turns it into TypeScript types. It is one of three artifacts that describe the
player-action / envelope / state contract shared between the C# server and the
TypeScript client. Keeping the three in sync is enforced by an engine test
(see [Drift detection](#drift-detection)).

## The three protocol artifacts

| # | Artifact | Location | Role |
|---|----------|----------|------|
| 1 | **Server truth (actions)** | `src/engine/RPC.Engine/Commands/CommandDispatcher.cs` — `CommandDispatcher.KnownActions` | The actions the server actually parses. This is the **source of truth for which actions exist**. The set is derived from the dispatch table, so the accepted-action set can never drift from the dispatch logic. |
| 2 | **Test reference schema** | `src/engine/RPC.Tests/Fixtures/protocol-schema.json` | The schema the engine tests assert against (`protocolVersion`, `envelopeTypes`, `actions`, `stateShape`). Its `actions` list must match the server. |
| 3 | **Client union source** | `tools/protocol-gen/schema.json` (JSON Schema) | The source from which the TypeScript `PlayerAction` union and payload/envelope types are generated into `src/client/src/shared/types/protocol.gen.ts`. The client imports these generated types. |

## Regenerating client types

```bash
cd tools/protocol-gen
npm install          # first time only
node generate.js
```

This compiles `schema.json` into `../../src/client/src/shared/types/protocol.gen.ts`
via `json-schema-to-typescript`. Commit the regenerated `protocol.gen.ts` together
with any `schema.json` change.

## Drift detection

The engine test suite catches divergence between the three artifacts.
`src/engine/RPC.Tests/ProtocolActionSyncTests.cs` asserts, naming any offending
action(s):

- `CommandDispatcher.KnownActions` ↔ `Fixtures/protocol-schema.json` `actions`
- `CommandDispatcher.KnownActions` ↔ `tools/protocol-gen/schema.json` `PlayerAction` union

Run the drift check:

```bash
dotnet test src/engine --configuration Release --filter ProtocolActionSync
```

If the check fails:

- **A new server action** was added without updating the contracts → add the action
  to `Fixtures/protocol-schema.json` and to `tools/protocol-gen/schema.json`
  (then regenerate client types).
- **A contract lists an action the server does not parse** → add the case to
  `CommandDispatcher` or remove it from the contract.

### Adding a new player action — checklist

1. Add the dispatch entry in `CommandDispatcher` (`Factories` table).
2. Add the action to `src/engine/RPC.Tests/Fixtures/protocol-schema.json` `actions`.
3. Add the variant to `tools/protocol-gen/schema.json` `PlayerAction.oneOf`.
4. Regenerate client types: `cd tools/protocol-gen && node generate.js`.
5. Run `dotnet test src/engine --filter ProtocolActionSync` — must pass.
