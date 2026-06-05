# Enemy AI Crew Convo Robberies Phase Plan

## Goal

Revise the current gang robbery feature so enemy AI robberies against the player are rare, local, and explainable. A robbery should require a nearby hostile outfit and some form of contact with the player's crew. It should not feel like a remote random tax.

This is a new phase track. Keep it separate from the route/UI stabilization work unless route authority is needed only for locating physical crew positions.

## Current Anchors

- Player-initiated gang robbery is currently handled in `GameplayTweaks/Features/Stability/GameplayTweaksPlugin.UiStabilityPatches.cs` through:
  - `TryHandleGangRobberyConvoClick`
  - `RollGangRobberyCash`
  - `CalculateGangRobberySuccessChance`
  - `TryTriggerRobberyFailureRetaliation`
- The existing flow currently lets the human crew rob an AI gang through conversation and then applies cash, WarHeat, relationship buffs, Grapevine, and possible retaliation.
- Stock `ConvoInitiative.Topic` only supports `TiedHouse`, `TruceRequest`, `ExpansionHalt`, `StolenOutpost`, and `JointWar`. There is no stock robbery request topic, so AI robbery should not be implemented by pretending an existing topic is robbery.
- Existing useful vanilla anchors:
  - `CombatAdvisor.IsAggroWithoutTruce`, `IsAttackAllowed`, and `GetAggroAndTruce` for hostile/war gating.
  - `AttackAdvisor.FindNearbyAggroTargets` and `CanAttackForAggro` for local enemy crew discovery.
  - `PlayerTerritory.GetNodeOwner` and `PlayerTerritory.IsOwnerOfNode` for territory context.
  - `PlayerFinances.GetMoney`, `CanChangeMoney`, and `DoChangeMoney` for target-vehicle cash mutation.

## Non-Goals

- Do not add frequent random robberies.
- Do not allow remote theft without a nearby enemy, same-node contact, or an explicit AI approach that reaches the player.
- Do not turn this into combat or ambush damage. This pass is contact pressure without an attack.
- Do not drain the player's safehouse or create runaway snowball losses.
- Do not replace the existing player-initiated robbery conversation unless a shared helper can reduce duplication safely.

## Phase 0: Discovery And Save Surface

Status: Phase 0A implemented on 2026-05-27.

Tasks:

- Map every current player-initiated robbery mutation:
  - cash change;
  - WarHeat;
  - relationship buff;
  - Grapevine;
  - street credit;
  - failed robbery retaliation.
- Decide the AI robbery state surface:
  - preferred: GameplayTweaks-owned pending robbery event saved in mod save data;
  - avoid adding fake `ConvoInitiative.Topic` values because stock enum/data cannot persist unknown topics safely.
- Find the least invasive hook for per-turn candidate evaluation:
  - AI player turn update if already patched;
  - Gang Ops maintenance pass if that already owns WarHeat/revenge decisions;
  - player movement/arrival hook for passive same-node checks.
- Add no behavior yet. Only add low-noise diagnostics if needed:
  - `ai-robbery-discovery source=<hook> candidates=<n>`.

Validation:

- Build only if diagnostics are added.
- Confirm no current player-initiated robbery behavior changed.

Implementation note - 2026-05-27:

- Existing human-target AI robbery was found in `GameplayTweaksPlugin.Pacts.cs`:
  - `TryRunRareAiRobberyAgainstHuman`;
  - `TryExecuteAiGangRobbery`;
  - `RollAiGangRobberyCash`;
  - `CalculateAiGangRobberySuccessChance`.
- This path previously selected a remote AI robber on external trade-cycle days and could call `TryExecuteAiGangRobbery`, which transferred clean cash from the human player and could dispatch combat on failure.
- Added `AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED = false` so accidental human-victim execution is blocked while this feature is in the diagnostic/contact-design phase.
- No new save fields were added in this slice. Pending robbery state remains a later Phase 2/5 decision after candidate logs prove the right hook and eligibility gates.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 14:22:20`, size `2187776`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 13:31:23`, size `2181120` at staging time.

## Phase 1: Eligibility Gates

Status: Phase 1A implemented as diagnostics-only on 2026-05-27.

Create a pure helper that answers whether an AI gang may consider robbing the human player.

Required gates:

- Aggressor is an alive AI gang or goon-style hostile outfit, not cops and not defeated.
- Victim is the human player for the first implementation.
- Relationship gate passes if either:
  - the AI is at war/aggro without truce against the human; or
  - the AI is broadly hostile, meaning it is aggro toward several active players or has high independent WarHeat behavior.
- Nearby requirement passes only if there is a valid physical player crew/vehicle target within a small radius, or an active AI approach event has reached same-node contact.
- Cooldowns pass:
  - global robbery cooldown;
  - per-aggressor cooldown;
  - per-target-crew/vehicle cooldown;
  - per-node cooldown to prevent the same corner from firing repeatedly.
- Rarity passes:
  - initial chance should be low enough that normal play can go many turns without seeing it;
  - chance may rise slightly during active war or in enemy territory.

Suggested logs:

- `ai-robbery-candidate pid=<ai> target=<crew> node=<node> reason=<reason>`
- `ai-robbery-blocked pid=<ai> reason=<gate>`

Validation:

- Peaceful nearby gangs never qualify.
- Truced gangs never qualify.
- War/aggro gangs qualify only when local contact exists or an approach event has reached contact.

Implementation note - 2026-05-27:

- `TryRunRareAiRobberyAgainstHuman` now evaluates and logs candidates only; it does not take cash, create Grapevine entries, apply relationship buffs, or dispatch retaliation.
- Added `AiRobberyCandidate` diagnostics for:
  - direct aggro vs broad hostility;
  - same-node vs nearby contact;
  - contact node and target crew/vehicle;
  - enemy/human territory;
  - whole-outfit and local power;
  - rarity roll/chance and whether the future resolver would attempt.
- Added contact/strength gates:
  - protected pact alliance and truce block the candidate;
  - robber must be directly aggro/at war with the human or broadly hostile;
  - player crew must be same-node or within `AI_ROBBERY_NEARBY_WORLD_DISTANCE`;
  - outside enemy territory, robber must beat the player by whole-outfit power or local power;
  - in enemy territory, local strength only has to be plausible.
- New expected logs:
  - `AIPlayerRobbery discovery phase=eligibility-only ...`;
  - `AIPlayerRobbery candidate phase=eligibility-only ...`;
  - `AIPlayerRobbery blocked phase=diagnostic-only reason=human-cash-mutation-disabled ...` if any old path tries to execute against the human.
- Next validation should check that a peaceful nearby gang does not produce candidates, a truced gang is blocked, and an aggro/war gang only appears when physically near a human crew node.

## Phase 2: Contact Models

Status: Phase 2A diagnostics implemented on 2026-05-27; Phase 2B same-node miss log throttle implemented on 2026-05-27; Phase 2C same-node miss cash preview implemented on 2026-05-27; Phase 2D low-cash contact diagnostics implemented on 2026-05-27.

Support two contact routes, with the same final resolver.

Model A: passive player proximity

- When the player moves onto or near a node with eligible hostile AI crew, evaluate a rare robbery roll.
- Same node is the only immediate robbery trigger.
- Adjacent/nearby should usually create a candidate or warning state, not immediate cash loss.

Model B: AI approach without attack

- AI chooses a player crew/vehicle target and sends available crew toward it.
- The approach uses normal movement/command authority where possible.
- No attack command is issued.
- Robbery resolves only after the AI reaches the same node as the target crew/vehicle.
- If the player leaves before contact, the event may expire or retarget, but should not teleport.

Validation:

- Robbery cannot fire against a crew that was never physically met.
- Player movement into an enemy node can trigger the passive model.
- AI approach does not damage crew or start combat.
- Leaving the node before contact cancels or delays the event cleanly.

Implementation note - 2026-05-27:

- Added a daily human-turn contact diagnostic pass:
  - `RunAiHumanRobberyContactPhase`;
  - `ProcessPendingAiHumanRobberyContacts`;
  - `ProcessAiHumanRobberyCandidateContact`.
- The daily pass reuses Phase 1 gates but logs only when the candidate is same-node or the rarity roll would attempt, so routine peaceful/blocked checks do not spam logs every turn.
- Added a runtime-only pending contact dictionary for human-target AI robbery candidates:
  - same-node candidates log contact immediately;
  - nearby candidates whose rarity roll passes log a pending approach marker;
  - pending contacts expire after 14 days or clear if truce/protection/non-hostile state appears;
  - if a pending robber and target naturally become same-node later, the contact logs and clears.
- This is still diagnostics-only:
  - no AI movement command is issued yet;
  - no cash is removed;
  - no WarHeat, relationship buff, Grapevine entry, or attack is triggered.
- New expected logs:
  - `AIPlayerRobbery discovery phase=eligibility-only source=human-turn-contact ...` only for same-node or would-attempt candidates;
  - `AIPlayerRobbery pending phase=approach-diagnostic ... result=no-ai-movement-yet`;
  - `AIPlayerRobbery contact phase=contact-diagnostic ... result=resolution-disabled`;
  - `AIPlayerRobbery expired phase=approach-diagnostic ... reason=timeout`;
  - `AIPlayerRobbery cleared phase=approach-diagnostic ... reason=<truce|pact-protected|not-hostile>`.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 14:58:25`, size `2192384`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 14:22:20`, size `2187776` at staging time.

Implementation note - 2026-05-27, Phase 2B:

- Live `2026-05-27 14:58:25` logs proved the contact gate was finding a same-node hostile candidate:
  - `source=human-turn-contact`;
  - `sameNode=True`;
  - `directAggro=True`;
  - `enemyTerritory=True`;
  - `rarity=<roll>/0.18`;
  - `wouldAttempt=False`.
- The repeated same-node rarity misses were useful proof, but they would log every turn while the player stayed on that hostile node.
- Added a runtime-only same-node miss log throttle:
  - same-node misses still log as periodic proof;
  - repeated misses for the same robber/crew are suppressed for `21` days;
  - would-attempt candidates and actual contact previews are not suppressed by this miss throttle.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 18:45:54`, size `2196480`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 14:58:25`, size `2192384` at staging time.

Implementation note - 2026-05-27, Phase 2C:

- The live `2026-05-27 18:45:54` run did not reach a robbery attempt before the next pass:
  - early external checks were blocked by low human cash;
  - the previous same-node war/contact evidence stayed useful, but all observed same-node rolls were rarity misses.
- Added a non-destructive same-node rarity-miss preview so the safehouse cap math can be validated without waiting for a rare successful roll:
  - emits `AIPlayerRobbery preview phase=rarity-miss ... result=no-attempt`;
  - includes `cashPreview`, `safehouseCash`, `totalCash`, `canDebitSafehouse`, `cashReason`, and `mutationEnabled=False`;
  - uses the same 21-day same-node miss throttle, so it should not spam every turn.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged/live DLL:
  - `GameplayTweaks.dll` timestamp `2026-05-27 18:54:09`, size `2197504`.

Implementation note - 2026-05-27, Phase 2D:

- The live `2026-05-27 18:54:09` run was below the human cash minimum during observed robbery checks:
  - external checks logged `reason=low-human-cash`;
  - no `human-turn-contact` candidate or same-node preview could prove the contact/cash math on that save while the early cash gate returned first.
- Adjusted the human-turn contact diagnostic pass only:
  - external trade-cycle checks still return immediately under `AI_ROBBERY_LOW_CASH_MIN`;
  - daily `human-turn-contact` diagnostics may continue scanning under low cash so same-node proximity can still be observed;
  - discovery/candidate logs now include `cashBelowMin=<true|false>`;
  - would-attempt same-node contacts under low cash log `blocked phase=contact-preview reason=low-human-cash ... result=no-attempt`;
  - nearby low-cash contacts do not create pending approach markers.
- Cash mutation remains disabled.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 19:02:56`, size `2198528`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 18:54:09`, size `2197504` at staging time.

## Phase 3: Territory And Strength Rules

Status: planned.

Territory should change the strength threshold.

Rules:

- On player-owned territory or neutral territory:
  - AI needs a clear local advantage over the target crew or a stronger overall gang score than the player.
- On AI-owned territory:
  - AI does not need to be stronger than the entire player outfit;
  - AI still needs enough local strength to plausibly stop the target crew and "keep up" with the player.
- On third-party territory:
  - use the stricter player/neutral rule unless the third party is allied with the aggressor in a later pass.

Initial "keep up" definition:

- AI has at least one living, available crew member near the target or assigned to the approach.
- AI crew has acceptable health.
- AI is not already busy with a higher-priority attack or critical route.
- Local target comparison is not wildly unfavorable.

Implementation note:

- Reuse or wrap existing `CalculateGangPower` for whole-outfit scoring.
- Add a small local-power scorer for the specific crew/vehicle contact to avoid a weak scout robbing a loaded car only because the gang is strong elsewhere.

Validation:

- Weak hostile gang on player territory does not rob.
- Hostile gang inside its own territory can rob without beating the entire player outfit, but only if the local encounter is plausible.
- Stronger hostile gang can rob on contested/player territory if other gates pass.

## Phase 4: Cash Amount And Vehicle Cap

Status: Phase 4A dry-run implemented on 2026-05-27; Phase 4B robbable-cash gate implemented on 2026-05-27; Phase 4C target-vehicle cash preview implemented on 2026-05-27; Phase 4D vehicle-only cash model implemented on 2026-05-27.

Robbery payout should be modest and based on the player's available money.

Rules:

- Target amount should usually feel like a vehicle cash pickup: about `$500-$1,000`.
- Scale from player cash so low-cash players are not crushed.
- Never exceed the cash physically carried by the contacted target vehicle.
- Never exceed total player cash.
- Prefer clean cash mutation through target-vehicle methods for the actual resolver:
  - read with `human.finances.GetMoney(targetCrew.VehicleID.FindEntity())`;
  - validate with `CanChangeMoney(targetVehicle, new Price(-amount))`;
  - mutate with `DoChangeMoney(targetVehicle, new Price(-amount), MoneyReason.Other)`.

Suggested initial amount:

```text
available = min(targetVehicleCash, totalCash)
base = clamp(round_to_50(available * 0.03), 100, 1000)
warBonus = activeWar ? 100 : 0
territoryBonus = enemyTerritory ? 100 : 0
amount = min(available, clamp(base + warBonus + territoryBonus, 100, 1000))
```

Validation:

- Low target-vehicle cash caps the robbery.
- High cash still caps at around `$1,000`.
- No negative vehicle balance.

Implementation note - 2026-05-27:

- Added a resolution-preview helper for same-node AI robbery contact:
  - reads total clean cash through the existing gang clean-cash helper;
  - reads safehouse clean cash directly from the gang safehouse inventory;
  - caps available money to `min(totalCash, safehouseCash)`;
  - previews a modest rounded amount using about `3%` of available cash plus small war/enemy-territory bonuses;
  - clamps the preview between `$100` and `$1,000`, still capped by available safehouse cash;
  - checks `CanChangeMoneyOnSafehouse` but does not mutate money.
- Contact logs now include:
  - `cashPreview=<amount>`;
  - `safehouseCash=<amount>`;
  - `totalCash=<amount>`;
  - `canDebitSafehouse=<true|false>`;
  - `mutationEnabled=False`;
  - `result=resolution-disabled`.
- Added runtime-only diagnostic cooldowns after a contact preview:
  - per robber/crew for `45` days;
  - per robber/node for `30` days;
  - cooldowns prevent repeated same-contact diagnostics while the real resolver remains disabled.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 15:05:33`, size `2195968`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 14:58:25`, size `2192384` at staging time.

Implementation note - 2026-05-27, Phase 4B:

- Live `2026-05-27 19:02:56` logs proved the cash preview path worked, but also exposed that total clean cash and safehouse clean cash can diverge:
  - observed `totalCash=100425` with `safehouseCash=0`;
  - preview correctly produced `cashPreview=0` and `canDebitSafehouse=False`.
- Updated the human-victim robbery cash gate to use robbable cash instead of total clean cash:
  - `availableCash = min(totalCash, safehouseCash)`;
  - external trade-cycle checks block on `reason=low-robbable-cash` when the safehouse-capped amount is below the minimum;
  - human-turn contact diagnostics may still scan for same-node proof, but now report `availableCash`, `safehouseCash`, and `totalCash`.
- Contact, rarity-miss, and low-cash preview logs now include `availableCash`.
- Cash mutation remains disabled.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged/live DLL:
  - `GameplayTweaks.dll` timestamp `2026-05-27 19:16:31`, size `2199040`.

Implementation note - 2026-05-27, Phase 4C:

- Live `2026-05-27 19:16:31` logs proved the safehouse-only gate blocks correctly when `safehouseCash=0`, including a same-node would-attempt case:
  - `cashPreview=0`;
  - `availableCash=0`;
  - `safehouseCash=0`;
  - `totalCash=100450`;
  - `reason=low-robbable-cash`.
- Because the robbery contact targets a crew vehicle, extended candidate previews to also read the target vehicle's clean cash:
  - preview now reports `vehicleCash=<amount>`;
  - preview now reports `canDebitVehicle=<true|false>`;
  - candidate-level `cashBelowMin` now uses `min(totalCash, safehouseCash + targetVehicleCash)`;
  - external trade-cycle pre-scan still has no specific target vehicle, so it continues to gate on safehouse-capped cash only.
- This remains diagnostics-only; no vehicle or safehouse cash is removed.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 19:27:06`, size `2200576`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 19:16:31`, size `2199040` at staging time.

Implementation note - 2026-05-27, Phase 4D:

- User direction: keep AI robberies as taking cash from the contacted crew vehicle rather than the safehouse.
- Updated the preview/cash gate to vehicle-only:
  - `availableCash = min(totalCash, targetVehicleCash)`;
  - `safehouseCash` remains in logs only as context;
  - `CanDebitSafehouse` is no longer used to qualify the preview;
  - successful future resolver should debit the target vehicle through `PlayerFinances.DoChangeMoney(vehicle, ...)`.
- External pre-scan still has no target vehicle, so it should not enable human-target robbery resolution on its own.
- Cash mutation remains disabled until the vehicle-only resolver is explicitly enabled in Phase 5.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 19:31:33`, size `2200064`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 19:27:06`, size `2200576` at staging time.

## Phase 5: Resolution, UX, And Cooldowns

Status: Phase 5A vehicle-debit resolver implemented on 2026-05-27; Phase 5B long cooldown tuning implemented on 2026-05-27.

On successful AI robbery:

- Remove the chosen cash amount from the contacted target vehicle.
- Add modest WarHeat or relationship impact in the correct direction.
- Add a concise ticker/Grapevine-style event with:
  - robber gang name;
  - target crew or corner;
  - cash amount;
  - no combat wording unless future combat is added.
- Set cooldowns immediately after resolution.

On failed/blocked contact:

- Do not take money.
- Usually do not trigger retaliation; the player was the victim of the attempt.
- Log the reason for tuning.

Suggested logs:

- `ai-robbery-contact pid=<ai> targetCrew=<id> node=<node> mode=<passive|approach>`
- `ai-robbery-cash pid=<ai> vehicle=<before> amount=<n> after=<after> cap=<cap>`
- `ai-robbery-cooldown pid=<ai> target=<crew> nextDay=<day>`

Validation:

- Ticker text is clear and not spammy.
- Robbery does not repeat against the same crew/corner for multiple turns.
- Existing player-initiated robbery conversation still works.

Implementation note - 2026-05-27, Phase 5A:

- Live `2026-05-27 19:31:33` logs proved the vehicle-only model is viable:
  - same-node contact passed rarity;
  - `cashPreview=1000`;
  - `vehicleCash` was around `$98k`;
  - `canDebitVehicle=True`;
  - safehouse cash stayed `0`, which no longer matters for the robbery amount.
- Enabled the same-node contact resolver for the vehicle-only path:
  - `AI_ROBBERY_CONTACT_VEHICLE_DEBIT_ENABLED = true`;
  - legacy external human-cash mutation remains disabled with `AI_ROBBERY_HUMAN_CASH_MUTATION_ENABLED = false`;
  - debit is only attempted after same-node contact and a passed rarity roll;
  - debit uses `PlayerFinances.DoChangeMoney(targetVehicle, new Price(-amount), MoneyReason.Other)`;
  - robber gang safehouse is credited when possible;
  - a modest WarHeat gain is applied;
  - Grapevine logs a no-shots robbery message;
  - existing crew/node cooldowns are recorded after successful debit.
- New expected success log:
  - `AIPlayerRobbery resolved phase=vehicle-debit ... cash=<amount> vehicleBefore=<n> vehicleAfter=<n> robberCredited=<true|false> result=success`.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 19:39:15`, size `2203648`.
- Live DLL status:
  - live Steam DLL was still `2026-05-27 19:31:33`, size `2200064` at staging time.

Implementation note - 2026-05-27, Phase 5B:

- Live `2026-05-27 19:39:15` logs proved the resolver was active and taking cash from contacted vehicles:
  - `resolved phase=vehicle-debit ... cash=1000 vehicleBefore=98643 vehicleAfter=97643 ... result=success`;
  - a later pending contact also resolved with vehicle debit;
  - a different hostile gang could still rob the same player vehicle soon after because the cooldown only covered robber/crew and robber/node keys.
- Extended human-target AI robbery cooldowns to about three months:
  - crew/robber cooldown: `91` days;
  - robber/node cooldown: `91` days;
  - target crew cooldown independent of robber: `91` days;
  - target vehicle cooldown independent of robber: `91` days.
- Added a separate inter-gang robbery pair cooldown:
  - successful and failed AI-vs-AI gang robberies now record a `91` day unordered pair cooldown;
  - blocked attempts log `reason=gang-robbery-cooldown`.
- Updated authored StreamingAssets player-initiated robbery cooldowns:
  - `relbuff-gangs-robbery1-table-on-finish` duration `42 -> 91` days;
  - `relbuff-gangs-robbery2-table-on-finish` duration `42 -> 91` days;
  - applied to both the personal authored StreamingAssets tree and the current public package copy.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 19:58:57`, size `2205184`.
- Live DLL status:
  - final verification showed the Steam live DLL also at `2026-05-27 19:58:57`, size `2205184`.

## Phase 6: Tuning Pass

Status: Phase 6A cooldown-aware miss-preview cleanup implemented on 2026-05-27; Phase 6B player response popup implemented on 2026-05-27; Phase 6C active prompt attack guard implemented on 2026-05-27; Phase 6D human retaliation crew-hit moderation implemented on 2026-05-27.

Tune after at least one short war-focused run and one normal run.

Watch for:

- too many robberies during a war;
- robberies while player is nowhere nearby;
- weak gangs robbing implausibly;
- amount feeling too punitive;
- cooldowns blocking all events forever;
- AI approach events clogging command queues.

Tuning knobs:

- base chance;
- war chance bonus;
- enemy-territory chance bonus;
- cooldown days;
- max approach distance;
- local strength threshold;
- cash percentage and cap.

Implementation note - 2026-05-27, Phase 6A:

- Live `2026-05-27 19:58:57` logs proved the 91-day cooldown was active:
  - a robbery resolved on day `701344` with `cooldownDays=crew:91,node:91,target:91,vehicle:91`;
  - a later would-attempt on day `701407` was blocked until day `701435`.
- The same run also showed cooldowned same-node candidates could still emit rarity-miss preview logs while the target was cooling down.
- Updated the same-node rarity-miss preview gate:
  - before logging a `preview phase=rarity-miss`, it now checks the active robbery cooldown keys;
  - cooldowned candidates still block and log if rarity would actually attempt;
  - routine no-attempt preview noise should stop during the 91-day cooldown window.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 20:12:33`, size `2205184`.
- Live DLL status:
  - live Steam DLL remained `2026-05-27 19:58:57`, size `2205184` at staging time.

Implementation note - 2026-05-27, Phase 6B:

- Latest live logs showed two related but separate behaviors:
  - AI robbery contact resolved immediately with `resolved phase=vehicle-debit ... result=success`;
  - later delayed attacks came from `GangOps.Independent.Revenge` / `PactRetaliation`, not from the AI robbery resolver itself.
- Updated same-node AI robbery contact so the human player gets a response popup before money is removed:
  - `AI_ROBBERY_CONTACT_PLAYER_RESPONSE_ENABLED = true`;
  - a same-node passed rarity roll now logs `prompt phase=response-popup ... result=shown`;
  - the popup uses the stock two-button modal in a short chain:
    - if a favor is available with the robber outfit's representative, the first choice is `Use Favor` or `Other`;
    - `Other` opens `Pay $<amount>` or `Refuse`;
    - without a favor, the first popup is `Pay $<amount>` or `Refuse`.
- Response outcomes:
  - `Use Favor`: spends one available social ticket from the robber representative to the human player, takes no cash, and logs `result=favor`;
  - `Pay`: uses the existing vehicle-only resolver, debits only the contacted crew vehicle, credits the robber safehouse when possible, and logs `mode=<mode>-pay`;
  - `Refuse`: does not issue an attack command; it rolls a territory/local-power based stand-firm chance, applies modest non-lethal injury risk, may lose part of the requested vehicle cash, and may grant street credit if the crew holds firm.
- Cooldowns are recorded when the popup is shown so repeated prompts cannot stack while the player is deciding.
- If UI is unavailable when contact resolves, the pass logs `blocked phase=response-popup reason=ui-unavailable result=no-debit` and does not silently take money.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.

Expected next-run proof:

- When same-node AI robbery contact passes rarity, the player should see a popup instead of an automatic cash debit.
- Logs should show `AIPlayerRobbery prompt phase=response-popup ... result=shown`.
- Choosing `Pay` should show `resolved phase=vehicle-debit ... mode=...-pay ... result=success`.
- Choosing `Use Favor` should show `resolved phase=response-popup ... result=favor` and not remove vehicle cash.
- Choosing `Refuse` should show `resolved phase=response-popup ... result=refused` with `heldFirm`, `injured`, `damage`, and vehicle cash fields.
- No `PactRetaliation` or `CommandAttack` should be dispatched by the AI robbery response path itself.

Implementation note - 2026-05-27, Phase 6C:

- Live `20:48:54` logs proved the response popup and pay path worked:
  - `prompt phase=response-popup ... result=shown`;
  - `resolved phase=vehicle-debit ... mode=passive-same-node-pay ... result=success`.
- The same run also showed unrelated `PactRetaliation` / `CommandAttack` dispatches while the robbery response popup was active.
- Added an active human robbery-response guard:
  - when a robbery response popup is shown, the human player id is marked active;
  - pay/favor/refuse clears the active marker;
  - `TryDispatchRuntimeGangAttack` now blocks runtime gang attack dispatch against the human while that marker is active;
  - blocked dispatch logs `PactRetaliation runtime-dispatch-blocked-ai-robbery-response ...`.
- This does not remove existing revenge/retaliation behavior after the player resolves the robbery response. It only prevents attacks from firing while the robbery choice is pending.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.

Expected next-run proof:

- During an active robbery response popup, unrelated retaliation should not produce `CommandAttack` against the human.
- If a retaliation tries to fire during the popup, log should show `runtime-dispatch-blocked-ai-robbery-response`.
- After choosing pay/favor/refuse, normal war retaliation behavior can resume on later checks.

Implementation note - 2026-05-27, Phase 6D:

- Live `21:02:45` logs confirmed Phase 6C worked:
  - `runtime-dispatch-blocked-ai-robbery-response` appeared;
  - the active robbery prompt retaliation resolved with `action=none taken=False crews=0`.
- The same run showed the broader retaliation scorer was still too eager to hit human crews:
  - stored `TargetCrewPeepId` values made revenge look `severe=True`;
  - low-heat examples such as `heat=9.0` or `heat=14.0` could score `action=HitCrew`.
- Updated retaliation scoring for human defenders:
  - a preferred target peep no longer makes human-directed revenge severe by itself;
  - ordinary `threshold` / `first-hit` revenge against the human no longer enables direct crew hits unless heat reaches the coordinated-attack threshold;
  - true severe human sources still allow crew hits, such as boss, kill, or leader events;
  - non-human gang retaliation behavior keeps the existing preferred-target/severe behavior.
- Added `hitCrew=<true|false>` to `GangOps.RetaliationScore` logs so the next run can prove whether a scored retaliation is allowed to dispatch a crew attack.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.

Expected next-run proof:

- Low-heat human-directed revenge should log `hitCrew=False` and should not dispatch `PactRetaliation runtime-dispatch ... source=Independent-revenge`.
- Boss/kill/leader revenge, or very high heat at/above the coordinated-attack threshold, can still hit human crews.
- AI-vs-AI revenge should remain able to use preferred-target crew hits as before.

## Phase 7: Optional Expansion

Status: Phase 7A warning-only pending approach counterplay implemented on 2026-05-27; Phase 7B warning closure implemented on 2026-05-27; Phase 7C active-contact closure suppression implemented on 2026-05-27 and live-validated on 2026-05-28; Phase 7D strict physical contact authority staged on 2026-05-28; Phase 7H route-sim heal expected-node-only staged on 2026-05-28; broader expansion remains future.

Only after the human-victim implementation is stable:

- Let AI rob other AI outfits under the same contact rules.
- Add a warning/counterplay state before a known strong enemy approaches.
- Tune or guard delayed retaliation/combat so robbery contact does not feel like an ambush chain.
- Let pacts alter the cooldown or retaliation pressure without turning this into constant theft.

Implementation note - 2026-05-27, Phase 7A:

- Live `21:33:04` logs showed the response popup/refuse path worked:
  - `prompt phase=response-popup ... result=shown`;
  - `resolved phase=response-popup ... mode=natural-same-node-refuse ... cashDebited=True ... injured=True ... result=refused`.
- The same run had pending approach diagnostics such as:
  - `pending phase=approach-diagnostic ... result=no-ai-movement-yet`.
- Added warning-only counterplay for pending approach contacts:
  - a nearby would-attempt candidate still creates only a pending diagnostic marker;
  - no AI movement, cash mutation, or attack dispatch was added;
  - the first pending marker for the robber/crew pair posts a Grapevine warning that the gang is shadowing one of the player's crews;
  - warnings use a separate 28-day robber/crew cooldown so an expired pending marker does not spam the player.
- New expected log markers:
  - `AIPlayerRobbery warning phase=approach-diagnostic ... cooldownDays=28 result=shown`;
  - `AIPlayerRobbery pending phase=approach-diagnostic ... warningShown=True result=no-ai-movement-yet`.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 21:55:59`, size `2217472`.
- Live DLL status:
  - Steam live DLL also showed `2026-05-27 21:55:59`, size `2217472` after staging.

Implementation note - 2026-05-27, Phase 7B:

- Live `21:55:59` logs proved Phase 7A worked:
  - `AIPlayerRobbery warning phase=approach-diagnostic ... cooldownDays=28 result=shown`;
  - matching `pending phase=approach-diagnostic ... warningShown=True result=no-ai-movement-yet`;
  - a pending warned contact later reached same-node contact and showed the response popup before resolving `result=refused`.
- Issue:
  - warned pending contacts could later expire or clear because of hostility changes, but the player-facing warning had no closure message.
- Updated:
  - pending contacts now remember whether a Grapevine warning was shown;
  - when a warned pending contact expires, Grapevine tells the player the outfit lost track of the crew;
  - when a warned pending contact clears because of truce, pact protection, or no longer hostile, Grapevine tells the player the outfit backed off;
  - no AI movement, cash mutation, or attack dispatch was added.
- New expected log markers:
  - `AIPlayerRobbery warning-cleared phase=approach-diagnostic ... reason=timeout result=closed`;
  - `AIPlayerRobbery expired phase=approach-diagnostic ... warningClosed=True reason=timeout`;
  - or `AIPlayerRobbery cleared phase=approach-diagnostic ... warningClosed=True`.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 22:13:45`, size `2218496`.
- Live DLL status:
  - Steam live DLL remained `2026-05-27 21:55:59`, size `2217472` at staging time.

Implementation note - 2026-05-27, Phase 7C:

- Live `22:13:45` logs proved Phase 7B worked:
  - `AIPlayerRobbery warning-cleared phase=approach-diagnostic ... reason=timeout result=closed`;
  - matching `expired phase=approach-diagnostic ... warningClosed=True reason=timeout`.
- Issue:
  - one warned pending contact expired on day `702352` and logged the "lost track" closure;
  - the same human-turn scan immediately found the same robber/crew at same-node contact and showed `prompt phase=response-popup ... mode=passive-same-node ... result=shown`;
  - that creates contradictory player-facing feedback on the same turn.
- Updated:
  - timeout closure now checks whether the same robber/crew pair is still an active nearby or same-node contact before logging the closure Grapevine;
  - the active-contact check reuses existing hostility, contact-distance, and strength gates without rolling rarity;
  - if the approach is still active, the expired diagnostic suppresses the closure and logs why;
  - truce, pact-protected, and not-hostile clears still close warned approaches normally.
- New expected log markers:
  - `expired phase=approach-diagnostic ... warningClosed=False closureSuppressed=True closureReason=active-same-node reason=timeout`;
  - or `closureReason=active-nearby` for a still-nearby target;
  - no matching `warning-cleared ... reason=timeout` should appear for that suppressed same-turn contact.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-27 22:37:09`, size `2219008`.
- Live DLL status:
  - Steam live DLL remained `2026-05-27 22:13:45`, size `2218496` at staging time.

Live validation note - 2026-05-28:

- Startup logs confirmed the expected Phase 7C DLL was active live:
  - `GameplayTweaks.dll built=2026-05-27 22:37:09`.
- Live approach warnings and pending markers appeared as expected:
  - day `702296`: `warning phase=approach-diagnostic ... cooldownDays=28 result=shown`;
  - day `702296`: `pending phase=approach-diagnostic ... expireDay=702310 warningShown=True result=no-ai-movement-yet`.
- The contradictory timeout closure was suppressed while the same robber/crew contact remained active:
  - day `702317`: `expired phase=approach-diagnostic ... warningClosed=False closureSuppressed=True closureReason=active-nearby reason=timeout`;
  - repeated active-nearby suppressions also appeared on days `702373`, `702401`, and `702464`.
- No exception, `NullReference`, or Harmony exception markers appeared in the latest live log window.
- No new code patch was made for this pass; the next step should remain validation/tuning of the response outcomes before enabling real AI movement.

Follow-up live validation note - 2026-05-28:

- A later live run on the same DLL validated the same flow on a different save timeline:
  - day `701344`: `prompt phase=response-popup ... mode=passive-same-node ... favors=0 ... result=shown`;
  - day `701344`: `resolved phase=response-popup ... mode=passive-same-node-refuse ... cashDebited=True ... injured=True ... result=refused`;
  - day `701351`: the follow-up same-node contact blocked on the long cooldown with `blocked phase=resolution-preview ... reason=diagnostic-cooldown ... untilDay=701435`.
- Approach warning behavior also covered both closure branches in one run:
  - day `701456`: robber `28` and robber `34` both posted `warning phase=approach-diagnostic` and `pending ... warningShown=True`;
  - day `701477`: robber `28` expired while still active same-node and logged `warningClosed=False closureSuppressed=True closureReason=active-same-node`;
  - day `701477`: robber `34` expired while no longer nearby and logged `warning-cleared ... reason=timeout result=closed` followed by `warningClosed=True closureSuppressed=False closureReason=too-far`.
- Human-directed retaliation moderation behaved as intended for the high-heat severe case:
  - day `701491`: `GangOps.RetaliationScore ... heat=84.0 ... hitCrew=True severe=True`, followed by `PactRetaliation runtime-dispatch ... source=Independent-auto-coord-retaliation`;
  - this does not contradict the Phase 6D low-heat guard because the event was above the coordinated-attack threshold and marked severe.
- No exception, `NullReference`, or Harmony exception markers appeared in the latest live log window.
- No new code patch was made for this pass; remaining response validation is the `Pay` path on this save and the `Use Favor` path when a favor exists.

Implementation note - 2026-05-28, Phase 7D:

- User-reported symptom:
  - movement and reactions appear to initialize late or from stale corner state;
  - cops/gangs can look tied to a corner before or after their visible vehicle arrival;
  - enemy gang healing/attacks may appear one or two turns late, suggesting mixed logical-vs-physical node authority.
- Live evidence supporting this as the next narrow pass:
  - route logs showed route-preview authority such as `route-sim-access ... source=route-sim-expected`;
  - strict action paths still logged `vehicle-not-physical`, proving the repo already distinguishes route preview from physical arrival;
  - AI robbery contact was still using general `TryGetAuthoritativeVehicleNode`, which can include logical/authority state rather than strict vehicle position.
- Updated AI human robbery contact discovery:
  - vehicle crews now contribute to AI robbery contact only when `TryGetStrictPhysicalVehicleNode` resolves a physical board/world-position node;
  - route-preview, selected-final-goal, pending expected, stale logical, and other non-physical vehicle authority no longer make a vehicle crew count as same-node or nearby for AI robbery contact;
  - on-foot crews still use their agent node.
- Added contact-source diagnostics:
  - candidate logs now include `robberNodeSource=<source>` and `targetNodeSource=<source>`;
  - expected physical vehicle contacts should show `physical`;
  - if a vehicle is still moving or not physically resolved, that crew should be absent from the candidate rather than logging a preview source.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `Things To Have\Current After Prohibition Mod\BepInEx\plugins\GameplayTweaks.dll`;
  - timestamp `2026-05-28 08:55:43`, size `2219520`.
- Live DLL status:
  - Steam live DLL also showed `2026-05-28 08:55:43`, size `2219520` after staging.

Implementation note - 2026-05-28, Phase 7E:

- Live validation of Phase 7D startup:
  - Steam live still loaded `GameplayTweaks.dll built=2026-05-28 08:55:43`;
  - no exception, `NullReference`, or Harmony exception markers appeared in the latest searched log window;
  - no new `human-turn-contact` AI robbery candidate-source logs appeared yet, so Phase 7D physical-source contact validation still needs an in-game contact.
- New live evidence matching the user's delayed healing/reaction report:
  - `route-sim-access ... source=route-sim-expected action=heal`;
  - `route-sim-heal ... source=route-sim-expected`;
  - `heal-validate-enabled ... source=NID_179:route-sim-expected`;
  - `heal-vehicle-group ... source=NID_179:route-sim-expected`.
- Updated route-sim healing authority:
  - `GameplayTweaks` now treats `heal` as a physical-only route-sim action and blocks route-preview healing at the shared route-sim access gate;
  - expected blocked marker is `route-sim-physical-only-blocked ... action=heal reason=physical-only-action`;
  - `AfterProhibitionRoutes` now also classifies `heal` as physical-only so decision logs match the enforced behavior.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`;
  - both passed with `0` warnings and `0` errors.
- Staged DLLs:
  - `GameplayTweaks.dll` timestamp `2026-05-28 09:05:08`, size `2220032`;
  - `AfterProhibitionRoutes.dll` timestamp `2026-05-28 09:03:12`, size `49152`.
- Live DLL status:
  - Steam live `GameplayTweaks.dll` remained `2026-05-28 08:55:43`, size `2219520`;
  - Steam live `AfterProhibitionRoutes.dll` remained `2026-05-14 11:07:42`, size `49152`;
  - next in-game validation needs the staged DLLs copied live before this phase can be tested.

Implementation note - 2026-05-28, Phase 7F:

- Live validation of Phase 7E:
  - Steam live loaded `GameplayTweaks.dll built=2026-05-28 09:05:08`;
  - route-preview heal now logs `route-sim-physical-only-blocked ... source=route-sim-expected action=heal reason=physical-only-action`;
  - the same turn later logs `heal-validate-enabled ... source=NID_179:physical` and `heal-vehicle-group ... source=NID_179:physical`, proving healing waits for physical arrival.
- New video/log evidence:
  - supplied video `C:\Users\User\Videos\2026-05-28 09-10-53.mp4` is `00:04:42`, `1152x720`, `30 fps`;
  - sampled video frame shows the selected human crew panel still open while combat/vehicle state is changing;
  - day `701701` logs show two `CommandAttack:Attack` executions against/around the selected human crew, then `CrewVehicleRemoved` and `CrewMemberKilled` deferred refreshes;
  - after that, vanilla `CommandHealValidator.AtSafehouseOrControlledBuilding` threw repeated `NullReferenceException` while `CrewCardContext.RefreshCommands` rebuilt buttons for stale crew `4295021995`.
- Updated stale selected-crew heal validation:
  - `CommandHealValidator.Validate` now has a prefix guard for human crews with missing crew assignment, peep entity, agent, node, player, or territory;
  - invalid stale crew returns the normal hidden Heal command status before vanilla validation runs;
  - expected diagnostic is `heal-validate-stale-crew-blocked pid=1 crew=<peep> reason=<invalid-crew|missing-agent|missing-node|missing-player-territory>`.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - passed with `0` warnings and `0` errors.
- Staged DLL:
  - `GameplayTweaks.dll` timestamp `2026-05-28 09:19:12`, size `2221056`.
- Live DLL status:
  - Steam live `GameplayTweaks.dll` file also showed `2026-05-28 09:19:12`, size `2221056` after staging;
  - current `Player.log` still only proves startup build `2026-05-28 09:05:08`, so the next in-game validation needs a restart/new startup marker before judging Phase 7F.

Implementation note - 2026-05-28, Phase 7G:

- User correction from the `09:10:53` video:
  - the main visible bug is not just stale selected-crew heal UI;
  - enemy AI can move to the corner on one turn but does not execute the queued attack until the next turn;
  - route-preview healing should remain allowed when the crew has enough movement points to reach the target node during the current turn.
- Cause found in `GameplayTweaks` turn-performance command queue limiting:
  - vanilla `CommandExecutor.ProcessCommandQueue` can process a completed move and then immediately process the next queued command;
  - the non-human limiter capped command queues at one completed command per turn, so a completed `CommandGoto` left the following `CommandAttack` queued for a later turn.
- Updated:
  - after a non-human `CommandGoto` completes, the limiter now allows the immediately queued `CommandAttack` to activate in the same command-queue pass;
  - the bypass applies only to `CommandGoto` followed by `CommandAttack`, and only when there is no still-active command;
  - normal non-human command queue budget/completed-command throttling stays in place for all other chains.
- Corrected the Phase 7E heal model:
  - `heal` is no longer classified as a physical-only route-sim action in `GameplayTweaks` or `AfterProhibitionRoutes`;
  - healing can again use `route-sim-expected` or `route-sim-goal` when the pending route says the vehicle can reach that node this turn;
  - combat, hostile, storage, module, shop-commit, buy-sell-commit, owned-building, safehouse, and explicitly physical action tags remain physical-only.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`;
  - both passed with `0` warnings and `0` errors.
- Staged DLLs:
  - `GameplayTweaks.dll` timestamp `2026-05-28 09:35:25`, size `2221568`;
  - `AfterProhibitionRoutes.dll` timestamp `2026-05-28 09:35:20`, size `49152`.
- Live DLL status:
  - Steam live `GameplayTweaks.dll` remained `2026-05-28 09:19:12`, size `2221056`;
  - Steam live `AfterProhibitionRoutes.dll` remained `2026-05-14 11:07:42`, size `49152`;
  - next validation needs the staged DLLs copied live before this phase can be judged.
- Expected next-run proof:
  - startup should show `GameplayTweaks.dll built=2026-05-28 09:35:25` and, if the routes DLL is loaded by the install, `AfterProhibitionRoutes.dll` should match the staged `09:35:20` build;
  - a non-human queue that completes `CommandGoto:GoTo` with `CommandAttack:Attack` next should no longer stop solely at `limiterOutcome=completed-limit` or `budget-before-activate`;
  - if the AI has enough movement to reach the corner, the attack should start on that same turn instead of waiting one more turn;
  - route-preview healing can again log `route-sim-access ... action=heal` and `route-sim-heal ... source=route-sim-expected` or `route-sim-goal` when the expected route node is reachable this turn;
  - route-preview healing should not log `route-sim-physical-only-blocked ... action=heal`.

Implementation note - 2026-05-28, Phase 7H:

- Live validation of Phase 7G startup:
  - Steam live loaded `GameplayTweaks.dll built=2026-05-28 09:35:25`;
  - Steam live `GameplayTweaks.dll` and `AfterProhibitionRoutes.dll` files matched the staged `09:35` builds.
- New live evidence:
  - healing at the current segment node behaved correctly with `route-sim-access ... action=heal source=route-sim-expected`;
  - longer trips incorrectly allowed final-goal healing while the current reachable segment was a different node, for example `route-sim-access ... node=NID_179 expectedNode=NID_535 finalGoal=NID_179 source=route-sim-goal action=heal`;
  - that allowed safehouse/owned-business healing when the player was merely headed there, even if the available movement points only reached the intermediate expected node.
- Updated route-sim heal reachability:
  - `heal` is now an expected-node-only route-sim action;
  - if the heal node is the pending route's `ExpectedNodeID`, healing can still happen before the vehicle physically settles there;
  - if the heal node is only the far `GoalNodeID` and differs from `ExpectedNodeID`, healing is blocked until a later turn/segment reaches that node;
  - `GameplayTweaks` logs `route-sim-goal-blocked ... action=heal reason=expected-node-only`;
  - `AfterProhibitionRoutes` reports the same block as `decision=block reason=expected-node-only` when its route-sim bridge owns the decision.
- Validation:
  - `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release`;
  - `dotnet build AfterProhibitionRoutes\AfterProhibitionRoutes.csproj -c Release`;
  - both passed with `0` warnings and `0` errors.
- Staged DLLs:
  - `GameplayTweaks.dll` timestamp `2026-05-28 09:59:01`, size `2222592`;
  - `AfterProhibitionRoutes.dll` timestamp `2026-05-28 09:58:57`, size `49152`.
- Live DLL status:
  - Steam live `GameplayTweaks.dll` also showed `2026-05-28 09:59:01`, size `2222592`;
  - Steam live `AfterProhibitionRoutes.dll` also showed `2026-05-28 09:58:57`, size `49152`;
  - current startup log already showed `GameplayTweaks.dll built=2026-05-28 09:59:01`, so Phase 7H is live and needs runtime heal repro validation.
- Expected next-run proof:
  - startup should show `GameplayTweaks.dll built=2026-05-28 09:59:01`;
  - long safehouse/owned-business trips where `expectedNode != finalGoal` should log `route-sim-goal-blocked ... action=heal reason=expected-node-only` and should not log `route-sim-heal ... source=route-sim-goal`;
  - same-turn reachable heal nodes where `expectedNode == heal node` may still log `route-sim-heal ... source=route-sim-expected`;
  - physical arrival should still allow normal `heal-vehicle-group ... source=<node>:physical`.

## Suggested Next Implementation Slice

The next implementation slice should validate physical-only healing, stale selected-crew UI refresh safety, and the new strict contact source diagnostics before adding real AI movement:

1. Confirm the popup appears only after same-node contact and a passed rarity roll.
2. Test all three outcomes: pay, favor, refuse.
3. Confirm active robbery response popups block unrelated runtime attack dispatch until the player chooses an outcome.
4. Confirm low-heat human-directed delayed revenge logs `hitCrew=False` and does not dispatch a crew attack.
5. Confirm nearby would-attempt candidates show `warning phase=approach-diagnostic` once and keep `result=no-ai-movement-yet`.
6. Confirm warned approaches that expire or clear log `warning-cleared phase=approach-diagnostic` and a matching `warningClosed=True`.
7. Confirm timeout closure is suppressed when the same expired approach is still active and immediately reaches same-node contact.
8. Confirm route-preview healing is allowed only when the heal node is the current turn's expected/reachable route node, not just the far final goal.
9. Confirm AI `CommandGoto -> CommandAttack` chains execute the attack on the same turn when movement was enough to reach the corner.
10. Confirm attacks that kill or remove the selected human crew no longer produce `CommandHealValidator.AtSafehouseOrControlledBuilding` `NullReferenceException` during deferred crew-card command refresh.
