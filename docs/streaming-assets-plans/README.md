# Streaming Assets Task Pack

This folder breaks the requested feature list into a small set of focused task docs.

## Primary Rule

These tasks should be implemented mostly through `StreamingAssets` content:

- entity templates
- business templates
- item definitions
- quest definitions
- convo / action definitions
- municipal / civic placements
- storage, warehouse, and trade content

Only use Harmony patches, custom methods, or code-side systems if the base data layer cannot express the behavior cleanly.

## Docs In This Pack

- `implementation-roadmap.md`
- `municipal-bank-building.md`
- `finance-accounts-and-high-value-deals.md`
- `train-station-bulk-liquor.md`
- `trait-passives-and-support-quests.md`
- `maritime-schemes-and-dock-contacts.md`
- `ranked-schemes-and-opportunity-quests.md`

## Patch Fallback Rules

Patch or method work should be treated as a last step, not the first step.

Use code only if one of these is true:

- municipal ownership or non-purchasable behavior cannot be enforced by data
- trait gating needs runtime checks the data layer cannot express
- weekly passive income or support ticks need a new scheduler hook
- union creation from multiple dock contacts cannot be modeled with content-only progression
- rank / stat scaling needs custom formulas instead of static requirements

## Clarifications Worth Confirming Later

These docs are structured enough to start, but these points may need your exact preference before implementation:

1. Whether offshore account discounts should be fixed or depend on accountant / connected / smart traits.
2. Whether the train-station bulk-liquor sale point should be a new warehouse business, a train-station attachment, or a storage-only interaction node.
3. Whether "random business unlock" scheme quests should unlock only one business per completion or build toward a weighted pool.
