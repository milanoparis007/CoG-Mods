# Compatibility Matrix (Stable-Core Profile)

## Runtime path rule
- The game loads plugins only from:
  - `...\City of Gangsters\BepInEx\plugins`
- Staging path example:
  - `C:\Users\User\Documents\COG Modding Stuff\Notes\New folder`
  - This is a source/staging folder and is not runtime-loaded.

## Stable-core allowed
- `com.mods.gameoptimizer` (keep only one optimizer active)
- `com.pia.cityofgangsters.uienhancer` / `10K Button & Input.dll`
- `com.pia.tickerenhancer` / `OutpostTickerMod.dll` (optional duplicate suppression)
- `com.cogmod.dirtycasheconomy`
- `com.pia.dirtycashvolumefix`

## Allowed with guard (cheat core only)
- `com.pia.cogcheat` / `Cog Ultimate Cheat.dll`
- Guard mode:
  - GameplayTweaks stays authoritative for heat/trial/war/pact logic.
  - Election/manager cheat variants remain blocked in stable profile.

## Pact-first native mode (default)
- `com.pia.gangwars`
- `com.modder.territoryautoexpand`
- `com.pia.mafiahierarchy`
- custom icons/portrait stack (`CoGCustomAssets.dll`, `CustomPortraits.dll`, trait icon limit plugins)
- Default behavior:
  - Native Gang Ops systems are authoritative (`AutoProtect`, `Coordinated Attacks`, `Revenge`, `WarHeat`, AI hire automation).
  - Gang Ops runs dual-channel:
    - `Pacts` channel for AI gangs in active pacts
    - `Independent` channel for AI gangs not in an active pact
  - GangWars adapter takeover paths are fallback-only and disabled by default.
  - Territory expansion channels are config-gated and default OFF:
    - gang expansion OFF
    - player auto-expand OFF
    - outpost auto-expand OFF
  - GameplayTweaks UI theme remains active for GameplayTweaks-owned menus.

## Blocked cheat variants (hard block profile)
- `com.pia.electionmanager` / `ElectionVoteCheat.dll`
- `com.pia.bossmanager` / `Boss Manager.dll`

## Launcher coexistence
- Bridge launcher GUID remains:
  - `com.mods.modlauncher`
- Bridge DLL output:
  - `ProhibitionLauncher.dll`
- External launcher GUID:
  - `com.pia.modlauncher`
- Dependent mods use soft dependency on both GUIDs.

## Operational defaults
- Retaliation war reconciliation stays active when GangWars is detected (pact-first authority).
- Territory visual overrides stay active with territory/gang-war mods (pact color precedence).
- UI re-theme stays active for GameplayTweaks-owned popups/buttons.
- Ticker duplicate suppression is optional and disabled by default.
- Hiring at business/residence assignments is allowed by default (`CrewHiring.AllowBusinessAssignedCandidates=true`).
- Gang Ops HUD is available outside crew management (`Gang Ops` button and `F10`), with `Pacts` and `Independent` tabs.

## Pact-first compatibility config (GameplayTweaks)
- `Compatibility.EnableGangWarsTributeSystems=false`
- `Compatibility.EnableGangTerritoryExpansion=false`
- `Compatibility.EnablePlayerAutoExpandTerritory=false`
- `Compatibility.EnableOutpostAutoExpand=false`
- `Compatibility.EnableGangWarsPactAdapter=false` (fallback mode)
- `Compatibility.ReplaceGangWarsAlliancesWithPacts=false` (fallback mode)
- `Compatibility.UseGangWarsColorStyleForPacts=false` (fallback mode)

## Gang Ops defaults (per-save seed)
- `PactOpsDefaults.Enabled=true`
- `PactOpsDefaults.AutoProtectEnabled=true`
- `PactOpsDefaults.CoordinatedAttackAutoEnabled=true`
- `PactOpsDefaults.RevengeEnabled=true`
- `PactOpsDefaults.WarHeatAttackGain=22`
- `PactOpsDefaults.WarHeatKillGain=55`
- `PactOpsDefaults.WarHeatDecayPerWeek=6`
- `PactOpsDefaults.HireAutomationEnabled=true`
- `PactOpsDefaults.TopGangCrewBonusMin=18`
- `PactOpsDefaults.TopGangCrewBonusMax=28`
- `PactOpsDefaults.MidGangCrewBonus=8`
- `PactOpsDefaults.BottomGangCrewBonus=3`
- `PactOpsDefaults.PactCrewBonus=4`
- `GangOpsDefaults.Independent.Enabled=true`
- `GangOpsDefaults.Independent.AutoProtectEnabled=true`
- `GangOpsDefaults.Independent.CoordinatedAttackAutoEnabled=true`
- `GangOpsDefaults.Independent.RevengeEnabled=true`
- `GangOpsDefaults.Independent.WarHeatAttackGain=14`
- `GangOpsDefaults.Independent.WarHeatKillGain=35`
- `GangOpsDefaults.Independent.WarHeatDecayPerWeek=10`
- `GangOpsDefaults.Independent.HireAutomationEnabled=true`
- `GangOpsDefaults.Independent.TopGangCrewBonusMin=16`
- `GangOpsDefaults.Independent.TopGangCrewBonusMax=24`
- `GangOpsDefaults.Independent.MidGangCrewBonus=7`
- `GangOpsDefaults.Independent.BottomGangCrewBonus=3`
- `GangOpsDefaults.Independent.PactCrewBonus=0`
