# Campaign Module

## Scope
Campaign-wide state and logic: reputation, evidence, heat, world state, campaign configuration, mastermind discovery, and the wildcard alliance system.

## Public API
- `CampaignState` — aggregate holding all campaign-related mutable state
- `CampaignService` — orchestrates campaign logic (secrets, settlement fates, accusations, wildcard alliance)
- `CampaignConfig` — configuration for a campaign run (patron, threat, mastermind, scheme, complication)
- `ReputationState` — faction reputation tracking with attitude tiers
- `EvidenceState` — evidence counters and suspected-faction tracking
- `HeatState` — heat level with tiered consequences

## Dependencies
- `Overworld` (read-only, for turn-based timeline checks)
- `Town` (mutates quest log on wildcard alliance)
- `Character` (read-only, for class-based revelation checks)

## Boundary
Do NOT put dungeon logic, combat mechanics, or town vendor logic here.
