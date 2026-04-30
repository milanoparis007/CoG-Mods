# Illegal Backroom Consumption Patch Fallback

## Purpose

This document is for the Visual Studio pass that checks whether `GameplayTweaks` illegal-backroom compatibility patches are overriding or breaking the `StreamingAssets` behavior that was restored for illegal backrooms.

Current symptom:

- player-owned illegal backrooms are not consuming items again
- the likely conflict is between restored `StreamingAssets` single-backroom content and older `GameplayTweaks` logic that still classifies, filters, blocks, or bypasses illegal backroom modules too aggressively

The goal is not to redesign the illegal-backroom system. The goal is to make sure the runtime patch layer does not interrupt the `StreamingAssets` source of truth.

## Current source of truth

`StreamingAssets` should remain the authority for:

- which illegal backroom modules exist
- which modules consume or produce
- what upgrade families are valid
- what one-backroom-per-business behavior should be

`GameplayTweaks` should only do narrow compatibility work:

- avoid duplicate or broken menu entries
- avoid conflicts with the external dirty-cash plugin
- avoid invalid duplicate installs when needed

It should not silently suppress normal illegal backroom consume/produce behavior for a valid installed backroom.

## Most likely runtime collision points

Primary file:

- [GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs](/abs/path/C:/Users/User/source/repos/ClassLibrary1/GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs)

Main patch surfaces in that file:

1. `DirtyCashManufacturePrefixPrefix`
2. `ShouldBypassDirtyCashBackroomManufacture`
3. `FindModulesToAddForSlotPostfix`
4. `FindUpgradesOrNullPostfix`
5. `CanInstallPostfix`
6. `OnModuleAddConfirmPrefix`
7. `FindConflictingIllegalBackroom`
8. `IsIllegalBackroomModule`
9. `NormalizeIllegalBackroomFamily`
10. `ScoreIllegalBackroomChoice`

Why these are the likely suspects:

- `IsIllegalBackroomModule(...)` is broad and may classify more modules than intended
- `FindConflictingIllegalBackroom(...)` treats any other illegal backroom in the module list as a conflict
- `FilterDuplicateIllegalBackroomChoices(...)` may collapse valid families or city variants too hard
- `DirtyCashManufacturePrefixPrefix(...)` may be short-circuiting external dirty-cash manufacture in a way that still prevents normal consumption from running

## Main question for the VS pass

Do the `GameplayTweaks` illegal-backroom patches still assume an older “multiple illegal backrooms / duplicate filtering / dirty-cash override” world in a way that now suppresses valid single-backroom `StreamingAssets` consumption behavior?

That is the question to answer first before changing any more content.

## Recommended investigation order

1. Inspect live `Player.log` for any new lines mentioning:
   - `DirtyCashEconomy`
   - `manufacture override bypassed`
   - `illegal backroom`
   - `duplicate illegal backroom`
2. Confirm whether the external dirty-cash plugin is active through:
   - `GameplayTweaksPlugin.Compat.cs`
   - `ExternalDirtyCashEconomyDetected`
3. Put breakpoints or temporary logs on:
   - `DirtyCashManufacturePrefixPrefix`
   - `ShouldBypassDirtyCashBackroomManufacture`
   - `IsIllegalBackroomModule`
4. Verify whether the affected illegal backroom module is:
   - being classified as illegal
   - being bypassed at manufacture time
   - failing to consume because the compatibility prefix returns early
5. Check whether a valid installed backroom is being treated as conflicting with:
   - another module in the same building
   - an upgrade variant
   - a city-suffixed version of the same family

## Likely safe fixes

Prefer the smallest fix that restores `StreamingAssets` behavior.

## VS pass update

Confirmed external dirty-cash seam:

- `DirtyCashEconomy.BackroomPatches.DoConsumeAndPay_Prefix(ConsumerModule)`
- `DirtyCashEconomy.BackroomPatches.DoConsumeAndPay_Postfix()`
- existing `GameplayTweaks` compatibility only bypassed `DirtyCashEconomy.MultiThreadManufacturePatches.DoConsumeAndProduce_Prefix(...)`

Implementation direction for this pass:

- keep the illegal-backroom add/upgrade filtering logic separate
- add a narrow bypass for the external consumer prefix and its paired postfix
- apply that bypass only to modules that `GameplayTweaks` already classifies as illegal backrooms
- leave ordinary front-room / legal-business dirty-cash behavior alone

Why this is safer:

- the symptom is missing consume/pay on installed illegal backrooms, not missing add/upgrade options
- the external consumer hook is the seam most likely to intercept `ConsumerModule.DoConsumeAndPay(...)`
- skipping only the prefix would leave the external postfix running against a bypassed call, so both hooks need to be gated together

### Option 1: narrow manufacture bypass

If `DirtyCashManufacturePrefixPrefix(...)` is the issue:

- restrict the bypass to only the exact external dirty-cash scenarios that need it
- do not apply the bypass to ordinary player illegal backroom consume/produce paths if vanilla/runtime consume logic should still run

### Option 2: narrow illegal module classification

If `IsIllegalBackroomModule(...)` is too broad:

- stop using loose `id.IndexOf("backroom") >= 0` matching as the deciding rule
- move toward explicit family prefixes or a tighter whitelist
- avoid catching non-manufacture or non-conflicting modules just because they have `backroom` in the id

### Option 3: narrow duplicate/conflict detection

If install or upgrade filtering is the issue:

- keep the duplicate-install guard
- but only block truly conflicting illegal backroom families
- do not block legitimate single installed modules from upgrading or consuming because a helper thinks they conflict with themselves or with a harmless paired module

### Option 4: config-gate the compatibility patch

If the old compatibility logic is still useful for some mod stacks:

- add a narrow config gate for the illegal-backroom compatibility behavior
- default it to the mode that respects current `StreamingAssets`

## Current finding after video review

The latest runtime/video pass narrows the remaining symptom:

- the player backroom is building successfully
- the selected module becomes active after construction
- the remaining confusion is the authored consume cadence for the chosen consumer module

Specific example from the current test:

- `Small Protection Racket & Weapons Fence` maps to `gangdend-player`
- `gangdend-player` is a `consumer-module-config`
- its `purchase.buildTurns` is `2`
- its `sink.consumeDayz` is `125`
- in the UI that surfaces as `every 125 days (18 turns)`

What that means:

- if the module is installed around Turn 3, it can be active by Turn 5 and still appear to “do nothing”
- that is expected until the next consume window is reached
- the old dirty-cash compatibility bug was real for install/consumer hook interception, but this specific post-fix video is showing a long-cycle consumer more than a failed build

Follow-up added in code for the next test pass:

- keep the consumer-hook bypass in place
- add explicit log output when a human illegal backroom consumer is active but simply not due yet
- log `consumeDayz`, `daysSinceLast`, and `daysUntilNext` so future reports separate timing from hook failure immediately

## Additional finding after moonshine video

The later moonshine test points at a second, different failure mode:

- `Backroom Moonshine Operation` maps to `production-homebooze-moonshine`
- the module is a `manufacture-module-config`
- its authored cadence is only `produceConsumeDayz 28`
- the UI shows it as `every 28 days (4 turns)`
- the video shows the building sitting at `100% done` across multiple later turns with enough visible inputs and no production

Why that matters:

- this is not the same as the gangden-style `consumeDayz 125` delay
- if the moonshine module is still `100% done` after the next due window, the likely problem is not recipe timing
- the current log only showed the install-time disabled update, which suggests the building/module may not be entering the normal system-turn business update after install

Implementation follow-up added for this pass:

- track which human-controlled illegal-backroom buildings actually receive `ModulesComponent.DoUpdate(...)` during `BusinessUpdate.UpdateBusinessModules(...)`
- after the normal business sweep, run a guarded fallback `DoUpdate(...)` once for any human-controlled illegal-backroom building that was missed
- log that fallback so the next runtime test can confirm whether the module had dropped out of the standard business tick

## What not to do

- do not push the fix back into `StreamingAssets` if the real bug is Harmony/runtime logic
- do not loosen all module install rules globally
- do not remove the entire compatibility file unless testing proves the patch layer is fully obsolete
- do not rely on broad string matching if the bug is really family normalization or external dirty-cash detection

## Files to inspect in Visual Studio

Primary code:

- [GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs](/abs/path/C:/Users/User/source/repos/ClassLibrary1/GameplayTweaks/Features/Compatibility/GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs)
- [GameplayTweaksPlugin.Compat.cs](/abs/path/C:/Users/User/source/repos/ClassLibrary1/GameplayTweaks/GameplayTweaksPlugin.Compat.cs)
- [GameplayTweaksPlugin.FeatureRegistrars.cs](/abs/path/C:/Users/User/source/repos/ClassLibrary1/GameplayTweaks/GameplayTweaksPlugin.FeatureRegistrars.cs)

Primary content references:

- [Businesses.sim](/abs/path/C:/Users/User/source/repos/ClassLibrary1/Things%20To%20Have/Current%20After%20Prohibition%20Mod/StreamingAssets/Entities/Businesses.sim)
- [Modules.sim](/abs/path/C:/Users/User/source/repos/ClassLibrary1/Things%20To%20Have/Current%20After%20Prohibition%20Mod/StreamingAssets/Entities/Modules.sim)

Live runtime log:

- [Player.log](/abs/path/C:/Users/User/AppData/LocalLow/SomaSim/City%20of%20Gangsters/Player.log)

## Acceptance criteria

1. A valid installed illegal backroom in a player-owned building consumes inputs again.
2. Normal illegal backroom production also still works.
3. StreamingAssets-defined single illegal backroom behavior remains intact.
4. Duplicate or invalid illegal backroom install options are still guarded if that guard is still needed.
5. No new nulls or owned-business popup errors appear when opening add/upgrade backroom UI.
6. If the external dirty-cash plugin is active, only the truly necessary compatibility behavior remains active.

## Suggested short summary for the VS engineer

Treat `StreamingAssets` as the source of truth again for illegal backrooms. Audit `GameplayTweaks` illegal-backroom compatibility so it stops interrupting valid consume/produce behavior. Start with `DirtyCashManufacturePrefixPrefix(...)`, `IsIllegalBackroomModule(...)`, and the duplicate/conflict helpers in `GameplayTweaksPlugin.DirtyCashEconomyCompatibility.cs`.
