# Delivery Route Front Expansion Plan

Goal: add an optional route-step toggle for `Collect front` automation. When enabled, a successful cash collection from a paid front also tries to start expansion for the selected fronts in that route, and the acting crew member earns expansion experience.

## Current Anchors

- Delivery editor model: `decompiled/Game.UI.Session.Deliveries/DeliveriesEditModel.cs`
  - `GenerateAutomationStep()` writes `skipIfEmptyOnSell` and `skipIfFullOnBuy`.
  - `MakeActions()` exposes `AutoAction.FrontVisit`.
  - `MakeDestinationsForItems()` maps `FrontVisit` to player outposts/fronts.
- Delivery editor UI: `decompiled/Game.UI.Session.Deliveries/DeliveriesEditView.cs`
  - `RefreshCheckbox()` shows the existing checkbox for Buy/Sell skip behavior.
  - `OnCheckboxChanged()` writes skip flags back into the model.
- Automation data: `decompiled/Game.Session.Data/AutomationStep.cs`
  - Vanilla step fields are `target`, `action`, `items`, `enabled`, `skipIfFullOnBuy`, `skipIfEmptyOnSell`.
- Route execution: `decompiled/Game.Session.Player/AutomationExecutor.cs`
  - `PerformCurrentStep()` dispatches `AutoAction.FrontVisit`.
  - `DoFrontVisit()` validates the outpost, checks `MoneyStatus`, and calls `DoCollectFromOutpost()`.
- Front collection and expansion: `decompiled/Game.Session.Player/PlayerOutposts.cs`
  - `FindOutpostCollectionStatus()`
  - `CanCrewCollectFromOutpost()`
  - `DoCollectFromOutpost()`
  - `CanStartNewOutpostExpansion()`
  - `PickBestExpansion()`
  - `DoStartNewOutpostExpansion()`
- Manual expansion: `decompiled/Game.UI.Session.Convo/ConvoCallbacks.cs`
  - `ExecuteOutpostPayForExpansion()` pays expansion cost then starts the expansion.
- Manual expansion XP is currently data-driven in `Things To Have/Current After Prohibition Mod/CoG_Data/StreamingAssets/UI/ConvoBiz.sim`, where the expansion button grants `add-xp source from-social`.

## Minimal Implementation

1. Add GameplayTweaks sidecar state keyed by automation route id plus front target id.
   - Avoid adding new fields to vanilla `AutomationStep` until save serialization is proven safe.
   - Persist the sidecar in GameplayTweaks save data.
   - Clean sidecar entries when routes or steps are deleted.

2. Patch the delivery editor checkbox for `AutoAction.FrontVisit`.
   - Reuse `Edit Panel/Dest/Checkbox`.
   - Label: "Expand fronts".
   - Mouseover: "When this route collects from a paid front, also start expansion for selected route fronts when possible."
   - Existing Buy/Sell skip behavior remains unchanged.

3. Patch route execution after successful `DoFrontVisit()` collection.
   - Only trigger when the current front had positive cash and collection succeeded.
   - If enabled, scan the route's selected `FrontVisit` steps.
   - For each selected front:
     - verify `CanStartNewOutpostExpansion(outpostId)`;
     - build a `VisitState` from the route crew, front building, and current time;
     - call `PickBestExpansion(outpostId, visit)`;
     - pay `ExpansionDef.monthlyCost` through the same money path as `ExecuteOutpostPayForExpansion()`;
     - call `DoStartNewOutpostExpansion(outpostId, exp)`;
     - award `crew.GetPeep().components.agent.AddXP(XPSource.FromSocial)`.

4. Add a single summary ticker/log per route tick.
   - Log tag: `FrontRouteExpand`.
   - Include route id, trigger front, expanded count, skipped no-corner count, skipped no-money count, and XP awarded.

## Balance Defaults

- Expansion should not be free. Use the same expansion cost as manual front expansion.
- Award `FromSocial` once per expansion started, matching manual expansion's existing grant.
- Do not start expansion for fronts that need support cash, are already expanding, cannot expand, or cannot be paid for from the available cash source.

## Risks

- `PickBestExpansion()` needs a valid `VisitState`; route automation has the crew and front building, but the patch must build the same shape the convo path expects.
- Sidecar route state can drift when steps are reordered, deleted, copied, or routes are removed.
- External territory/auto-expand mods are intentionally gated by GameplayTweaks compatibility settings. This route toggle should use native `PlayerOutposts` APIs and not re-enable external auto-expand behavior.

## Test Scenario

1. Create a route with three `Collect front` steps.
2. Front A has positive cash and can expand.
3. Front B has positive cash but no valid expansion corner.
4. Front C needs support cash or is already expanding.
5. Toggle enabled: collecting A starts expansion for A, skips B/C with clear `FrontRouteExpand` logs, and grants `FromSocial` XP for started expansions.
6. Toggle disabled: collection behavior is unchanged and no expansion XP is awarded.
7. Reorder and delete route steps, then confirm sidecar state follows or cleans up.
