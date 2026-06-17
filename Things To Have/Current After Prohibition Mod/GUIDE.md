# Current After Prohibition Mod Guide

## Public Release Status

This package is the current public After Prohibition Beta v0.3 build for the v1.3.98 package line. It includes StreamingAssets content plus staged BepInEx plugins for the active runtime systems.

The June 17, 2026 v0.3 pass adds heat-based weapon stances, enemy outfit sit-down cooling, AI sit-down offers, and robbery/front-closure heat links for gang wars. Players can send a normal `Player.log` first; detailed diagnostics can be enabled later only if a bug needs a deeper repro.

The older stable guide at `Things To Have\Old Stable\#Guide.txt` remains a legacy content reference and was not removed.

For the full gameplay explanation, read `SYSTEMS_GUIDE.md` in this folder.

## Install

1. Open the City of Gangsters install folder.
2. Copy this release folder's `BepInEx` contents into the game's `BepInEx` folder.
3. Copy this release folder's `CoG_Data` contents into the game's `CoG_Data` folder when the release includes StreamingAssets changes.
4. Install `CoGCustomAssets` for this build. Some current icons and asset references expect `CoGCustomAssets` to be present.
5. Copy this release folder's `Custom Maps` contents when custom map files are included.
6. Start the game and check `BepInEx\LogOutput.log` or the City of Gangsters `Player.log` if something does not load.

## Important Requirements

- Do not install this as a DLL-only update. The current build expects the included `CoG_Data\StreamingAssets` files to be installed.
- `CoGCustomAssets` is required for now because this build references custom icons/assets through that stack.
- `CoGCustomAssets` should be installed under `BepInEx\plugins`, usually as `BepInEx\plugins\CoGCustomAssets\CoGCustomAssets.dll`.
- If maps or new-game setup do not load correctly, first confirm that `CoG_Data\StreamingAssets` was copied and that `CoGCustomAssets` is installed.

## Included Runtime DLL

- `BepInEx\plugins\GameplayTweaks.dll`
- `BepInEx\plugins\AfterProhibitionAssets.dll`
- `BepInEx\plugins\AfterProhibitionCompatibility.dll`
- `BepInEx\plugins\AfterProhibitionEconomy.dll`
- `BepInEx\plugins\AfterProhibitionFamily.dll`
- `BepInEx\plugins\AfterProhibitionPolitics.dll`
- `BepInEx\plugins\AfterProhibitionRoutes.dll`
- `BepInEx\plugins\AfterProhibitionUI.dll`

This DLL is staged from the repo release build. For local testing, copy the staged DLL into the live game install until symlinks are restored.

The standalone After Prohibition plugins split assets, compatibility, economy, family, politics, routes, and UI behavior away from `GameplayTweaks` so each system can be debugged and disabled more safely. `GameplayTweaks` is still required; the split plugins currently delegate only proven slices and leave fallback behavior in place.

`AfterProhibitionUI` owns UI-only cleanup and rendering surfaces such as stale crew-pick portraits, connection-card render guards, crew HUD refresh scheduling, aggro crew-pick refresh scheduling, popup docking, menu retheme bridging, crew inspect footer buttons, and crew jail visuals. Gameplay behavior remains in `GameplayTweaks` or the owning split plugin.

## StreamingAssets Content

This release also includes updated `CoG_Data\StreamingAssets` content. Do not treat it as a DLL-only update.

Main content changes included in the package:

- legal-front business data for grocery, restaurant, barber, florist, funeral, cafe, hardware, laundry, car dealership, real estate, wholesale, contractor, gas station, jewelry, hotel, liquor, pub, movie theater, pawnshop, law office, record, film, gym, talent, and money-laundering fronts;
- bank-counter and employment-agency style front modules;
- updated business seeding so some fronts use legal modules, bank counters, delis/home-booze sources, and newer tobacconist content instead of generic placeholder storage;
- controlled bank map placement and more reachable funeral-home starter fronts;
- connected-connector fake-account scheme work through funeral homes and churches;
- rail shipment quests for bulk liquor movement through train stations;
- school support, church support, church loan, and church repayment conversation paths;
- pact, cop-killing, politics-protection, and gang-loot relationship/heat buffs;
- AI, people, skills, schemes, gambling, and economy retuning for the newer legal-front and backroom systems;
- city map generation updates for schools and business/family density.

When installing a public build, copy the `CoG_Data` folder from this package along with `BepInEx` so these data changes are present.

## StreamingAssets Roadmap Status

The planning docs in `docs\streaming-assets-plans` are not all finished features. v0.3 includes visible content and gameplay systems from that roadmap, while the larger ladders remain planned.

Finished or visible by v0.3:

- Bank-counter/front content and controlled bank placement support.
- Train-station bulk liquor shipment quest chains.
- School support and church support/loan conversation paths.
- Funeral-home/church fake-account paperwork style content.
- Legal-front module expansion across many business types.
- Gambling data updates for gym naming support and loan shark debtor slot caps.
- Civic nonpurchase fallback support so schools and similar civic content can still expose actions when purchase data is missing.
- Illegal backroom runtime fallback support for respect, territory influence, and production/consumption edge cases.
- Heat-based weapon stances, outfit sit-down cooling, passive AI sit-down offers, and robbery/front-closure heat links.

Still planned after v0.3:

- Full municipal bank behavior with stronger city-controlled/non-buyout handling.
- Expanded `45k` and `100k` connected drug and liquor deal tiers.
- Offshore account discount handling if mixed dirty/clean payment math needs code.
- School passive-income and recruit-muscle follow-through if recurring payments need code.
- Maritime dock contracts, port security bribes, jewelry/ore smuggling, dock contacts, and union progression.
- Ranked schemes such as mini vacations, fundraisers, weapon searches, hijacks, larger burglaries, and random business opportunity quests.
- Any patch fallback that proves necessary after the content-only versions are tested.

## Quick Start

- Use normal City of Gangsters play as the base.
- Crew members now have more long-term identity through loyalty, happiness, odd jobs, spouse/family hooks, snitch risk, and street credit.
- Pacts and independent gangs can build pressure, retaliate, protect turf, expand, rob weak or exposed crews, and respond to refusals through important-business pressure or rare quick attacks.
- Legal-front businesses, banks, schools, churches, train stations, and map generation are also changed through StreamingAssets data.
- Safehouse and owned-building storage follow stricter vehicle-position rules.
- Delivery routes can optionally try to expand collection fronts with the compact `+` toggle on `Collect front` steps.

## Legal Protection In Crew Relations

- `Political Bribe` costs `$10,000` in clean safehouse cash and protects the whole outfit for 180 days. It delays federal arrest pressure and unlocks a `$5,000` `Call In Favor` option during active police trouble. The favor can cool the involved precinct or end the remaining cop hostility.
- `Bribe Judge` protects only the selected crew member. It requires local heat, costs `$2,080` at Low heat, `$4,160` at Medium, or `$8,320` at High, prevents that member's federal arrest countdown from progressing, and adds a 25% case-dismissal chance. It is consumed after a successful dismissal.
- Lawyer controls appear when the selected crew member is jailed. Use `Add $1k` or `Add $10k` to fund that member's retainer from clean safehouse cash.
- A lawyer balance of at least `$1,000`, `$2,000`, or `$3,000` adds a 10%, 20%, or 30% case-dismissal chance during eligible legal updates.
- Turn `Retainer Confirmed` on to authorize active trial representation. A confirmed balance above `$20,000` gives a separate 60% chance to mark the trial paid off.
- Confirmed retainers lose `$500` per jail upkeep update while below `$20,000`, or `$1,000` while at or above `$20,000`. Confirmation turns off when the balance reaches zero.

## Robbery And Front Pressure

- Enemy outfits can try robbery/extortion when a human crew is exposed in enemy territory, when the player looks weak to a nearby outfit, or when local pressure says the outfit should test the player.
- Peaceful outfits should generally avoid robbery pressure, while aggressive and expansion-minded outfits are more willing to use robbery, important-business pressure, or quick attacks.
- The player can leave robbery prompts enabled, or open the boss crew relations menu and switch `Robbery: Prompt` to `Robbery: Auto Evade`.
- Prompt mode lets the player manually pay, use a favor when available, evade, or refuse.
- Auto Evade mode suppresses the prompt and lets crews use the standing order automatically. Failed evasion can still cost money or trigger retaliation.
- Robbery pair cooldowns are six months, so the same robber outfit should not immediately keep trying the same human target after a resolved attempt.
- Successful refusals can lead to important shop closures or, more rarely, a coordinated attack warning for the next turn.
- Important-business closures target valid visible shops tied to the defender's territory or economic activity. Closed shops should block buying and selling until reopened by game rules.
- Normal AI expansion and war pressure still exists alongside robbery. Robbery is an added pressure path, not the only reason outfits attack fronts or businesses.
- AI robberies and completed front closures now add war heat, which can raise the future weapon stance for that outfit pair.

## Combat And Vehicle Crews

- Vehicle attacks can include confirmed passengers in the target vehicle, so the attack popup can focus one passenger or spread damage across multiple occupants.
- The combat popup includes `Attack Type`, `On-Foot Crew`, `Range`, and `Targets` controls when the situation supports them.
- `Attack Type` switches between drive-by and on-foot behavior when available.
- `On-Foot Crew` selects how many available attackers join an on-foot attack.
- `Range` filters on-foot attacks between any, melee, and ranged weapons.
- `Targets` switches between focused targeting and spread targeting when multiple enemy targets are available.
- Passenger and driver routing remains strict for movement so future passengers should not be treated as already present attackers.

## War Weapon Stances

- Gang fights now use the current war heat between the two outfits to choose a weapon stance.
- `Hands Only` allows fists.
- `Street Weapons` allows fists and melee weapons.
- `Sidearms` allows melee weapons plus pistols and revolvers.
- `Open Arsenal` allows the full valid vehicle weapon pool.
- The stance is recalculated for every fight from current heat; there is no separate war-start agreement to manage.
- Player weapons stay selectable. If you choose a weapon above the current stance, the popup warns you before the fight commits.
- If you confirm and use the prohibited weapon, the opposing outfit gains extra war heat against you and receives a relationship penalty.
- AI attackers and defenders normally follow the stance, but AI-vs-AI fights can sometimes breach one tier when heat, pressure, personality, and available vehicle weapons support it.
- AI breaches use only weapons already in that vehicle. They do not unlock weapons, buy from gun shops, or change weapon supply.

## Outfit Sit-Downs

- Select the boss and open Crew Relations to find outfit-level sit-down controls.
- Outfit sit-downs are remote; you do not need to physically meet the enemy boss.
- Player options can include Talk, Leisure, and the liquor business deal when requirements are met.
- Successful sit-downs reduce war heat in both directions and can lower the next combat stance naturally.
- Enemy outfits can offer Talk or Leisure sit-downs. These offers appear in Crew Relations instead of opening a forced popup.
- AI outfits cannot offer the liquor deal to the player in this release.
- Failed, unavailable, declined, or canceled sit-downs should not reduce heat.

## Public Logging And Diagnostics

- Normal logs keep compact breadcrumbs for robbery prompts, robbery resolutions, front/business closure outcomes, combat commits, route problems, forced shop locks, and true anomalies.
- Detailed verification logs are disabled by default. Enable `Diagnostics.EnableVerboseVerificationLogs=true` only when a compact log is not enough.
- Area-specific diagnostics can be enabled with `Diagnostics.EnableRobberyDiagnostics`, `Diagnostics.EnableFrontPressureDiagnostics`, `Diagnostics.EnableVehicleAuthorityDiagnostics`, `Diagnostics.EnableVehicleCombatDiagnostics`, and `Diagnostics.EnableCompatibilityDiagnostics`.
- Detailed performance telemetry is disabled by default. Enable `Diagnostics.EnablePerformanceDiagnostics=true` only when profiling a slowdown.
- Routine Economy batch progress and mutation-detail logs are disabled by default through `AfterProhibitionEconomy` diagnostics. Use `Diagnostics.EnableRuntimeEconomyProgressLog=true` or `Diagnostics.EnableRuntimeEconomyRoutineLog=true` only for economy repros.
- Always-on Unity errors and compact gameplay breadcrumbs remain useful for first bug reports. Do not ask players to rerun with verbose logs unless the first log does not contain enough evidence.

## Content To Look For

- Train stations can offer bulk rail shipments when you have the right storage/territory setup.
- Schools can start a support path that can lead into a payout and a short local recruiting window.
- Churches can start support and loan paths, including repayment.
- Connected connector crew can start fake-account paperwork schemes through funeral homes or churches when they have paper, ink, and cash.
- Legal fronts now matter more because data files give more businesses real front modules and upkeep hooks.
- Banks and employment-style fronts have more specific modules and resource roles.
- Maps should generate large schools and controlled bank locations through the current civic-style placement lists while leaving more room for businesses on most updated cities.

## Storage Rules

- Vehicle at safehouse: safehouse resources can be accessed normally.
- Vehicle not at safehouse: safehouse resources should not be transferable for that vehicle.
- Vehicle at owned business: owned-building storage can be accessed normally.
- Vehicle traveling to that exact owned storage target: transfer can remain available for that intended action.
- Vehicle leaves the corner or targets another corner: storage may remain visible for context, but transfer buttons should be disabled.

## Delivery Route Front Expansion

- Open a delivery route step that collects from a front.
- Use the compact `+` marker to enable expansion for that route step.
- When the route successfully collects from that front, the mod can ask the vanilla expansion system to start an expansion.
- Expansion still requires a valid next corner and enough vehicle cash.
- If no legal expansion corner exists, the route continues and the log records the skip.

## Route Split Ownership

- `AfterProhibitionRoutes` is installed as a separate plugin, but `GameplayTweaks` remains required.
- `AfterProhibitionRoutes` currently owns read-only route state audits and route decision bridges.
- `GameplayTweaks` reads those route decisions and keeps the mutating fallback behavior active.
- Full route behavior ownership is still false for travel continuation, vehicle node authority, delivery-route pump, and route-simulated access.
- This means the current route split should improve diagnostics and prevent double-ownership without removing the existing GameplayTweaks route fixes.

## Route-In Shop Staging

- Old/departure businesses should stop allowing buy/sell once the vehicle leaves that corner.
- The committed next-destination business can open route-simulated business conversation and stage buy/sell while the vehicle is en route when `VehicleRouteSimulation.EnableRouteSimulatedConvenienceActions=true`.
- Shift-clicking the committed next-destination business is supported as an intentional route-in opener for the selected vehicle.
- Staged buy/sell does not move cash, goods, vehicle cargo, or shop stock until the vehicle physically arrives at that destination or a validated shop frontage/access node.
- More than one different resource or buy/sell direction can be staged for the same vehicle destination; staging the same shop/resource/direction again updates that matching order.
- Destination glow and automatic pop-ups are not used as arrival proof.

## Useful Log Markers

- `route-command-deferred` means a route command waited because the vehicle was still arriving.
- `routes-baseline` and `route-state-audit` show the read-only `AfterProhibitionRoutes` bridge loaded.
- `travel-continuation`, `vehicle-node-authority`, `delivery-route-pump`, and `route-sim-access` show `AfterProhibitionRoutes` decision classifications.
- `Routes behavior fallback active` means `GameplayTweaks` is still correctly handling route mutation because `AfterProhibitionRoutes` does not yet own that behavior slice.
- `FrontRouteExpand` shows route expansion arming, skip, and success details.
- `routeSimConvenienceEnabled=True/False` in the startup log shows whether route-in destination conversations and staged shop orders are enabled.
- `route-in-shift-destination-open` shows the Shift-click route-in opener accepted the selected vehicle's current destination.
- `route-sim-convo`, `route-shop-stage-opened`, `route-shop-staged`, and `route-shop-committed` show route-in shop staging working.
- `safehouse-access-crew-filtered` shows strict safehouse access filtering.
- `scope-wrong-corner-presence-blocked` shows wrong-corner preview rejection.
- `[PoliticsStarter] delegated owner=AfterProhibitionPolitics` shows `GameplayTweaks` has handed the New York starter quest fallback to `AfterProhibitionPolitics`.
- `uiRetheme=delegated`, `crewHudRefresh=delegated`, and `aggroUiRefresh=delegated` show `GameplayTweaks` is using the `AfterProhibitionUI` bridge for migrated UI surfaces.
- `CrewHUD refresh ... owner=AfterProhibitionUI`, `AggroUI ... owner=AfterProhibitionUI`, and `PopupDocking ... owner=AfterProhibitionUI` show the UI split is active.
- `scheduler=AfterProhibitionUI fallback=GameplayTweaks` means `AfterProhibitionUI` owns the visible aggro refresh scheduler while `GameplayTweaks` still provides the gameplay-coupled reconciliation fallback.
- `politics-audit`, `bribe-state-audit`, `judge-law-office-audit`, and `campaign-election-audit` show read-only politics diagnostics.
- `[Loyalty]` shows crew relation turn updates.
- `[OddJob]` shows odd-job pay.
- `[GangOps.Pact]` and `[GangOps.Independent]` show pact and gang automation.
- `[CopKilling]` shows cop-war safety cleanup.
