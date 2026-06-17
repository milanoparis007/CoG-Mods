# Gang War Weapon Stance Phased Prompt

Use this prompt to add heat-driven weapon limits and gradual escalation to gang wars in `C:\Users\User\source\repos\ClassLibrary1`.

## Goal

Make the beginning of a gang war feel like a street conflict instead of an immediate late-game shootout.

At low war heat, attackers and defenders should usually fight with fists or limited street weapons even when their vehicles contain deadly firearms. The weapon stance belongs to the war between two outfits, not to one combat encounter and not to one vehicle.

For wars involving the player:

- choose the stance from current war heat for every combat
- keep every owned weapon selectable in combat
- clearly warn when the selected weapon exceeds the current heat stance
- apply penalties only if the prohibited weapon is actually used

For AI-versus-AI wars:

- choose the stance from current war heat for every combat
- use the same thresholds, weapon selection, and violation rules

This replaces the immediate black-market implementation priority. Do not change AI weapon supply, monthly weapon drops, weapon shops, or weapon unlocks during this feature.

## Core Design

Do not detect the beginning or end of a war and do not store a separate weapon-stance state.

At the start of every real combat:

1. read the existing directional war heat for both outfit directions
2. use the higher value as effective pair heat
3. map that value to the current stance
4. use that stance for both attacker and defender weapon selection

Because new conflicts normally begin with low heat, their early attacks naturally begin Hands Only. If a pair already has high heat from murder, retaliation, robbery, or another hostile event, its next combat should immediately use the higher stance.

Initial stances:

| Stance | Allowed weapons |
| --- | --- |
| Hands Only | fists and the default unarmed weapon |
| Street Weapons | fists, tools, clubs, knives, and other approved melee weapons |
| Sidearms | street weapons plus approved pistols and revolvers |
| Open Arsenal | every valid weapon in the vehicle |

Current war heat determines the stance for every combat. Initial thresholds:

| Effective war heat | Stance |
| --- | --- |
| `0-24` | Hands Only |
| `25-49` | Street Weapons |
| `50-74` | Sidearms |
| `75-100` | Open Arsenal |

Keep thresholds configurable. Use the higher of the two directional war-heat values as the effective pair heat.

Do not classify weapons by damage alone. Use `WeaponConfig.firearm` plus an explicit weapon-category table for custom and ambiguous weapons.

## Escalation Rules

- The stance applies equally to attackers and defenders.
- A declared or active war does not create UI or change inventories.
- No war-start, war-end, or first-combat detection is required.
- Resolve the stance from current heat immediately before weapon selection.
- A canceled player combat plan creates no state and no penalty.
- Vehicle inventory determines which weapons are available, but it does not automatically determine what is allowed.
- Weapon quantity and armed-crew coverage may influence later AI violation chance, but not the heat tier.
- Count usable weapons across living assigned crew vehicles.
- Track the percentage of crew vehicles with melee weapons, sidearms, and long guns.
- Do not let one stockpiled vehicle or duplicate weapon stack dominate the outfit's stance calculation.
- A prohibited-weapon violation adds heat before the next combat stance is calculated.
- Normal heat decay may lower the stance for later combats automatically.
- No stance reset is required when a war ends.

## Enemy Sit-Down Cooling

Add a dedicated **Enemy Sit-Down** system for hostile outfits.

The primary player entry point should be an **Outfit Sit-Downs** section in the boss's Crew Relations tab. This is an outfit-level diplomatic action, so show it only while the player's boss is selected.

The section should list every living, met outfit that:

- is not the human outfit
- is not defeated
- has a valid boss or outfit identity
- has current war heat, direct aggro, or another qualifying hostile relationship
- is not protected by a truce or alliance that already forbids hostility

The player can select an outfit and propose a remote sit-down without traveling to its boss or sharing a map node. Do not require physical contact, a meeting route, or an available enemy crew vehicle.

Do not expose the full friendly gang conversation or trade catalog during aggro. The current top-level leisure and liquor trade buttons use `check-is-convo-aggro expected #false`; keep those routes unchanged.

Initial sit-down options:

| Option | Cost | Initial heat reduction |
| --- | ---: | ---: |
| Talk it down | $0 | `-5` |
| Leisure sit-down | $1,000 | `-10` |
| Liquor business deal | $3,000 | `-20` |

The `$1,000` leisure action is based on `gang-leisure`.

The `$3,000` business deal is based on `gang-trade-liquor-lowb2` in:

- `Things To Have\Current After Prohibition Mod\Public\Days Of Prohibition Beta v0.2 Build 2026-06-09\After Prohibition Mod\CoG_Data\StreamingAssets\UI\ConvoGangs.sim`
- lines `1095-1113`

That trade already:

- consumes one conversation action
- removes `$3,000` from crew cash
- grants liquor inventory
- grants XP and street credit
- applies gang trade relationship buffs
- applies local trade heat

Expose the liquor deal only as a player-initiated cooling option. Reproduce the existing completed transaction by debiting the player's crew cash and granting the established liquor bundle to the player's safehouse before reducing heat.

Do not let AI outfits offer or execute this liquor deal against the player. AI outreach is limited to Talk and Leisure.

Cooling rules:

- Reduce both directional war-heat records by the same amount.
- Clamp each direction at zero.
- Recalculate the weapon stance naturally on the next combat.
- Do not directly store or force a lower stance.
- Limit successful cooling to once per outfit pair every `14` days initially.
- Consume one conversation action for every successful option.
- Remote sit-downs do not require physical presence.
- Plain talk should not grant goods, XP, street credit, or relationship buffs.
- Plain talk should be the weakest option and may be refused at high heat.
- Leisure should apply its existing relationship and progression rewards only after payment succeeds.
- The player pays `$1,000` when initiating Leisure.
- An AI outfit pays `$1,000` from its own available clean treasury when it initiates Leisure.
- The player-initiated liquor deal must grant its goods successfully before cooling heat.
- The liquor deal is not part of AI-initiated offers.
- Canceled, unavailable, or failed actions must not reduce heat.
- Robbery, threats, demands, and forced payments must never count as cooling trades.

Suggested acceptance rules:

- Talk it down is normally available below `50` effective heat.
- Leisure is normally available below `75` effective heat.
- The player may propose the `$3,000` liquor deal at any heat below `100`, but severe hostility can require a relationship, street-credit, or no-recent-kill check.
- A boss murder, crew killing, or stance violation within the last `7` days can block plain talk while leaving paid restitution-style options available.

The initial values intentionally let successful actions cross stance thresholds:

- heat `28` plus plain talk becomes `23`, lowering Street Weapons to Hands Only
- heat `58` plus leisure becomes `48`, lowering Sidearms to Street Weapons
- heat `82` plus the player-funded `$3,000` deal becomes `62`, lowering Open Arsenal to Sidearms

Do not guarantee a tier reduction when heat is far above the next threshold. The player is buying cooling pressure, not selecting a stance directly.

### Player-Initiated Sit-Downs

The boss Crew Relations tab should provide:

- an outfit selector
- current effective war heat
- current weapon stance
- opponent power compared with player power
- sit-down cooldown
- Talk, Leisure, and Liquor Deal action buttons
- previewed heat and stance after success

Use `CalculateGangPower` for both outfits.

An enemy outfit may accept or decline the player's proposal. Initial acceptance model:

- start at `50`
- peaceful personality: `+20`
- isolationist or cautious posture: `+10`
- aggressive, bold, cruel, or vindictive posture: `-15`
- player power at least `25%` above enemy power: `+20`
- enemy power at least `25%` above player power: `-20`
- Leisure offer: `+15`
- effective heat `50-74`: `-10`
- effective heat `75-100`: `-25`
- recent boss or crew killing: `-30` or block
- recent rejected sit-down: block through cooldown

Clamp acceptance to `5-95` and use one deterministic roll per proposal. Do not reroll when reopening the panel.

A stronger enemy is therefore more willing to decline a free Talk proposal. A weaker or cautious enemy is more likely to accept because it is wary of escalation.

On decline:

- consume the player's conversation action
- do not charge Leisure cash
- do not reduce heat
- start a shorter failed-proposal cooldown
- do not automatically add heat in the first pass

### AI-Initiated Sit-Down Offers

Enemy AI outfits may remotely offer the player a sit-down.

First-release AI offers:

- Talk
- Leisure

Do not let AI offer the liquor deal to the player. This restriction does not remove the player-initiated liquor option.

AI offer eligibility should consider:

- current effective war heat
- no active truce
- no pending sit-down offer for that pair
- pair cooldown
- no severe killing or stance violation within the configured recent-event block
- AI treasury for Leisure
- AI personality
- AI power compared with player power
- recent crew losses

Offer postures:

- **Peaceful:** the outfit generally wants the violence to cool down because of peaceful or isolationist personality and no immediate severe grievance.
- **Wary:** the outfit wants to limit escalation because the player is stronger, the outfit has suffered losses, or the current stance is becoming dangerous.

Do not open an unsolicited popup when the AI decides it wants a sit-down. Store a pending offer and expose it only in the boss's **Outfit Sit-Downs** area of Crew Relations.

The relevant outfit row must replace or supplement the normal proposal action with a clear offer state:

- `Sit-down offered: Peaceful Talk`
- `Sit-down offered: Wary Leisure`

The Outfit Sit-Downs control may show a small pending-offer count so the player can discover new offers without a modal interruption. Selecting the outfit must show:

- the Peaceful or Wary posture
- whether the offer is Talk or Leisure
- that the enemy pays for an AI-funded Leisure offer
- Accept and Decline actions

If there is no pending AI offer, keep the normal player-initiated Talk, Leisure, and Liquor Deal proposal options.

Do not expose raw acceptance scores or hidden personality IDs.

Initial AI offer selection:

- Peaceful outfits prefer Talk.
- Wary outfits prefer Leisure when they can afford it.
- An outfit at least `25%` weaker than the player gains a strong offer-chance bonus.
- An outfit at least `25%` stronger than the player receives a strong offer-chance penalty unless it is peaceful.
- Aggressive outfits should rarely initiate Talk and should not offer merely because a cooldown expired.

When the AI offers Leisure:

- restrict this offer to valid enemy `GangPlayer` outfits; do not treat goons, cops, or other AI player types as eligible outfits
- require a valid, non-vanquished AI safehouse
- require at least `$4,200` clean cash in that specific safehouse at offer creation
- do not debit the AI outfit until the player accepts
- revalidate the safehouse, vanquished state, and `$4,200` safehouse balance when the player accepts
- if revalidation fails, expire the offer without reducing heat
- debit the `$1,000` Leisure cost from that safehouse, leaving at least `$3,200`
- after successful payment, apply the existing leisure relationship effect
- reduce bilateral war heat by `10`
- do not send resources

When the player declines an AI offer:

- do not reduce heat
- do not debit either side
- start the failed-offer cooldown
- record the decline for later AI decision-making
- do not force immediate retaliation in the first pass

Pending AI offers must use a passive Crew Relations lifecycle:

- create or update the offer during simulation without opening UI
- allow at most one pending offer per outfit pair
- refresh the Outfit Sit-Downs count and outfit row the next time Crew Relations refreshes
- accept or decline only from the boss's Outfit Sit-Downs area
- expire cleanly if either outfit is defeated, a truce begins, or heat reaches zero
- expire after a configured offer lifetime
- clear the pending record on accept or decline
- persist enough data for the same offer to remain visible after save and load
- never acquire an active-popup or input lock

## Player Freedom And Violations

Never remove or disable the player's prohibited weapons in the combat popup.

Instead:

1. mark choices that exceed the current stance
2. warn the player before the combat transaction commits
3. allow the player to continue
4. apply the violation only after combat actually uses the weapon

Canceling the combat popup must not count as a violation.

A player violation should:

- add directional war heat
- apply a relationship penalty from the opposing outfit
- mark that the player exceeded the current stance
- allow the added heat to determine the next combat's stance
- improve the opponent's retaliation or escalation willingness
- create one ticker or grapevine report

Apply the stance-violation penalty once per committed combat transaction. Grouped combat must not multiply the penalty once per shooter. Existing attack and kill heat can still apply normally.

If the player is defending against an AI-initiated attack, automatically choose the player's best stance-compliant defensive weapon. Do not make the player breach an agreement through an automatic defense they could not control.

## AI Compliance And Violations

In the first behavioral release, AI outfits should always honor the current heat stance.

Add AI violation chances only after compliant selection is stable. Later violation pressure may consider:

- current war heat
- recent crew deaths
- boss death
- aggressive or bold personality
- relationship hostility
- repeated violations by the opponent
- firearm coverage across living crew vehicles
- whether the outfit is badly outnumbered

Peaceful and isolationist outfits should be more likely to honor the stance. Aggressive and expansionist outfits may become more willing to break it in a later phase.

An AI violation must receive the same heat, relationship, escalation, and notification consequences as a player violation.

## Ownership

Implement this in `GameplayTweaks`.

Relevant ownership already exists there:

- directional gang war heat
- hostile-event registration
- retaliation scoring
- AI personality helpers
- grouped vehicle combat
- player combat popup weapon lists
- combat-result postfix processing
- save data and migration
- boss-only Crew Relations controls
- Crew Relations refresh and pending-offer display state
- `CalculateGangPower`

Do not create a separate DLL for the first implementation. A separate assembly would need to coordinate patch order and shared combat state with the existing `GameplayTweaks` combat patches.

## Phase Progress

### Phase 0 Runtime Anchor Verification - 2026-06-14

Status: complete enough to start Phase 1 diagnostics.

Verified live assembly path:

- `C:\Program Files (x86)\Steam\steamapps\common\City of Gangsters\CoG_Data\Managed\Assembly-CSharp.dll`

Verified vanilla combat signatures:

- `CombatManager.PerformHumanCombat(Entity attacker, Entity target, WeaponConfig attackWeapon)` pays the human attack cost, chooses the target's vanilla best defensive weapon, then calls `PerformCombat`.
- `CombatManager.PerformAICombat(CrewAssignment attacker, CrewAssignment target)` chooses both sides' vanilla best weapons, calls `PerformCombat`, then shows/tickers results when the target is human.
- `CombatManager.PerformCombat(CrewAssignment crew1, CrewAssignment crew2, WeaponConfig weapon1, WeaponConfig weapon2)` is the final shared vanilla commit point for damage, deaths, violence heat, social attack handling, vehicle `ProcessAttack`, VFX, and `GangWarAction`.
- `CombatPopupPlanning.OnFight()` loops the popup rows, calls private `DoCombat`, then removes the popup and shows results.
- `CombatPopupPlanning.RefreshContents()` rebuilds human/enemy cards and refreshes the fight button.

Verified current `GameplayTweaks` owners:

- `VehicleGroupCombatPatch` already hooks `CombatPopupPlanning.OnFight`, `CombatPopupPlanning.RefreshContents`, `CombatPopupPlanning.OnCancel`, `CombatPopupPlanning.ReleaseOnPop`, `CombatPopupPlanning.OnTargetRowSelection`, `CombatPopupPlanning.OnArrow`, `CombatManager.PerformHumanCombat(Entity, Entity, WeaponConfig)`, and `CombatManager.PerformAICombat(CrewAssignment, CrewAssignment)`.
- `PactOpsCombatPatch` already postfixes `PerformCombat` and `PerformHumanCombat` for gang-war heat/retaliation processing.
- `RealCombatGrapevinePatch` already postfixes `PerformCombat(CrewAssignment, CrewAssignment, WeaponConfig, WeaponConfig)`.
- `VehicleGroupCombatPatch.DispatchCombatObservers` already publishes grouped-combat results through `CombatObserverBridge`.

Verified grouped-combat boundary:

- Popup commits are deduped by `PopupCommitTokens`.
- Grouped exchanges use `_groupedCombatTransactionSerial`, `_groupedCombatTransactionDepth`, and `_groupedCombatTransactionSource`.
- `ExecuteVehicleGroupCombat` and `ExecuteOnFootSpreadCombat` call `BeginGroupedCombatTransaction` once per grouped transaction and dispatch observers for each produced result.
- Later stance-violation consequences should dedupe by the grouped transaction source, not by each result row.

Verified war-heat read path:

- Existing heat lives in `SaveData.PactWarHeat` and `SaveData.IndependentWarHeat`.
- `GetWarHeat(channel, attackerPid, defenderPid)` clamps directional records to `0-100`.
- `GetWarHeat(attackerPid, defenderPid)` resolves one channel for the pair.
- Existing mediation code has `GetHighestWarHeatForPair(firstPid, secondPid)`, which checks both directions and both channels. Phase 1 can mirror this logic without needing war-start or war-end detection.

Phase 1 implementation target:

- Add diagnostics-only stance resolution in `GameplayTweaks`.
- Extend existing combat owners instead of adding competing patches.
- Log combat pair IDs, directional/effective heat, stance, human/AI roles, selected weapons, and arsenal coverage.
- Use `CombatObserverBridge`/existing grouped transaction context for grouped combat diagnostics.
- Do not change weapon selection, inventories, monthly drops, gun shops, unlocks, popup behavior, attack costs, heat, or relationships in Phase 1.

### Phase 1 Diagnostics First Pass - 2026-06-14

Status: implemented and build-validated.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`
- `GameplayTweaks\GameplayTweaksPlugin.cs`

Implemented diagnostics-only behavior:

- Added stateless stance resolution from current bilateral war heat.
- Reads Pact and Independent heat in both directions and uses the highest value as effective heat.
- Maps heat to `HandsOnly`, `StreetWeapons`, `Sidearms`, or `OpenArsenal`.
- Logs selected attacker/defender weapons after combat resolves.
- Logs whether the actually selected weapons would comply with the current stance.
- Logs crew count, active vehicle count, and vehicle coverage for melee, sidearms, long guns, and automatic weapons.
- Logs direct aggro state when the AI combat advisor exposes an aggro query; otherwise reports `unknown`.
- Logs vanilla combat through the existing `PerformCombat` postfix.
- Logs grouped combat through the existing grouped-combat observer dispatch and includes the grouped transaction source when available.

Markers added:

- `[WarStance][HeatTier]`
- `[WarStance][Arsenal]`
- `[WarStance][Combat]`

Scope and safety:

- No weapon selection changes.
- No inventory changes.
- No popup changes.
- No heat, relationship, cost, retaliation, monthly-drop, weapon-shop, or unlock changes.
- The first-pass category logic is diagnostic only. Phase 2 should still create the final centralized classifier before any enforcement uses categories.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live log evidence:

- `Player.log` showed `[WarStance][HeatTier]`, `[WarStance][Arsenal]`, and `[WarStance][Combat]`.
- AI-versus-AI combat logged correctly, including `attackerHuman=False defenderHuman=False`.
- AI-versus-player grouped combat logged correctly through `PerformAICombat:txn=...`, including `attackerHuman=False defenderHuman=True grouped=True`.
- Low-heat fights resolved `HandsOnly` without war-start detection.
- High-heat AI-versus-player combat resolved `OpenArsenal` at effective heat `82.0` and `100.0`.
- The diagnostics exposed classifier issues before enforcement: `weapon-crowbar` was misclassified as `Automatic`, and `weapon-colt`/`weapon-lemat` were too broad as long-gun-like firearms.

### Phase 2 Classifier First Pass - 2026-06-14

Status: implemented and build-validated.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented diagnostics-only classifier improvements:

- Added explicit category overrides for known vanilla/After Prohibition weapon IDs.
- `weapon-fists` resolves as `Unarmed`.
- `weapon-bat`, `weapon-billy-club`, `weapon-crowbar`, `weapon-knife`, and `weapon-switchblade` resolve as `Melee`.
- `weapon-bearcat`, `weapon-beretta`, `weapon-broomhandle`, `weapon-colt`, `weapon-iver-johnson`, `weapon-lemat`, and `weapon-pistol` resolve as `Sidearm`.
- `weapon-1912-winchester`, `weapon-1934-winchester`, `weapon-enfield`, `weapon-remington`, and `weapon-winchester` resolve as `LongGun`.
- `weapon-beretta-m1918`, `weapon-browning-1918`, `weapon-emp-35`, `weapon-mp18`, `weapon-mp-28`, and `weapon-thompson` resolve as `Automatic`.
- Unknown firearms now remain `Unknown` instead of silently defaulting into an allowed lower tier.
- Unknown weapon IDs log once with `[WarStance][UnknownWeapon]`.

Scope and safety:

- Still diagnostics-only.
- No weapon selection changes.
- No inventory, heat, relationship, attack-cost, popup, AI-drop, shop, or unlock changes.
- This pass is meant to make logs reliable before AI compliance starts.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- `weapon-crowbar` should report `attackerCategory=Melee` or `defenderCategory=Melee`.
- `weapon-colt`, `weapon-lemat`, and `weapon-pistol` should report `Sidearm`.
- `weapon-thompson`, `weapon-mp18`, `weapon-mp-28`, `weapon-emp-35`, `weapon-beretta-m1918`, and `weapon-browning-1918` should report `Automatic`.
- Any unexpected weapon should produce one `[WarStance][UnknownWeapon]` line.

Live log follow-up:

- The live plugin was updated to the 2026-06-14 `6:00 PM` classifier build.
- `Player.log` confirmed `weapon-colt` as `Sidearm`.
- `Player.log` confirmed `weapon-crowbar` as `Melee`.
- `Player-prev.log` still showed the old bad categories, which confirms the current run is using the corrected classifier.

### Phase 3 Shadow Selection First Pass - 2026-06-14

Status: implemented and build-validated.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented diagnostics-only shadow selection:

- Added a combat-time stance-compliant weapon resolver that inspects the combatant's current vehicle inventory.
- Uses vanilla sorted weapon order and picks the best weapon that complies with the resolved heat stance.
- Falls back to fists when no vehicle weapon is allowed or available.
- Logs the actual vanilla weapon beside the weapon the stance system would select.
- Logs noncompliant actual weapon use without applying penalties or changing combat results.

Markers added:

- `[WarStance][WouldSelect]`
- `[WarStance][WouldViolate]`

Scope and safety:

- Still diagnostics-only.
- No weapon selection changes.
- No inventory, heat, relationship, attack-cost, popup, AI-drop, shop, or unlock changes.
- This pass is the last dry run before deciding whether to redirect AI attacker/defender weapon choice through the compliant selector.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- Low-heat `HandsOnly` combats with firearms present should log actual firearm use plus `attackerWould=weapon-fists` or `defenderWould=weapon-fists`.
- `StreetWeapons` should shadow-select melee weapons when available, otherwise fists.
- `Sidearms` should shadow-select melee or sidearm weapons, not long guns or automatics.
- `OpenArsenal` should shadow-select the same best weapon vanilla would normally prefer.
- `[WarStance][WouldViolate]` should appear only when the actual vanilla weapon exceeds the current heat stance.

Live log follow-up:

- The live plugin was updated to the 2026-06-14 `6:09 PM` shadow-selection build.
- `Player.log` confirmed `[WarStance][WouldSelect]` and `[WarStance][WouldViolate]`.
- Low-heat `HandsOnly` AI-versus-AI combat with firearms present still used vanilla firearms, while `WouldSelect` resolved fists for both sides.
- AI-versus-player grouped combat remained unchanged and continued to log shadow selections only.

### Phase 4 AI Compliance First Pass - 2026-06-14

Status: implemented and build-validated for one-on-one AI-versus-AI combat.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented:

- Added a narrow AI-only compliance branch inside the existing `CombatManager.PerformAICombat` prefix.
- Applies only when both attacker and defender are AI and the existing grouped-combat branch is not being used.
- Resolves current effective pair heat immediately before combat.
- Chooses the best stance-compliant attacker and defender weapons from their current vehicles.
- Falls back to fists/default weapon when no stronger allowed weapon exists.
- Calls vanilla `PerformCombat` with the compliant weapons so existing damage, heat, social, death, and grapevine flows still run.
- Leaves player-involved combat unchanged.

Markers added:

- `[WarStance][AIComply]`
- `[WarStance][AIComplyFallback]`

Scope and safety:

- No inventory changes.
- No player popup changes.
- No player warning or violation penalties yet.
- No AI violation chance yet.
- Grouped AI combat remains shadow-only for this first pass so weapon-pool distribution can be handled separately.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `6:16 PM`, size `2737664`.
- Live DLL at the time of this note: 2026-06-14 `6:09 PM`, size `2735616`.
- Copy the `6:16 PM` build live before testing Phase 4 markers.

Next test signals:

- Low-heat one-on-one AI-versus-AI fights should log `[WarStance][AIComply]` and then `[WarStance][Combat]` with `attackerComplies=True` and `defenderComplies=True`.
- Low-heat one-on-one AI-versus-AI fights should use `weapon-fists` even when sidearms are in the vehicles.
- `WouldSelect` should match the actual weapons after AI compliance for one-on-one AI-versus-AI combat.
- AI-versus-player and player-versus-AI combat should remain unchanged for this pass.
- Grouped AI combat may still show `WouldViolate`; that is expected until the grouped weapon-pool pass.

Live log follow-up:

- The live plugin was updated to the 2026-06-14 `6:16 PM` AI compliance build.
- `Player.log` confirmed `[WarStance][AIComply]` for one-on-one AI-versus-AI combat.
- Low-heat one-on-one AI-versus-AI fights now used `weapon-fists` for attacker and defender.
- Follow-up `[WarStance][Combat]` lines showed `attackerComplies=True` and `defenderComplies=True`.
- Player-involved combat remained unchanged; low-heat AI-versus-player attacks can still show `WouldViolate` until the later player-defense phase.

### Phase 4 Grouped AI Compliance Pass - 2026-06-14

Status: implemented and build-validated for AI-versus-AI grouped combat.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented:

- Added `[WarStance][AIGroupComply]` when AI-only grouped combat resolves a heat stance.
- Filters AI-only grouped attacker weapon pools by the current stance.
- Forces AI-only drive-by combat down to on-foot combat when the stance does not allow ranged weapons.
- Keeps Sidearms and Open Arsenal eligible for drive-by, but only with stance-compliant weapons.
- Uses stance-compliant defender weapons for AI-only grouped exchanges.
- Falls back to fists/default weapon through the existing grouped assignment fallback.

Scope and safety:

- AI-versus-player grouped combat remains unchanged.
- Player-versus-AI grouped combat remains unchanged.
- No inventory, unlock, shop, monthly-drop, popup, heat, or relationship changes.
- AI violation chances remain deferred.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `6:28 PM`, size `2739200`.
- Live DLL at the time of this note: 2026-06-14 `6:16 PM`, size `2737664`.
- Copy the `6:28 PM` build live before testing grouped AI compliance.

Next test signals:

- AI-only grouped combat should log `[WarStance][AIGroupComply]`.
- Low-heat AI-only grouped combat should report `mode=onfoot`.
- Low-heat AI-only grouped combat should use fists and show `attackerComplies=True` and `defenderComplies=True`.
- Sidearms/Open Arsenal AI-only grouped combat may remain drive-by if compliant ranged weapons are available.
- Player-involved grouped combat should continue to behave like the prior build.

Live log follow-up:

- The live plugin was updated to the 2026-06-14 `6:28 PM` grouped AI compliance build.
- `Player.log` confirmed `[WarStance][AIGroupComply]`.
- Low-heat AI-only grouped combat reported `mode=onfoot`.
- Follow-up grouped combat used `weapon-fists` and showed `attackerComplies=True` and `defenderComplies=True`.
- Player-involved incoming attacks still showed low-heat `WouldViolate`, as expected before the player-defense pass.

### Phase 5 Automatic Player Defense First Pass - 2026-06-14

Status: implemented and build-validated for single AI attacks against the player.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented:

- Added `[WarStance][PlayerDefenseComply]` for single AI-versus-player `PerformAICombat`.
- Applies only when the attacker is an AI outfit and the target is the human player.
- Excludes cops and feds.
- Resolves current effective pair heat immediately before combat.
- Chooses the AI attacker's best stance-compliant weapon.
- Chooses the player's best stance-compliant automatic defensive weapon.
- Calls vanilla `PerformCombat` and then shows the normal incoming-combat result ticker.
- Does not record a player violation because the player did not choose the defensive weapon.

Markers added:

- `[WarStance][PlayerDefenseComply]`
- `[WarStance][PlayerDefenseFallback]`

Scope and safety:

- Player-initiated attacks remain unchanged.
- Player warning/confirmation remains unimplemented.
- Grouped AI-versus-player combat remains unchanged for this pass.
- No inventory, unlock, shop, monthly-drop, popup, heat, relationship, or violation-penalty changes.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `6:42 PM`, size `2741248`.
- Live DLL at the time of this note: 2026-06-14 `6:28 PM`, size `2739200`.
- Copy the `6:42 PM` build live before testing automatic player defense compliance.

Next test signals:

- Single AI attacks against the player should log `[WarStance][PlayerDefenseComply]`.
- Low-heat single AI attacks against the player should use `weapon-fists` for the AI attacker and player defender.
- Follow-up `[WarStance][Combat]` should show `attackerComplies=True` and `defenderComplies=True`.
- Cops/feds should not route through `[WarStance][PlayerDefenseComply]`.
- Player-initiated attacks should still behave like the prior build.
- Grouped incoming AI attacks against the player may still show `WouldViolate`; that is expected until the grouped player-defense pass.

Live log follow-up:

- The live plugin was updated to the 2026-06-14 `6:42 PM` automatic player-defense build.
- `Player.log` confirmed `[WarStance][PlayerDefenseComply]`.
- Low-heat single AI attacks against the player used `weapon-fists` for attacker and defender.
- Follow-up combat lines showed `attackerComplies=True` and `defenderComplies=True`.
- Grouped incoming AI attacks still used prohibited weapons at low heat, confirming the next gap.

### Phase 5 Grouped Automatic Player Defense Pass - 2026-06-14

Status: implemented and build-validated for grouped AI attacks against the player.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented:

- Added `[WarStance][PlayerGroupDefenseComply]` for grouped AI-versus-player combat.
- Applies only when an AI outfit attacks the human player through the grouped combat executor.
- Excludes cops and feds.
- Filters grouped AI attacker weapon assignments by the current heat stance.
- Uses stance-compliant automatic defensive weapons for the player's targeted defenders.
- Downgrades incoming drive-by combat to on-foot when Hands Only or Street Weapons does not allow ranged weapons.
- Retains Sidearms/Open Arsenal drive-by behavior when compliant ranged weapons are available.
- Generalized the grouped assignment helper's optional stance parameter so the same selection path supports AI-only and player-defense compliance.

Scope and safety:

- Player-initiated combat remains unchanged.
- No player warning, confirmation, or violation penalties yet.
- No inventory, unlock, shop, monthly-drop, heat, or relationship changes.
- No player violation is recorded for automatic defense.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `7:16 PM`, size `2742272`.
- Live DLL at the time of this note: 2026-06-14 `6:42 PM`, size `2741248`.
- Copy the `7:16 PM` build live before testing grouped automatic player defense.

Next test signals:

- Grouped incoming AI attacks should log `[WarStance][PlayerGroupDefenseComply]`.
- Low-heat grouped incoming attacks should report `mode=onfoot`.
- Low-heat grouped incoming combat should use fists and show both sides compliant.
- Street Weapons grouped incoming combat should use melee weapons or fists.
- Sidearms/Open Arsenal may remain drive-by with compliant ranged weapons.
- Cops/feds should not route through `[WarStance][PlayerGroupDefenseComply]`.
- Player-initiated attacks should remain unchanged.

Live log follow-up:

- The live plugin was updated to the 2026-06-14 `7:16 PM` grouped automatic player-defense build.
- `Player.log` confirmed `[WarStance][PlayerGroupDefenseComply]` for grouped AI attacks against the player.
- Hands Only grouped incoming combat was downgraded to on-foot and used `weapon-fists` for both sides.
- Open Arsenal grouped incoming combat retained firearms and reported both sides compliant.
- Single incoming attacks continued to report `[WarStance][PlayerDefenseComply]`.
- Player-initiated MP18 attacks still reported `[WarStance][WouldViolate]` under Hands Only and Street Weapons, confirming the next player-popup gap.

### Phase 6 Player Popup Stance Warning Pass - 2026-06-14

Status: implemented and build-validated for live UI testing.

Code touched:

- `GameplayTweaks\GameplayTweaksPlugin.Combat.cs`

Implemented:

- Adds a compact combat-popup summary showing the current war stance and effective heat.
- Refreshes the summary when the popup rebuilds or the player changes target, weapon, or grouped-combat selection.
- Shows a clear warning when one or more selected player weapons exceed the stance.
- Supports different target outfits in one popup by reporting `Mixed` when their resolved stances differ.
- Excludes cops and feds from the gang-war stance warning.
- Keeps every player weapon visible and selectable.
- Logs `[WarStance][PlayerPopup]` when the visible assessment changes.
- Logs `[WarStance][PlayerWarning]` when Fight is pressed, including whether the selections violate the stance.

Scope and safety:

- This is the warning shadow pass only.
- Fight is not blocked and confirmation is not required yet.
- No heat or relationship penalties are applied.
- Canceling the popup changes no war-stance state.
- Automatic player defense and AI compliance behavior are unchanged.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `7:34 PM`, size `2746368`.
- Live DLL at the time of this note: 2026-06-14 `7:16 PM`, size `2742272`.
- Copy the `7:34 PM` build live before testing the player popup warning.

Next test signals:

- Opening a player attack should show `War stance`, the current heat, and whether selected weapons comply.
- Selecting fists under Hands Only should remove the warning.
- Selecting an MP18 under Hands Only or Street Weapons should show the warning without removing the MP18 option.
- Changing the target should immediately update the stance summary.
- The log should show `[WarStance][PlayerPopup]` for selection changes.
- Pressing Fight should show one `[WarStance][PlayerWarning]` with `warning=true` for a prohibited selection and `penaltiesApplied=false`.
- Canceling should not produce a commit warning or change heat.

Live UI follow-up:

- The live `7:34 PM` DLL matched the expected player-popup build.
- `Player.log` showed `[WarStance][PlayerPopup]` resolving Open Arsenal at heat `94`.
- The assessment refreshed from one to two selected attackers without exceptions.
- The stance text rendered too wide in its lower-center placement.

### Phase 6 Popup Layout Fit Pass - 2026-06-14

Status: implemented and build-validated for live UI testing.

Implemented:

- Moved the stance summary to the upper-right of the combat popup.
- Offset the summary from the close button.
- Changed the display to two shorter right-aligned lines.
- Shortened compliance text to `Weapons within stance` or `<count> weapons over stance`.
- Enabled best-fit text sizing with a bounded minimum font size.
- Kept all warning, selection, combat, and penalty behavior unchanged.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `7:44 PM`, size `2746368`.
- Live DLL at the time of this note: 2026-06-14 `7:34 PM`, size `2746368`.
- Copy the `7:44 PM` build live before checking the revised placement.

Next test signals:

- The summary should fit in the upper-right without touching the close button or popup title.
- Open Arsenal should display as `War stance: Open Arsenal` on the first line.
- The second line should fit `Heat 94 | Weapons within stance`.
- A prohibited selection should switch the second line to the shorter orange warning.

Live UI follow-up:

- The live `7:44 PM` DLL matched the expected layout build.
- The upper-right placement and shortened wording fit correctly.
- `Player.log` showed Hands Only at heat `0` flagging `weapon-lemat` as one violation.
- Changing to a compliant weapon immediately changed the same popup assessment to zero violations.
- No combat-popup or war-stance exceptions appeared.

### Phase 6 Player Violation Confirmation Pass - 2026-06-14

Status: implemented and build-validated for live testing.

Implemented:

- The first Fight click with a prohibited weapon now pauses the attack.
- The upper-right summary changes to `Stance violation selected` and `Click Fight again to proceed`.
- A second Fight click with the same selections proceeds with the attack.
- Changing a target or weapon clears the pending confirmation.
- Canceling or closing the popup clears the pending confirmation.
- Grouped combat uses one popup-level confirmation for the committed transaction.
- Legal weapon selections still proceed on the first click.
- Logs distinguish blocked first clicks from confirmed second clicks through `[WarStance][PlayerWarning]`.

Scope and safety:

- Prohibited weapons remain visible and selectable.
- No heat or relationship penalties are applied yet.
- Confirmation state is runtime-only and requires no save migration.
- Automatic defense and AI compliance are unchanged.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `7:54 PM`, size `2747392`.
- Live DLL at the time of this note: 2026-06-14 `7:44 PM`, size `2746368`.
- Copy the `7:54 PM` build live before testing confirmation.

Next test signals:

- Under Hands Only, select the LeMat and press Fight once.
- The popup should remain open and show `Click Fight again to proceed`.
- The log should show `confirmationRequired=true confirmed=false blocked=true`.
- Press Fight again without changing selections; combat should proceed normally.
- The log should show `confirmationRequired=true confirmed=true blocked=false`.
- Reopen the popup, press Fight once with a violation, change to fists, and confirm the attack proceeds with one click and no stale confirmation.
- Cancel after the first warning click and confirm no combat or heat change occurs.

Live confirmation follow-up:

- The live `7:54 PM` DLL matched the expected confirmation build.
- The first prohibited Fight click logged `confirmed=false blocked=true`.
- The second click logged `confirmed=true blocked=false` and committed the same LeMat selection.
- The committed grouped attack used the LeMat under Hands Only, proving that player violation freedom remains intact.
- No confirmation or combat-popup exceptions appeared.
- The committed result also exposed an AI defender gap: the target outfit counterattacked with a Colt under Hands Only.
- Earlier on-foot spread results showed the same unrestricted AI defender behavior under Hands Only and Street Weapons.

### Phase 6 Player-Offense AI Defender Compliance Pass - 2026-06-14

Status: implemented and build-validated for live testing.

Implemented:

- Player-selected attacker weapons remain unrestricted after confirmation.
- AI defenders in player-initiated grouped combat now resolve the same pair heat and stance.
- Every grouped counterattacker draws from a stance-filtered weapon pool.
- The focused defender weapon is stance-compliant.
- On-foot spread targets now select stance-compliant defensive weapons as well.
- Cops and feds remain excluded.
- Added `[WarStance][PlayerGroupOffenseDefenderComply]` and `[WarStance][PlayerSpreadOffenseDefenderComply]` diagnostics.

Scope and safety:

- This pass does not restrict or replace the player's selected weapon.
- No violation heat or relationship penalties are applied yet.
- Confirmation behavior is unchanged.
- Weapon inventories and dropdown contents are unchanged.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `8:08 PM`, size `2748928`.
- Live DLL at the time of this note: 2026-06-14 `7:54 PM`, size `2747392`.
- Copy the `8:08 PM` build live before testing player-offense defender compliance.

Next test signals:

- Repeat a confirmed LeMat attack under Hands Only.
- The player attacker should still use the LeMat and report `attackerComplies=False`.
- The AI defender and all grouped counterresponders should use fists and report `defenderComplies=True`.
- `[WarStance][PlayerGroupOffenseDefenderComply]` should appear once for the grouped transaction.
- Test on-foot spread under Hands Only or Street Weapons; defenders should use fists or approved melee weapons.
- No AI defender Colt should appear below the Sidearms threshold.

Live follow-up:

- The live `8:08 PM` DLL matched the expected defender-compliance build.
- The run contained AI-versus-AI and AI-versus-player incoming combat only.
- Those combats remained stance-compliant, including grouped incoming attacks.
- No player attack popup, confirmation, player-offense defender-compliance marker, or player violation occurred in this run.
- The player-offense defender fix therefore still needs a direct player attack test before real penalties are enabled.

### Phase 6 Proposed Violation Consequence Shadow Pass - 2026-06-14

Status: implemented and build-validated for live testing.

Implemented:

- Reads the weapon that actually reached the combat result.
- Ignores popup selection changes, cancellation, AI attacks, automatic player defense, and compliant player attacks.
- Proposes `+10` heat for prohibited melee use.
- Proposes `+20` heat for prohibited sidearm use.
- Proposes `+30` heat for prohibited long guns, automatic weapons, or unknown weapons.
- Logs the proposed defender-to-player heat direction, resulting heat, and resulting stance.
- Logs one proposed hostile relationship penalty without applying it.
- Aggregates grouped exchanges and emits one proposal using the strongest prohibited weapon that actually fired.
- Added `[WarStance][ProposedViolation]` with `penaltiesApplied=false`.

Scope and safety:

- No heat is changed.
- No relationship buff or penalty is applied.
- No ticker or grapevine entry is created.
- Confirmation, combat damage, attack cost, and weapon selection are unchanged.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Live DLL status:

- Built repo DLL: 2026-06-14 `8:24 PM`, size `2750976`.
- Live DLL at the time of this note: 2026-06-14 `8:08 PM`, size `2748928`.
- Copy the `8:24 PM` build live before testing the consequence shadow path.

Next test signals:

- Repeat a confirmed LeMat attack under Hands Only.
- The AI defender should use fists and report `defenderComplies=True`.
- The transaction should emit exactly one `[WarStance][ProposedViolation]`.
- The LeMat proposal should report `proposedHeat=+20`, `resultingHeat=20`, `resultingStance=HandsOnly`, and `penaltiesApplied=false`.
- A grouped attack with several violating shooters should still emit one proposal.
- Canceling after the first confirmation click should emit no proposal.

Live follow-up:

- The live `8:24 PM` DLL matched the expected proposed-consequence build.
- Player-offense AI defender compliance worked under Hands Only, Street Weapons, and Sidearms.
- The first prohibited Fight click was blocked and the second click confirmed the LeMat selection.
- No proposal appeared because on-foot fist exchanges executed first and raised effective heat from `46` to `74`.
- The later LeMat drive-by therefore resolved as legal under Sidearms even though the player confirmed it under Street Weapons.
- No combat or confirmation exception appeared.

### Phase 6 Popup Commit Stance Snapshot Pass - 2026-06-14

Status: implemented and build-validated for live testing.

Implemented:

- Custom player combat popups now snapshot each human-attacker versus AI-outfit stance when Fight commits.
- Every action inside that one popup uses the committed stance for AI defender compliance and actual-result violation accounting.
- Heat gained by an earlier exchange no longer changes the agreement halfway through the same committed popup.
- Heat changes still affect the next independent combat or popup normally.
- Cops, feds, AI-versus-AI combat, and incoming AI attacks are excluded from the popup snapshot.
- Added `[WarStance][PlayerCommitSnapshot]` with the committed target pair, heat, and stance.
- The snapshot is cleared after results display and on the exception path.

Scope and safety:

- No heat or relationship penalties are applied yet.
- Player weapon choice, damage, attack cost, inventory, and dropdown contents are unchanged.
- The snapshot exists only during synchronous popup execution and requires no save migration.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- Under Street Weapons around heat `46`, select one on-foot fist action and one LeMat drive-by against the same outfit.
- Confirm the warning with the second Fight click.
- The log should show one `[WarStance][PlayerCommitSnapshot]` at Street Weapons and heat `46`.
- All actions in that popup should continue to report Street Weapons even if normal combat heat crosses `50`.
- The LeMat result should report `attackerComplies=False`.
- The transaction should emit exactly one `[WarStance][ProposedViolation]` with `proposedHeat=+20` and `penaltiesApplied=false`.
- A new later attack should resolve from the newly increased live heat rather than reusing the old snapshot.

Live follow-up - 2026-06-15:

- The live DLL matched the popup commit snapshot build.
- `[WarStance][PlayerCommitSnapshot]` appeared for player attacks with the expected pair heat and stance.
- AI attacker, AI defender, and automatic player-defense compliance remained healthy across Hands Only, Street Weapons, Sidearms, and Open Arsenal.
- No stance-related exception appeared.
- The only low-heat LeMat selection was changed back to fists before Fight committed.
- Later committed LeMat attacks occurred under Open Arsenal and were legal.
- The run therefore did not exercise `[WarStance][ProposedViolation]`; real penalties remain disabled.

### Phase 6 Popup-Wide Violation Aggregation Pass - 2026-06-15

Status: implemented and build-validated for live testing.

Implemented:

- One Fight command now owns one violation aggregation key even when it executes several grouped sub-transactions.
- Mixed on-foot, drive-by, grouped, and ordinary attack results inside the custom popup contribute to the same proposal.
- The strongest prohibited weapon actually used determines the single proposal.
- Sub-transaction completion no longer emits separate proposals for the same popup.
- The popup-level proposal flushes once after all committed actions finish.
- Exception and cleanup paths discard any unflushed popup proposal.

Scope and safety:

- No heat or relationship penalties are applied yet.
- Legal attacks and automatic player defense do not create proposals.
- Weapon choice, inventories, damage, and attack costs are unchanged.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- At Hands Only or Street Weapons, commit a popup containing a prohibited LeMat action.
- If possible, include both an on-foot action and a grouped vehicle action in the same Fight command.
- The committed stance should remain fixed for every action in that popup.
- At least one actual result must show `attackerWeapon=weapon-lemat` and `attackerComplies=False`.
- Exactly one `[WarStance][ProposedViolation]` should appear for the popup.
- Its transaction should begin with `CombatPopupCommit:`.
- Canceling or changing back to fists before commit should produce no proposal.

Live follow-up:

- The ranged attack ran with the prior popup-snapshot build, not the newer popup-wide aggregation build.
- The popup first displayed a LeMat violation, but the committed selection changed to fists and logged `violations=0`.
- The grouped attack nevertheless assigned a LeMat automatically to a second player crew member.
- That actual LeMat result correctly logged `attackerComplies=False`, `[WarStance][WouldViolate]`, and one shadow `[WarStance][ProposedViolation]`.
- The proposal correctly reported a Sidearm violation under Hands Only, `proposedHeat=+20`, and `penaltiesApplied=false`.
- This proves actual-result violation detection, but it exposed a warning bypass: an unselected grouped crew member could silently use a prohibited weapon.
- The proposal transaction used the older grouped transaction key because the popup-wide aggregation DLL was not live.

### Phase 6 Explicit Player Violation Assignment Pass - 2026-06-15

Status: implemented and build-validated for live testing.

Implemented:

- Only the weapon explicitly selected in the player popup may exceed the committed stance.
- Additional grouped player attackers now draw from stance-compliant vehicle weapons.
- If no compliant weapon is available, additional attackers fall back to fists.
- An explicitly selected prohibited weapon remains usable after the normal two-click warning.
- The same rule applies to grouped on-foot, on-foot spread, and drive-by assignment.
- AI compliance and automatic player defense remain unchanged.

Scope and safety:

- No weapon is removed from inventory or hidden from the popup.
- Real heat and relationship penalties remain disabled.
- The change prevents an unselected crew member from creating an unwarned player violation.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- Select fists under Hands Only with two or more grouped attackers.
- Every attacker should use fists and no proposal should appear.
- Then explicitly select the LeMat, confirm twice, and attack again.
- Exactly one attacker should use the selected LeMat; all additional attackers should use fists.
- The popup should warn before commit.
- Exactly one popup-wide proposal should appear with transaction `CombatPopupCommit:...`.

Live follow-up:

- The live DLL matched the explicit player violation assignment build.
- A fists selection under Hands Only kept both grouped player attackers on fists and produced no proposal.
- A billy club selected under Hands Only required the two-click confirmation.
- The explicitly selected attacker used the billy club and reported `attackerComplies=False`.
- The additional grouped attacker used fists and remained compliant.
- The popup emitted exactly one `[WarStance][ProposedViolation]`.
- Its transaction used the popup-wide `CombatPopupCommit:` key.
- The proposal correctly reported a Melee violation, `proposedHeat=+10`, and `penaltiesApplied=false`.
- No stance or combat-popup exception appeared.

### Phase 7 Real Violation Heat Pass - 2026-06-15

Status: implemented and build-validated for live testing.

Implemented:

- A proven player stance violation now adds real war heat once at the popup-wide proposal flush.
- The heat is added in the hostile outfit-to-player direction.
- Normal attack and kill heat complete before the separate stance-violation heat is applied.
- Melee violations add `+10`, sidearms add `+20`, and long guns, automatic weapons, or unknown weapons add `+30`.
- Added `[WarStance][StanceViolated]` with directional and effective heat before and after application.
- The marker reports the stance that the next independent combat will use.
- The existing transaction key prevents duplicate heat application inside one popup.

Deferred:

- The relationship penalty remains shadow-only until a dedicated hostile buff definition and persistence behavior are finalized.
- `[WarStance][StanceViolated]` reports `relationshipPenaltyApplied=false`.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- Commit one billy-club violation under Hands Only.
- The popup should emit one proposal and one `[WarStance][StanceViolated]`.
- The violation marker should report `violationHeat=+10` and `heatApplied=true`.
- The GangOps war-heat log should show reason `player-war-stance-violation`.
- No second violation marker should appear for additional grouped exchanges.
- Open a new combat against the same outfit and confirm it uses the logged `nextStance`.
- Canceling or selecting fists should not add violation heat.

Live follow-up:

- The live DLL matched the real violation heat build.
- Three confirmed melee violations each emitted one proposal and one `[WarStance][StanceViolated]`.
- Each violation added exactly `+10` in the offended outfit-to-player direction.
- Normal attack heat completed first; examples showed directional heat moving `14 -> 24`, `26 -> 36`, and `49 -> 59`.
- Threshold crossings reported the correct next stance, including Street Weapons and Sidearms.
- Additional grouped exchanges did not duplicate the violation heat.
- No stance or combat-popup exception appeared.

### Phase 7 Real Relationship Penalty Pass - 2026-06-15

Status: implemented and build-validated for live testing.

Implemented:

- A proven player stance violation now applies one directed relationship penalty from the offended outfit toward the player.
- Added dedicated buff ID `relbuff-war-stance-violated`.
- The buff applies `-10` relationship for 180 days.
- It uses the existing localized “wrong foot” description rather than the much stronger gang-injury wording.
- The buff is registered in the existing persistent gang relationship system.
- Repeated violations refresh the dedicated buff instead of adding unrelated injury or death penalties.
- `[WarStance][StanceViolated]` now reports the buff ID and whether application succeeded.

Scope and safety:

- Heat values and one-per-popup aggregation are unchanged.
- The relationship penalty is directional and does not create a truce, clear aggro, or alter inventory.
- The existing gang-injury penalty remains reserved for actual crew injury or death behavior.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- Commit one prohibited attack against an outfit.
- `[WarStance][StanceViolated]` should report `relationshipBuff=relbuff-war-stance-violated` and `relationshipPenaltyApplied=True`.
- The buff injection log may appear once on first use.
- The relationship UI should show one negative “wrong foot” entry for that outfit.
- Save and reload, then confirm the relationship penalty remains.
- A second violation should refresh the same entry, not create duplicate relationship entries.

Live follow-up:

- The live DLL matched the relationship penalty build.
- The dedicated `relbuff-war-stance-violated` definition injected once from the gang-injury template.
- `[WarStance][StanceViolated]` reported `relationshipPenaltyApplied=True`.
- A later violation against another outfit reused the definition without reinjection.
- Persistent gang relationship reconciliation refreshed records with no invalid entries.
- Heat and relationship penalties remained one-per-popup.
- No relationship, stance, or popup exception appeared.

### War Weapon Stance Compatibility Gate Pass - 2026-06-15

Status: implemented and build-validated for live testing.

Implemented:

- Added config entry `[WarWeaponStance] Enabled`, default `true`.
- When disabled, combat popups hide the war stance summary and do not require violation confirmation.
- AI-versus-AI and AI-versus-player combat stop filtering weapons by stance.
- Grouped player offense and AI defense stop applying stance weapon restrictions.
- Player commit snapshots, stance diagnostics, heat penalties, and relationship penalties stop.
- Existing grouped-combat behavior remains enabled.
- Saved war heat and existing relationship history are preserved.
- The loaded-build banner reports `warWeaponStanceEnabled=true/false`.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test signals:

- Launch once with `[WarWeaponStance] Enabled = false`.
- The loaded banner should report `warWeaponStanceEnabled=False`.
- The combat popup should not show the stance panel or require a second Fight click.
- Low-heat AI combat may use its normal best weapons.
- No `[WarStance]` combat, proposal, or penalty markers should appear.
- Restore the setting to `true`, relaunch, and confirm stance behavior resumes using the existing saved heat.

### Phase 7 Configurable Heat Tuning Pass - 2026-06-15

Latest enabled-build evidence confirms:

- a Hands Only crowbar selection required the second confirmation click
- only the selected crowbar violated; the extra grouped attacker used fists
- the AI defender used fists
- one `+10` melee violation applied in the enemy-to-player heat direction
- the pair advanced to Street Weapons
- the relationship penalty applied once
- no runtime exceptions appeared

Implemented without changing combat ownership or save state:

- `StreetWeaponsThreshold=25`
- `SidearmsThreshold=50`
- `OpenArsenalThreshold=75`
- `MeleeViolationHeat=10`
- `SidearmViolationHeat=20`
- `HeavyViolationHeat=30`

Thresholds normalize at runtime into a strictly increasing `1..100` sequence. Violation heat values clamp to `0..100`. The startup banner reports both effective triplets so balancing logs identify the active profile.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Confirm the banner reports `warWeaponStanceThresholds=25/50/75` and `warWeaponStanceViolationHeat=10/20/30`.
2. Repeat one low-heat melee violation and confirm it still adds exactly `10`.
3. Temporarily change only `MeleeViolationHeat`, restart, and confirm the banner and one violation use that value.
4. Restore the default before continuing normal balance tests.

### Phase 10 Enemy Sit-Down Diagnostic Surface - 2026-06-15

Latest live evidence confirms:

- the tuning build loaded with thresholds `25/50/75`
- violation heat loaded as `10/20/30`
- a melee violation added `10`
- a sidearm violation added `20`
- stance transitions used the resulting heat
- AI attackers, AI defenders, and automatic player defenders remained compliant
- no runtime exceptions appeared

Implemented as a read-only first pass:

- Added a boss-only `Outfit Sit-Downs` button to Crew Relations.
- Added a scrollable hostile-outfit panel without physical contact or a target boss peep.
- Eligible rows exclude defeated outfits, truces, and pact-protected alliances.
- Eligibility currently accepts active pair heat, direct aggro, or a hostile relationship bias.
- Each row shows both heat directions, effective heat, current stance, player/enemy power, and Peaceful/Wary/Guarded posture.
- Each row previews Talk `-5`, Leisure `-10`, and Liquor Deal `-20` heat outcomes and resulting stances.
- Acceptance scoring and a stable 14-day-window diagnostic roll are logged but not exposed as raw UI values.
- Added `[WarStance][SitDownOpened]` and `[WarStance][CoolingOption]` markers.
- No action spends cash, consumes conversation actions, launches a conversation, changes relationships, or changes war heat.
- The panel closes when another crew member is selected or boss-only menus close.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Select the player boss and confirm `Outfit Sit-Downs` appears.
2. Select another crew member and confirm the button and panel are unavailable.
3. Open the panel and compare the listed heat/stance against recent combat logs.
4. Confirm Talk, Leisure, and Liquor previews cross thresholds correctly where applicable.
5. Confirm `[WarStance][SitDownOpened]` and one `[WarStance][CoolingOption]` line per listed outfit.
6. Confirm opening and closing the panel changes no cash, actions, relationships, aggro, truce state, or heat.

### Phase 10 Sit-Down Panel Layering And Placement Fix - 2026-06-15

Screenshot and live-log evidence confirmed the diagnostic rows were generated correctly, but the panel shared the left dock with Crew Relations and had no independent sorting canvas. Crew Relations sorting order `998` therefore rendered over the sit-down text until the user manually moved it.

Implemented:

- Added an independent sit-down `Canvas` and `GraphicRaycaster`.
- Set sit-down sorting order to `999`, directly above Crew Relations.
- Replaced fixed far-left docking with side-aware placement using the current Crew Relations world rectangle.
- Prefer opening to the right of Crew Relations; use the left side when only that side fits.
- Clamp the panel horizontally inside the overlay when neither side has full room.
- Keep the sit-down panel as the last sibling after positioning.
- No sit-down gameplay behavior was added in this pass.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Leave Crew Relations in its default position and open `Outfit Sit-Downs`.
2. Confirm the panels appear beside each other immediately.
3. Confirm every sit-down row remains readable without moving Crew Relations.
4. Move Crew Relations near the opposite screen edge, reopen Sit-Downs, and confirm it chooses the available side.
5. Confirm both panels remain clickable and their close buttons work.

### Phase 10 Player Talk Cooling First Pass - 2026-06-15

Latest live evidence confirms:

- the `10:03:44` layering build was active
- the sit-down panel fit beside Crew Relations
- six hostile outfit rows were generated
- AfterProhibitionUI rethemed the panel normally
- no UI or runtime exceptions appeared

Implemented Talk as the only active cooling option:

- Talk is available when effective WarHeat is above `0` and below `50`.
- The player boss must be able to pay the normal conversation action cost.
- Eligibility, heat, truce/alliance state, cooldown, and action points revalidate on click.
- Acceptance uses the diagnostic score and stable deterministic roll already shown in logs.
- Decline consumes the conversation action, changes no heat, and starts a `7` day cooldown.
- Acceptance consumes the conversation action before cooling.
- Successful Talk subtracts `5` from every existing directional record in both Pact and Independent channels.
- Zero-value records are removed.
- Successful Talk starts a `14` day pair cooldown.
- Cooldown is stored under a namespaced key in the existing persisted mediation dictionary.
- Talk does not change aggro, create a truce, grant relationship buffs, move resources, or require physical contact.
- Leisure and Liquor Deal remain disabled previews.

Added runtime markers:

- `[WarStance][ProposalAccepted]`
- `[WarStance][ProposalDeclined]`
- `[WarStance][CoolingHeat]`
- `[WarStance][CoolingCompleted]`
- `[WarStance][CoolingBlocked]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Open Outfit Sit-Downs with the boss.
2. Confirm zero-heat and `50+` heat Talk buttons are disabled with a reason.
3. Propose Talk to an outfit between `1-49` heat.
4. On decline, confirm one boss conversation action is consumed, heat is unchanged, and a `7d` cooldown appears.
5. On acceptance, confirm one action is consumed and both directional heat records fall by exactly `5`.
6. Confirm the next combat uses the resulting stance.
7. Save and reload during cooldown and confirm the remaining cooldown persists.
8. Confirm aggro and truce state remain unchanged.

### Phase 10 Talk Availability And Decline Verification Pass - 2026-06-15

Latest live evidence confirms:

- the `10:17:16` Talk build was active
- three declined Talk proposals each consumed exactly one boss action: `15->14`, `14->13`, and `13->12`
- each decline immediately applied a separate `7` day pair cooldown
- the corresponding rows became unavailable with `cooldownRemaining=7`
- no cooling marker appeared on decline
- no runtime exception or action-payment warning appeared

One UI mismatch was also proven: the Talk button remained clickable before the boss could pay the conversation action cost, although click-time validation correctly blocked it.

Implemented:

- Talk availability now includes the boss's ability to pay the current conversation cost.
- The disabled button reports `Talk (Boss needs action)` when action points are insufficient.
- `[WarStance][CoolingOption]` now reports `bossCanPayAction`.
- `[WarStance][ProposalDeclined]` now reports before/after effective heat and both directional heat values, making unchanged heat explicit.
- Leisure and Liquor remain previews until an accepted Talk proves bilateral cooling.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Open the panel with no boss conversation action available and confirm Talk is disabled with `Boss needs action`.
2. Restore boss actions and confirm eligible Talk buttons enable.
3. On another decline, confirm the log reports identical before/after values for both directions.
4. Obtain one accepted Talk and confirm `[CoolingHeat]` plus `[CoolingCompleted]` report exactly `-5`.

### Phase 10 Player Leisure Cooling First Pass - 2026-06-15

Latest live evidence confirms the Talk prerequisite:

- accepted Talk cooled enemy-to-player heat from `4` to `0`
- a second accepted Talk cooled enemy-to-player heat from `16` to `11`
- each acceptance consumed exactly one boss conversation action and started a `14` day cooldown
- declined Talk offers left both heat directions unchanged and started a `7` day cooldown
- no Talk, action-payment, or runtime exception appeared

Implemented:

- Leisure is available when effective WarHeat is above `0` and below `75`.
- The player boss must have the normal conversation action and the safehouse must have `$1,000` clean cash.
- Eligibility, heat, cooldown, actions, and cash revalidate on click.
- A decline consumes one conversation action, charges no cash, changes no heat, and starts a `7` day cooldown.
- An acceptance consumes one conversation action and then charges `$1,000`.
- Accepted Leisure applies the established leisure relationship buffs and player street-credit progress before cooling.
- Accepted Leisure reduces every existing bilateral Pact and Independent heat record by `10`, then starts a `14` day cooldown.
- Leisure does not transfer resources, create a truce, clear aggro, or require physical contact.
- Liquor Deal remains a disabled preview.
- Sit-down diagnostics now report Talk and Leisure as active options.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Confirm Leisure is disabled with `Need $1,000` when the player safehouse cannot pay.
2. Test one declined Leisure and confirm actions decrease by one while cash and both heat directions remain unchanged.
3. Test one accepted Leisure and confirm cash decreases by exactly `$1,000`, actions decrease by one, and effective heat decreases by exactly `10`.
4. Confirm the leisure relationship buffs and street-credit progress apply only on acceptance.
5. Prefer a test near a stance threshold, such as heat `58 -> 48`, and confirm the next combat uses the lower stance.
6. Save and reload during the Leisure cooldown and confirm the remaining days persist.
7. Confirm aggro and truce state remain unchanged.

### Phase 10 Player Liquor Cooling First Pass - 2026-06-15

Latest live evidence confirms the Leisure prerequisite:

- one accepted Leisure charged exactly `$1,000`, consumed one boss action, and cooled heat from `8` to `0`
- another accepted Leisure charged exactly `$1,000`, consumed one boss action, and cooled heat from `58` to `48`
- the `58 -> 48` result immediately changed the previewed stance from Sidearms to Street Weapons
- both established leisure relationship buffs applied in both directions
- player street-credit progress applied successfully
- each accepted Leisure started a `14` day cooldown
- later AI attacks continued using the current heat stance without exceptions

Implemented the player-only `$3,000` Liquor Deal:

- Liquor is available when effective WarHeat is above `0` and below `100`.
- The player boss must have the normal conversation action.
- The player safehouse must exist, expose an inventory, and have `$3,000` clean cash available.
- Decline consumes one conversation action, charges no cash, grants no goods, changes no heat, and starts a `7` day cooldown.
- Acceptance revalidates cash and safehouse delivery before beginning the transaction.
- The cash debit must equal exactly `$3,000` before any goods or rewards apply.
- The established authored bundle is delivered to the player safehouse:
  - `streetcredit x2`
  - `moonshine x50`
  - `home-brew x100`
  - `cider x50`
  - `brick-wine x45`
- Partial resource delivery rolls back the added goods and refunds the cash.
- Successful delivery applies `150` XP to the player boss.
- Successful delivery applies `relbuff-gangs-loot1-table-on-finish` and `relbuff-gangs-loot2-buff`.
- The authored local trade heat buff is applied at the boss's current node when that node is available.
- Only after the transaction completes does the deal reduce bilateral WarHeat by `20`.
- Success starts the shared `14` day pair cooldown.
- AI offer code is unchanged and cannot offer or execute the Liquor Deal.

Added runtime diagnostics:

- `[WarStance][LiquorTransaction]` reports exact cash, goods, XP, relationship buffs, and local heat results.
- Liquor proposal acceptance, decline, blocked, and cooling markers use the existing sit-down diagnostic family.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Confirm the Liquor button is disabled with `Need $3,000` when cash is insufficient.
2. Confirm the button reports `No safehouse` if safehouse delivery is unavailable.
3. Test one decline and confirm one action is consumed while cash, goods, and heat remain unchanged.
4. Test one acceptance and confirm exactly `$3,000` cash, the full authored bundle, and `150` boss XP.
5. Confirm both relationship buffs and the local trade heat marker report success.
6. Prefer heat near `82`; confirm successful cooling reaches `62` and changes Open Arsenal to Sidearms.
7. Confirm the next combat uses the cooled stance.
8. Confirm the pair receives a `14` day cooldown and AI never presents a Liquor offer.

### Phase 10 Passive AI Offer Lifecycle First Pass - 2026-06-15

Latest live evidence confirms the player Liquor prerequisite:

- the active DLL matched the Liquor build
- one accepted deal charged exactly `$3,000`, granted the complete authored bundle, applied `150` boss XP, both relationship buffs, and local trade heat
- that deal cooled heat from `94` to `74`, changing Open Arsenal to Sidearms
- a second accepted deal repeated the exact transaction and cooled heat from `64` to `44`, changing Sidearms to Street Weapons
- each success consumed one boss action and started a `14` day cooldown
- no sit-down exception appeared

Implemented a read-only passive AI-offer lifecycle:

- AI offer evaluation runs during the existing human-turn maintenance path.
- Evaluation never opens a popup, queues a modal, moves focus, or acquires an input lock.
- At most one new offer is created per human turn.
- At most one pending offer exists per enemy outfit.
- Eligible offers require a living enemy `GangPlayer`, positive pair heat, no truce or pact protection, and no pair sit-down cooldown.
- Peaceful or isolationist outfits prefer Talk.
- Wary outfits prefer Leisure when their specific safehouse exists and contains at least `$4,200`.
- Aggressive and stronger outfits receive strong offer-chance penalties.
- AI offers are limited to Talk or Leisure; Liquor is not an AI offer type.
- Pending offers last `14` days in this diagnostic pass.
- Offers expire when their lifetime ends, the outfit becomes invalid, a truce or pact protection begins, heat reaches zero, or a Leisure treasury falls below `$4,200`.
- The boss `Outfit Sit-Downs` button shows a pending-offer count.
- The correct outfit row shows `Sit-down offered: Peaceful Talk` or `Sit-down offered: Wary Leisure`.
- Leisure rows explicitly state that the outfit pays `$1,000`.
- Player proposal buttons remain available; AI-offer Accept and Decline actions are intentionally deferred.
- Pending offers are runtime-only in this first pass; persistence is deferred until creation, display, and invalidation are proven.

Added runtime markers:

- `[WarStance][AIOfferCreated]`
- `[WarStance][AIOfferDisplayed]`
- `[WarStance][AIOfferExpired]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. End turns with at least one peaceful or clearly weaker hostile outfit at positive heat and no cooldown.
2. Confirm `[AIOfferCreated]` appears without any popup or focus change.
3. Select the boss and confirm `Outfit Sit-Downs (1)` or a higher pending count appears.
4. Open Outfit Sit-Downs and confirm the offer appears on the matching gang row with Peaceful or Wary posture.
5. Confirm no AI offer is labeled Liquor.
6. For Leisure, confirm creation reports at least `$4,200` in that outfit's safehouse.
7. Confirm no cash, actions, relationships, aggro, truce state, or heat change merely from creating or viewing the offer.
8. Let an offer expire or invalidate one through zero heat/truce and confirm `[AIOfferExpired]`.

### Phase 10 AI Offer Accept And Decline Pass - 2026-06-15

Latest live evidence exposed the expected diagnostic limitation:

- gang `27` created and displayed a `Peaceful Talk` offer
- the offer row still exposed the normal player `Propose Talk` button
- clicking it ran the player proposal path, consumed one player action, and produced `[ProposalDeclined]`
- high-heat AI Talk offers had no dedicated acceptance path because player Talk is normally blocked at heat `50+`

Implemented:

- A row with a pending AI offer now hides Talk, Leisure, and Liquor proposal controls.
- Pending rows expose only `Accept Talk Offer` or `Accept Leisure Offer`, plus `Decline Offer`.
- Accepting an AI offer never consumes a player conversation action.
- AI Talk acceptance is valid at any positive heat and cools bilateral heat by `5`.
- AI Leisure acceptance revalidates the offering outfit's specific safehouse and requires at least `$4,200`.
- AI Leisure debits exactly `$1,000` from that safehouse and requires at least `$3,200` to remain.
- Failed treasury revalidation expires the offer without cooling heat.
- Successful AI Leisure applies the established leisure relationship buffs and player street-credit progress before cooling bilateral heat by `10`.
- Successful acceptance clears the pending offer and starts a `14` day pair cooldown.
- Decline clears the pending offer, charges no action or cash to either side, changes no heat, and starts a `7` day pair cooldown.
- Accept and Decline refresh both Outfit Sit-Downs and the boss button pending count immediately.
- AI offers still never include Liquor and never open an unsolicited popup.

Added runtime markers:

- `[WarStance][AIOfferAccepted]`
- `[WarStance][AIOfferDeclined]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.
- `git diff --check` passed with no whitespace errors.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `11:36:15 AM`
- size `2802176`

Next test:

1. Create a pending Talk offer above heat `50`.
2. Confirm that row shows `Accept Talk Offer` and `Decline Offer`, with no player proposal buttons.
3. Accept it and confirm no player action is consumed and heat drops by exactly `5`.
4. Create a Leisure offer and confirm the outfit pays exactly `$1,000`, retains at least `$3,200`, and heat drops by exactly `10`.
5. Decline another offer and confirm no action, cash, relationship, or heat changes.
6. Confirm the pending count decreases immediately after acceptance or decline.
7. Confirm no offer action creates a truce or clears aggro.

Live follow-up:

- The active DLL matched the `11:36:15 AM` acceptance build.
- High-heat Talk acceptance cooled exactly `5` with zero player actions.
- Decline preserved heat, cash, and actions and started the `7` day cooldown.
- AI-funded Leisure charged the outfit exactly `$1,000`, retained more than `$3,200`, applied both leisure buffs and street credit, and cooled heat exactly `10`.
- A later Leisure offer expired with `reason=leisure-treasury` after the outfit fell below `$4,200`.
- No runtime exceptions were reported.

### Phase 10 AI Offer Persistence Pass - 2026-06-15

Implemented:

- Added pending AI sit-down offers to the existing GameplayTweaks V2 save state.
- Save capture stores only gang ID, offer type, posture, creation day, and expiry day.
- Load restoration accepts only Talk or Leisure and Peaceful or Wary records with valid IDs and dates.
- Restored offers pass through the existing defeat, truce, zero-heat, lifetime, and Leisure treasury validation.
- Loading a valid pending offer marks the current day evaluated so reload cannot immediately create an additional offer.
- Invalid or expired saved records are removed before the Crew Relations UI reads them.

Added runtime marker:

- `[WarStance][AIOffersRestored]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.
- `git diff --check` passed with no whitespace errors.
- The normal and fallback SION readers both support the new `Dictionary<int, WarStanceAiOfferState>` field.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `11:56:09 AM`
- size `2804224`
- Live DLL remains the prior `11:36:15 AM` build until manually copied.

Next test:

1. Wait for a Talk or Leisure offer and save before responding.
2. Reload the save and confirm `[AIOffersRestored] restored=1 active=1`.
3. Confirm the boss button pending count and matching outfit row still show exactly one offer.
4. Accept or decline the restored offer and save/reload again.
5. Confirm the resolved offer does not return.
6. Save a Leisure offer, reduce the outfit below `$4,200` if practical, then reload and confirm it is pruned without cooling heat.

Live follow-up:

- The active DLL matched the `11:56:09 AM` persistence build.
- `[AIOffersRestored] restored=1 active=1 rejected=0` confirmed one saved Talk offer returned.
- The restored offer displayed on gang `27` and retained its Accept/Decline-only controls.
- Accepting the restored offer cooled heat `14 -> 9` with zero player actions.
- Saving after acceptance did not restore the resolved offer again.
- No load, save, UI, or sit-down exceptions appeared.
- The load hook temporarily reported day `0` before startup reached the real campaign day `701715`.

### Phase 10 AI Offer Load-Timing Hardening - 2026-06-15

Implemented:

- Saved offers are restored during the load hook without running world-dependent validation while the game clock still reports day `0`.
- The newest restored creation day becomes the offer-evaluation guard until the real campaign clock is available.
- This prevents the first human-turn maintenance pass after reload from creating another offer on the same in-game day.
- Existing UI and human-turn paths still prune lifetime, defeat, truce, zero-heat, and Leisure treasury failures once the world is ready.
- The restore marker now reports load day, evaluation day, and whether validation was immediate or deferred.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.
- `git diff --check` passed with no whitespace errors.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `12:48:19 PM`
- size `2804224`
- Live DLL remains the prior `11:56:09 AM` build until manually copied.

Next test:

1. Save with one pending offer and reload.
2. Confirm `[AIOffersRestored]` reports `loadDay=0`, the real saved `evaluationDay`, and `validation=deferred-until-world-ready`.
3. Advance through the first loaded human turn and confirm no second same-day offer is created.
4. Open Outfit Sit-Downs and confirm the restored offer is still valid and unique.
5. Advance to a later eligible day and confirm normal offer creation resumes.

### Player Fists Dropdown Interactable Fix - 2026-06-15

Latest live evidence:

- Player combat popups repeatedly remained on Browning or LeMat while Hands Only was active.
- AI combat continued to resolve `weapon-fists` correctly, so classification and the shared fists resource were valid.
- The grouped popup rebuild restored the weapon dropdown's enabled state from its previous transient value.
- During some popup refreshes, the method dropdown was read while the target row was temporarily disabled and remained disabled after a valid target returned.

Implemented:

- The rebuilt weapon dropdown now derives its enabled state from the current valid target and whether more than one weapon option exists.
- A stale disabled state from an earlier refresh no longer prevents selecting fists under Any or Melee mode.
- No-target rows remain disabled.
- Ranged-only mode continues to exclude fists by design.

Added runtime marker:

- `[WarStance][PlayerWeaponDropdownRepaired]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.
- `git diff --check` passed with no whitespace errors.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `12:53:20 PM`
- size `2804736`
- Live DLL remains the prior `11:56:09 AM` build until manually copied.

Next test:

1. Open a player attack with a vehicle containing melee and firearm weapons.
2. Cycle the on-foot filter between Any, Melee, and Ranged.
3. Confirm Any and Melee keep the weapon dropdown enabled and allow `weapon-fists`.
4. Confirm Ranged excludes fists but leaves valid ranged choices selectable.
5. Change targets and attacker order, then confirm the weapon dropdown does not remain disabled after refresh.

Live follow-up:

- The active DLL matched the `12:53:20 PM` dropdown repair build.
- `[PlayerWeaponDropdownRepaired]` fired once for the stale disabled row.
- The player changed the selection to fists, the popup warning cleared, and the actual grouped combat used `weapon-fists` for attacker and defender.
- Follow-up AI and player-involved combats continued to comply under Hands Only.
- No runtime exceptions were reported.

### Player Popup Stance-Compliant Default Pass - 2026-06-15

Implemented:

- The popup now records explicit player weapon selections immediately when the Method dropdown changes.
- If no explicit player choice exists yet, the popup defaults to the best currently visible stance-compliant weapon.
- Hands Only defaults to fists when the base popup would otherwise start on a crowbar, pistol, or long gun.
- Explicit player choices are still respected, so the player can intentionally choose a prohibited weapon and accept the penalty path.
- Cops and feds are excluded from this defaulting path, matching the warning behavior.

Added runtime marker:

- `[WarStance][PlayerWeaponDefaultedToStance]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.
- `git diff --check` passed with no whitespace errors.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `1:14:04 PM`
- size `2805760`
- Live DLL remains the prior `12:53:20 PM` dropdown-repair build until manually copied.

Next test:

1. Open a Hands Only player attack with firearms and melee weapons in the vehicle.
2. Confirm the Method dropdown starts on fists without needing a manual change.
3. Manually pick a prohibited weapon and confirm the warning returns.
4. Change targets or attacker order and confirm the explicit prohibited choice is preserved.
5. Cancel the popup and confirm no heat or penalty is applied.

Live follow-up:

- The active DLL matched the `1:14:04 PM` stance-compliant default build.
- Melee and Any on-foot filters defaulted the Method dropdown to `weapon-fists` and cleared the player warning before grouped combat committed.
- The actual grouped combat used `weapon-fists` for all logged player attackers and defenders under Hands Only.
- A later Street Weapons popup still recorded `weapon-browning-1918` when the popup began from the ranged/drive-by path, because fists were not present in that filtered list.

### Player Popup Stance-Aware Initial Filter Pass - 2026-06-15

Implemented:

- Before the player has made an explicit weapon, attack-type, or range-filter choice, low-heat enemy target popups now default to On Foot plus Melee.
- This gives the existing stance-compliant weapon default pass a fists/melee list to choose from instead of a ranged-only list.
- Manual attack-type and range-filter clicks are tracked and respected, so the player can still intentionally choose ranged or drive-by options.
- Target-row changes re-run the initial default only while the popup is still automatic; explicit Method dropdown choices remain preserved.
- Cops, feds, human targets, and non-enemy rows remain excluded.

Added runtime marker:

- `[WarStance][PlayerPopupModeDefaultedToStance]`

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Next test:

1. Open a low-heat player attack where the first available popup path previously showed Browning.
2. Confirm the attack type starts On Foot, Range starts Melee, and Method starts on fists or the best legal street weapon.
3. Click Range until Ranged and confirm Browning or another ranged weapon can still be selected manually.
4. Change targets before choosing a weapon and confirm low-heat enemies still return to the compliant initial mode.
5. Pick a prohibited weapon manually, then confirm the warning and violation flow still work.

Live follow-up:

- The active DLL matched the `2:03:56 PM` GameplayTweaks build.
- AI-versus-AI compliance remained clean for Hands Only and Street Weapons.
- AI-versus-player defense compliance remained clean for Hands Only and Sidearms.
- No fresh player-offense popup, warning, proposed violation, or real stance violation marker appeared in the latest log slice.

### Player Offense Readiness Diagnostic Pass - 2026-06-15

Implemented:

- Added `[WarStance][PlayerPopupReady]` diagnostics for player attack popups.
- The marker reports the existing popup diagnostic plus current combat mode, on-foot weapon filter, and whether a violation confirmation is pending.
- The readiness marker is cached per popup state and cleared with the rest of the popup caches, so it should not spam every refresh.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `4:14:18 PM`
- size `2810880`

Next test signals:

1. Open a player attack against an enemy outfit while heat is below the selected weapon's stance.
2. Confirm `[WarStance][PlayerPopupReady]` appears when the popup has a valid target and selected method.
3. Select a weapon over stance and click Fight once; confirm `[WarStance][PlayerWarning] warning=true confirmationRequired=true confirmed=false blocked=true`.
4. Click Fight again; confirm `[WarStance][PlayerWarning] ... confirmed=true`, then `[WarStance][ProposedViolation]` and `[WarStance][StanceViolated]`.

Live follow-up:

- The active DLL matched the `4:14:18 PM` readiness diagnostic build.
- `[WarStance][PlayerPopupReady]` appeared for compliant and prohibited player selections.
- A Browning over Street Weapons required the first warning click, accepted the second click, and committed combat.
- A Browning over Hands Only produced `[WarStance][ProposedViolation]` and `[WarStance][StanceViolated]`.
- The grouped violation applied too late in the observer order: the proposal was based on `currentHeat=0.0`, but the final violation log read `directionalHeatBefore=100.0 directionalHeatAfter=100.0`.

### Grouped Violation Early Flush Pass - 2026-06-15

Implemented:

- Grouped combat now flushes a pending stance violation immediately after `LogWarStanceCombatDiagnostic` records it in `DispatchCombatObservers`.
- The flush now happens before normal combat consequence processors can raise war heat.
- The existing end-of-transaction flush remains as a safety no-op for cases where no earlier violation was recorded.

Validation:

- `dotnet build GameplayTweaks\GameplayTweaks.csproj -c Release` passed with `0` warnings and `0` errors.

Built DLL:

- `GameplayTweaks\bin\Release\GameplayTweaks.dll`
- 2026-06-15 `4:25:49 PM`
- size `2810880`

Next test signals:

1. Repeat a player grouped/spread attack with a prohibited weapon at low heat.
2. Confirm `[WarStance][StanceViolated]` reports a real before/after change, such as `0.0 -> 30.0`, before normal combat heat pushes the pair higher.
3. Confirm only one `[WarStance][StanceViolated]` appears for the grouped attack.
4. Confirm later compliant grouped attackers still use fists or legal melee instead of the prohibited selected weapon.

## Existing Anchors

Game-side anchors:

- `decompiled\Game.Session.Player.AI\CombatAdvisor.cs`
- `decompiled\Game.Session.Player.AI\CombatAdvisorData.cs`
- `decompiled\Game.Session.Sim\CombatManager.cs`
- `decompiled\Game.Session.Entities\WeaponConfig.cs`
- `decompiled\Game.UI.Session.Combat\CombatPopupPlanning.cs`
- `decompiled\Game.UI.Session.Combat\CombatCardContext.cs`

GameplayTweaks anchors:

- `WarHeatEntry`
- `GetWarHeat`
- `AddWarHeat`
- `ReduceWarHeatBothWays`, to be added
- `RegisterGangOpsHostileEvent`
- `RealCombatGrapevinePatch`
- `VehicleGroupCombatPatch`
- `CrewRelationshipHandlerPatch`
- `CrewRelationshipHandlerPatch.CreateHandlerPopup`
- `CrewRelationshipHandlerPatch.RefreshHandlerUI`
- boss-only `flag4` visibility pattern used by Side Quests and Gang Meetings
- existing boss Crew Relations list, button, and refresh patterns
- `CalculateGangPower`
- `IsAiPersonalityPeaceful`
- `IsAiPersonalityAggressive`
- `CombatManager.PerformHumanCombat` patch
- `CombatManager.PerformAICombat` patch
- `CombatManager.PerformCombat` postfixes
- `CombatPopupPlanning.OnFight` patch
- `CombatPopupPlanning.RefreshContents` patch
- existing AI personality helpers

Vanilla behavior to account for:

- `CombatManager.PerformAICombat` gives both sides their best vehicle weapon.
- Human attacks pass the selected weapon into `PerformHumanCombat`.
- AI attacks against the player automatically use the player's best vehicle weapon for defense.
- `WeaponConfig` distinguishes firearms but does not provide enough categories for sidearms versus long guns.
- `CombatManager.PerformCombat` emits `GangWarAction` and is the final shared combat transaction.

## Blast Radius

Overall blast radius: **medium**.

Safe diagnostics and shadow phases: **low**.

Final enforcement phases: **medium**, with isolated medium-high risk around the already complex grouped-combat path.

High-risk surfaces:

- AI attacker and defender weapon selection
- player automatic defensive weapon selection
- grouped vehicle combat weapon pools
- duplicate penalties from multi-member combat
- reading existing heat consistently across save/load

Medium-risk surfaces:

- gang war heat and retaliation timing
- relationship changes after violations
- player combat popup lifecycle
- Crew Relations offer-row refresh and save/load restoration
- simultaneous wars with different stances
- AI-versus-AI combat balance

Low-risk surfaces:

- weapon inventories and monthly drops
- map generation
- weapon shops and resource unlocks
- business production and trade

The main safety rule is to leave inventories unchanged and resolve stance only when selecting or committing a combat weapon.

## Save-State Requirement

The core heat-driven stance system should add **no new persistent pair state**.

Use the existing war-heat storage as the source of truth. Configurable thresholds belong in normal mod settings, not campaign pair records.

The later Enemy Sit-Down phase is an exception. It may persist only:

- proposal cooldown by outfit pair
- deterministic player-proposal result
- pending AI offer shown in Outfit Sit-Downs
- last AI offer decline day

This sit-down ledger does not store a stance. Combat still derives stance directly from current heat.

Optional bounded runtime caches may store:

- one-time unknown weapon warnings
- one combat transaction token for grouped violation deduplication
- short-lived popup confirmation state

Clear runtime caches on session unload. Do not require save migration for the core stance resolver.

## Phase 0: Verify Runtime Anchors

Before editing combat behavior, verify live signatures with dnSpy:

- `CombatManager.PerformHumanCombat`
- `CombatManager.PerformAICombat`
- `CombatManager.PerformCombat`
- `CombatPopupPlanning.OnFight`
- `CombatPopupPlanning.RefreshContents`

Also verify the existing `GameplayTweaks` Harmony owners for those methods so the new work extends current patches instead of adding competing prefixes.

Pass criteria:

- the final shared combat commit point is confirmed
- grouped combat transactions have one stable transaction identifier
- existing war heat can be read safely for both outfit directions during combat
- no custom war lifecycle patch is required

## Phase 1: Diagnostics-Only Combat Stance

Observe real combat without detecting war lifecycle events or changing weapon selection.

Log:

- pair IDs
- directional aggro state
- directional and effective war heat
- crew count
- active vehicle count
- melee, sidearm, long-gun, and automatic-weapon coverage
- resolved stance and heat threshold
- whether the attacker and defender are player or AI
- selected attacker and defender weapons
- whether each selected weapon would comply

Suggested markers:

- `[WarStance][Combat]`
- `[WarStance][HeatTier]`
- `[WarStance][Arsenal]`
- `[WarStance][WouldSelect]`

Pass criteria:

- each combat reports one effective heat and stance
- defenders are included
- AI-versus-AI and AI-versus-player combat combinations are observed
- low heat reports Hands Only without checking war age
- no popup or weapon behavior changes

## Phase 2: Weapon Classification

Create one centralized classifier:

- unarmed
- melee or tool
- sidearm
- long gun
- automatic or heavy
- unknown

Use:

- fists/default weapon identity
- `WeaponConfig.firearm`
- `WeaponConfig.tool`
- explicit resource-ID overrides

Unknown firearms should default conservatively to Open Arsenal only. Unknown non-firearms should not silently qualify as unarmed.

Log every unknown weapon ID once.

Pass criteria:

- all vanilla and After Prohibition weapons resolve to a deliberate category
- custom weapon IDs do not fall through silently
- classification is reused by combat warnings, AI selection, and combat validation

## Phase 3: Shadow Heat Resolver

Resolve the heat stance at every combat without enforcing it.

For each combat:

1. read attacker-to-defender heat
2. read defender-to-attacker heat
3. choose the higher effective heat
4. map it to Hands Only, Street Weapons, Sidearms, or Open Arsenal
5. inspect both vehicle arsenals
6. retain vanilla weapon behavior
7. log which weapons would have been selected under the heat stance

Suggested markers:

- `[WarStance][HeatTier]`
- `[WarStance][WouldSelect]`
- `[WarStance][WouldViolate]`

Pass criteria:

- reverse attack direction resolves the same effective heat
- simultaneous combats resolve independently
- save and reload require no stance migration
- heat changes immediately alter the next combat stance

## Phase 4: AI Compliance Without Player Popup

Enable stance-aware selection for AI-versus-AI combat only.

Resolve the best allowed weapon for:

- the AI attacker
- the AI defender
- every eligible grouped-combat participant

Do not remove weapons from inventories.

Fallback order:

1. best weapon allowed by the stance
2. fists/default weapon
3. vanilla best weapon only if no valid default weapon exists, with an error marker

Keep all player-involved combat on vanilla/Open Arsenal behavior during this phase.

Pass criteria:

- both AI attackers and defenders comply
- low-heat AI-versus-AI combat uses fists regardless of existing weapons
- later combat changes tier as heat crosses thresholds
- low-heat wars produce brawls
- high-tier weapons remain in vehicle inventories
- grouped combat does not reintroduce prohibited weapons
- combat results and grapevine text report the weapon actually used

## Phase 5: Player Heat Stance

Apply the current heat stance to combat involving the player.

Player offensive flow:

1. the player opens the combat plan
2. resolve and display the current heat stance
3. keep every weapon visible and selectable
4. warn if the player selects a weapon above the stance
5. allow the player to continue and accept penalties

Player defensive flow:

1. an AI attack reaches `CombatManager.PerformAICombat`
2. resolve the current heat stance
3. choose the AI attacker's best compliant weapon
4. choose the player's best compliant automatic defensive weapon
5. complete combat normally

Do not add a negotiation popup and do not hold or replay incoming attacks.

Pass criteria:

- low-heat combat defaults to Hands Only for player offense and defense
- AI participants honor the current heat stance
- a player can deliberately violate the current heat stance
- canceling the player combat popup changes no state
- attack costs and damage resolve through the normal path
- crossing a heat threshold changes the next combat stance

## Phase 6: Player Warning Without Enforcement

Keep all player weapons selectable.

In the combat popup:

- identify selections above the active stance
- add a clear stance-violation warning to the selected method or nearby combat summary
- require one confirmation when the player presses Fight
- do not penalize on selection change or cancellation

Run the violation path in shadow mode first:

- log the heat increase
- log the relationship penalty
- log the resulting stance
- do not apply them

Pass criteria:

- legal weapons do not show a warning
- prohibited weapons remain usable
- canceling produces no violation
- grouped attacks produce one proposed violation

## Phase 7: Apply Player Violation Consequences

Enable real consequences after the warning path is proven.

Recommended first values:

- melee used during Hands Only: `+10` war heat
- sidearm used below Sidearms: `+20` war heat
- long gun or automatic weapon used below Open Arsenal: `+30` war heat
- one relationship penalty per transaction
- added heat determines the next combat's stance
- repeated violations can push heat into Open Arsenal

Keep values configurable.

Use the weapon that actually reached `PerformCombat`, not the last dropdown selection.

Pass criteria:

- the player can complete the prohibited attack
- the violation is recorded once
- heat and relationship penalties apply once
- the next combat uses the stance produced by the new heat value
- normal attack and kill heat still work

## Phase 8: Player Defense Compliance

When AI attacks the player:

- resolve the current heat stance for both sides
- choose the player's best stance-compliant defensive weapon automatically
- do not remove or consume prohibited weapons
- do not record a player violation
- show the actual defensive weapon in combat results

Pass criteria:

- low-heat incoming attacks resolve normally with fists
- higher-heat incoming attacks use the appropriate allowed tier
- player defenders obey the same stance
- automatic defense never causes an involuntary violation
- fists are available as the final fallback

## Phase 9: AI Violations

Add bounded AI stance-violation behavior behind a config gate.

Start with a low maximum violation chance. A normal low-heat outfit should strongly prefer compliance.

When the AI violates the current stance:

- choose an otherwise valid prohibited weapon from its own vehicle
- apply the same heat and relationship rules
- notify the player if involved
- log the decision factors and deterministic roll

Suggested markers:

- `[WarStance][AIViolationRoll]`
- `[WarStance][StanceViolated]`
- `[WarStance][StanceEscalated]`

Pass criteria:

- AI violations are uncommon at low heat
- violations become more likely after deaths or opponent violations
- peaceful outfits remain more compliant
- the AI cannot use a weapon absent from its vehicle

## Phase 10: Enemy Sit-Down Cooling

Add diagnostics before changing heat:

- detect when the boss Crew Relations sit-down section opens
- report current directional heat and effective stance
- report each eligible outfit and available cooling options
- report player and enemy outfit power
- report the enemy's acceptance score and deterministic roll
- report AI offer eligibility and Peaceful or Wary posture
- report the expected heat and stance after each option

Suggested markers:

- `[WarStance][SitDownOpened]`
- `[WarStance][CoolingOption]`
- `[WarStance][ProposalAccepted]`
- `[WarStance][ProposalDeclined]`
- `[WarStance][AIOfferCreated]`
- `[WarStance][AIOfferDisplayed]`
- `[WarStance][AIOfferAccepted]`
- `[WarStance][AIOfferDeclined]`
- `[WarStance][AIOfferExpired]`
- `[WarStance][CoolingCompleted]`
- `[WarStance][CoolingBlocked]`

Add one shared helper:

- `ReduceWarHeatBothWays(firstPid, secondPid, amount, reason)`

The helper should:

1. resolve the correct GangOps channel for each existing directional record
2. subtract from both directions
3. clamp at zero
4. remove zero-value records
5. log before and after values
6. leave aggro and truce state unchanged

Implement player-initiated options in this order:

1. plain talk
2. `$1,000` leisure sit-down
3. `$3,000` liquor business deal

Then add AI-initiated offers:

1. Peaceful Talk
2. Wary Talk
3. AI-funded Leisure

Use the boss Crew Relations **Outfit Sit-Downs** area as the only player action surface. Do not route through a physical `VisitState` or require an enemy peep to be on the same node.

Do not use the deferred AI-human robbery response popup architecture. AI interest must create passive pending-offer records that the existing Crew Relations refresh path reads. No sit-down modal, popup queue, active-popup lock, or forced focus change should occur.

Persist only the state required for:

- pair proposal cooldown
- last deterministic player-proposal result
- pending AI offer type, posture, creation day, and expiry day
- last declined AI offer

Do not persist UI objects or Unity references.

Pass criteria:

- the boss Crew Relations tab lists every eligible hostile outfit
- non-boss crew do not show outfit-level sit-down controls
- player proposals require no physical contact
- stronger enemies decline more often than weaker enemies under otherwise equal conditions
- a pending offer is clearly marked on the correct outfit row
- Peaceful and Wary AI postures display correctly in Outfit Sit-Downs
- AI offers never open an unsolicited popup or move input focus
- AI offers contain only Talk or Leisure
- AI-funded Leisure is unavailable when the outfit safehouse is missing or vanquished
- AI-funded Leisure requires at least `$4,200` in the outfit safehouse before payment
- AI-funded Leisure leaves at least `$3,200` in the outfit safehouse after payment
- the player can propose the liquor deal
- AI cannot offer the liquor deal
- each successful option reduces both heat directions exactly once
- player Leisure payment occurs before heat reduction
- player Liquor Deal payment and safehouse resource grants occur before heat reduction
- AI Leisure payment occurs before heat reduction
- failure or cancellation does not change heat
- declined proposals do not charge Leisure cash
- only one pending offer exists per outfit pair
- accepting, declining, expiry, defeat, truce, or zero heat removes the offer from the list
- save and load restores a valid pending offer without duplicating it
- the next combat immediately uses the newly calculated heat stance
- ordinary business purchases do not cool war heat

## Phase 11: Heat Tuning And De-escalation

Only after the core loop is stable, allow:

- threshold tuning by campaign difficulty
- slower heat decay while active attacks continue
- stance reset or reduction after a truce
- short no-firearm grace periods after truces

The normal rule remains that current effective heat selects the stance for every combat.

## Validation Matrix

Test each behavioral phase across:

1. AI attacker versus AI defender.
2. AI attacker versus player defender.
3. Player attacker versus AI defender.
4. Single-member combat.
5. Grouped on-foot combat.
6. Drive-by or grouped vehicle combat.
7. Hands Only with firearms present in both vehicles.
8. Street Weapons with only firearms and fists available.
9. Sidearms with sidearms and long guns present.
10. Open Arsenal.
11. Player cancellation after selecting a prohibited weapon.
12. Player confirmation and actual prohibited use.
13. Save and reload at low heat.
14. Save and reload above each threshold.
15. Save and reload after a violation.
16. Two simultaneous conflicts with different heat tiers.
17. Heat decay crossing downward through a threshold.
18. Player Talk proposal to a weaker enemy at heat `28`.
19. Player Talk proposal to a stronger enemy with the same personality and heat.
20. Player-funded Leisure proposal at heat `58`.
21. Player-funded `$3,000` Liquor Deal at heat `82`.
22. Liquor Deal failure caused by insufficient cash or unavailable safehouse delivery.
23. Peaceful AI Talk offer.
24. Wary weaker-outfit AI Leisure offer.
25. Confirm AI never offers the Liquor Deal.
26. Strong aggressive AI declining or not offering a sit-down.
27. Player declining an AI offer.
28. Canceling each sit-down option.
29. Repeating a successful or declined sit-down during cooldown.
30. Opening Outfit Sit-Downs while non-boss crew is selected.
31. AI creates an offer while Crew Relations is closed; confirm no popup appears.
32. Open Outfit Sit-Downs and confirm the offering gang, posture, and action are marked.
33. Save and reload with one pending AI offer.
34. Accept and decline offers only from Outfit Sit-Downs.

For each phase:

1. Build `GameplayTweaks\GameplayTweaks.csproj` in Release.
2. Inspect live `Player.log` and `Player-prev.log`.
3. Test a new campaign and an existing save.
4. Confirm inventories are unchanged.
5. Confirm combat results report the actual chosen weapon.
6. Confirm grouped combat applies one stance-violation penalty.
7. Confirm disabling the feature restores current weapon behavior.

## Stop Conditions

Stop and investigate before proceeding if:

- the player's prohibited weapons disappear from inventory or dropdowns
- selecting and canceling creates a penalty
- one grouped attack creates multiple stance violations
- attackers comply but defenders still use their best unrestricted weapon
- an automatic player defense records a player violation
- reverse attack direction resolves a different effective heat
- any war-start or war-end patch is required for stance correctness
- canceling a player attack changes heat or stance state
- a low-heat incoming attack resolves with a firearm
- attack costs are charged twice
- one pair's heat tier leaks into another combat
- AI combat fails when no allowed inventory weapon exists
- a failed or canceled sit-down reduces heat
- only one directional heat record is reduced
- a cheap ordinary shop purchase reduces heat
- non-boss crew can initiate outfit sit-downs
- remote sit-downs require a physical enemy peep or shared node
- an AI offer includes the liquor deal
- a player Liquor Deal cools heat before cash and resources complete
- an AI-funded Leisure offer spends player cash
- a declined player Leisure proposal still charges cash
- a stronger enemy is easier to persuade than a weaker identical enemy
- an AI sit-down popup appears at all
- an offer row omits whether the outfit is Peaceful or Wary
- a pending offer duplicates, appears on the wrong outfit, or survives invalidation
- an AI-funded Leisure offer reduces heat after its treasury revalidation fails
- a cooling action clears aggro or creates a truce
- current robbery, retaliation, truce, or grouped-combat flows regress

## Recommended First Release

The first public release should include:

- stateless combat-time heat resolution
- complete weapon classification
- Hands Only, Street Weapons, Sidearms, and Open Arsenal
- AI attacker and defender compliance
- heat-driven stance selection for every combat
- player warning and confirmation
- player freedom to violate the current stance
- one violation penalty per combat transaction
- automatic compliant player defense
- hostile plain-talk cooling
- hostile `$1,000` leisure cooling
- player-initiated `$3,000` liquor-deal cooling
- boss Crew Relations outfit selector
- player proposal acceptance based on relative outfit strength
- Peaceful and Wary AI sit-down offers
- AI-initiated Talk or AI-funded Leisure only
- passive pending offers in Outfit Sit-Downs with no unsolicited popup
- bilateral heat reduction with a cooldown
- config switch restoring current behavior

Do not include initially:

- custom pair save state
- war-start or war-end detection
- AI stance violations
- negotiation UI
- AI-initiated liquor-deal cooling
- general AI-to-player conversation resource delivery
- black-market weapon supply
- physical gun shops
- ammunition or weapon durability
- separate crew-by-crew stances
- confiscation or removal of prohibited weapons
