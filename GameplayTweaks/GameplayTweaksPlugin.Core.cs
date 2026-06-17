using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin : BaseUnityPlugin
{

	private void Awake()
	{
		Instance = this;
		BindConfigEntries();
		if (IsNoOpBootstrapMode())
		{
			LogLoadedBuildBanner();
			TryInitializeLeanTweenCapacity();
			VerificationLog("Compat", "no-op bootstrap active; skipping Harmony patch registration and subsystem initialization");
			return;
		}
		Harmony harmony = new Harmony("com.mods.gameplaytweaks");
		ApplyHarmonyPatches(harmony);
		InitializeSubsystems();
	}

	private static bool IsNoOpBootstrapMode()
	{
		return !(EnableCoreAndRelationshipFeatures?.Value ?? false)
			&& !(EnableCrewRelationsMenuFeatures?.Value ?? false)
			&& !(EnableUiAndInteractionFeatures?.Value ?? false)
			&& !(EnableCombatAndGangOpsFeatures?.Value ?? false)
			&& !(EnableCompatibilityAndWorldFeatures?.Value ?? false)
			&& !(EnableVehicleAndPoliticsFeatures?.Value ?? false);
	}

	private void BindConfigEntries()
	{
		EnableSpouseEthnicity = ((BaseUnityPlugin)this).Config.Bind<bool>("SpouseEthnicity", "Enabled", true, "Spouses share ethnicity.");
		SpouseEthnicityChance = ((BaseUnityPlugin)this).Config.Bind<float>("SpouseEthnicity", "SameEthnicityChance", 0.8f, "Probability same ethnicity.");
		EnableHireableAge = ((BaseUnityPlugin)this).Config.Bind<bool>("HireableAge", "Enabled", true, "Change min hire age.");
		HireableMinAge = ((BaseUnityPlugin)this).Config.Bind<float>("HireableAge", "MinAge", 18f, "Minimum age to hire.");
		MarriageMinAge = ((BaseUnityPlugin)this).Config.Bind<int>("Marriage", "MinAge", 18, "Minimum marriage age.");
		MarriageMaxAgeDiff = ((BaseUnityPlugin)this).Config.Bind<int>("Marriage", "MaxAgeDifference", 10, "Max age difference.");
		EnableCrewStats = ((BaseUnityPlugin)this).Config.Bind<bool>("CrewStats", "Enabled", true, "Enable crew stat tracking.");
		EnableAIAlliances = ((BaseUnityPlugin)this).Config.Bind<bool>("AIAlliances", "Enabled", true, "Enable AI alliances.");
		EnableDirtyCash = ((BaseUnityPlugin)this).Config.Bind<bool>("DirtyCash", "Enabled", true, "Enable dirty cash economy.");
		// Stable GameplayTweaks profile is now the default. Disable every gate manually
		// only when you intentionally want the no-op bootstrap profile.
		EnableCoreAndRelationshipFeatures = ((BaseUnityPlugin)this).Config.Bind<bool>("FeatureGates", "EnableCoreAndRelationshipFeatures", true, "Enable the core and relationship patch set. Stable profile default: true.");
		EnableCrewRelationsMenuFeatures = ((BaseUnityPlugin)this).Config.Bind<bool>("FeatureGates", "EnableCrewRelationsMenuFeatures", true, "Enable the Crew Relations / Hideout menu slice. Stable profile default: true.");
		EnableUiAndInteractionFeatures = ((BaseUnityPlugin)this).Config.Bind<bool>("FeatureGates", "EnableUiAndInteractionFeatures", true, "Enable the UI and interaction patch set. Stable profile default: true.");
		EnableCombatAndGangOpsFeatures = ((BaseUnityPlugin)this).Config.Bind<bool>("FeatureGates", "EnableCombatAndGangOpsFeatures", true, "Enable the combat and gang-ops patch set. Stable profile default: true.");
		EnableVehicleAndPoliticsFeatures = ((BaseUnityPlugin)this).Config.Bind<bool>("FeatureGates", "EnableVehicleAndPoliticsFeatures", true, "Enable the multi-crew vehicle and politics patch set. Stable profile default: true.");
		EnableCompatibilityAndWorldFeatures = ((BaseUnityPlugin)this).Config.Bind<bool>("FeatureGates", "EnableCompatibilityAndWorldFeatures", true, "Enable the compatibility and world patch set. Stable profile default: true.");
		EnableCompactBugBreadcrumbs = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableCompactBugBreadcrumbs", true, "Keep low-noise outcome and anomaly verification breadcrumbs in normal logs so first bug reports remain useful.");
		EnableVerboseVerificationLogs = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableVerboseVerificationLogs", false, "Emit every verification log line. Leave disabled for public builds unless reproducing a bug that needs full trace detail.");
		EnablePerformanceDiagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnablePerformanceDiagnostics", false, "Emit detailed [PERF] telemetry for turn slicing, cache hits, and low-ms instrumentation. Leave disabled for public builds unless profiling a slowdown.");
		EnableRobberyDiagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableRobberyDiagnostics", false, "Emit detailed robbery/extortion verification logs beyond compact breadcrumbs.");
		EnableFrontPressureDiagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableFrontPressureDiagnostics", false, "Emit detailed front-pressure and important-business closure verification logs beyond compact breadcrumbs.");
		EnableVehicleAuthorityDiagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableVehicleAuthorityDiagnostics", false, "Emit detailed vehicle/node authority verification logs beyond compact breadcrumbs.");
		EnableVehicleCombatDiagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableVehicleCombatDiagnostics", false, "Emit detailed grouped-combat verification logs beyond compact breadcrumbs.");
		EnableCompatibilityDiagnostics = ((BaseUnityPlugin)this).Config.Bind<bool>("Diagnostics", "EnableCompatibilityDiagnostics", false, "Emit detailed compatibility verification logs beyond compact breadcrumbs.");
		EnableRouteSimulatedConvenienceActions = ((BaseUnityPlugin)this).Config.Bind<bool>("VehicleRouteSimulation", "EnableRouteSimulatedConvenienceActions", true, "Allow selected human vehicles that are actively routed to a friendly/neutral business or civic destination to open route-simulated conversations and stage shop buy/sell before physical arrival. Physical arrival remains required for inventory mutation.");
		EnableCrewOddJobs = ((BaseUnityPlugin)this).Config.Bind<bool>("CrewRelations", "EnableOddJobs", true, "Enable the Crew Relations odd job button. Odd jobs consume half of a crew member's current action points and pay a small weekly clean-cash wage.");
		DebugAddMurderWitnessHotkey = ((BaseUnityPlugin)this).Config.Bind<KeyboardShortcut>("Debug", "AddMurderWitnessHotkey", new KeyboardShortcut(KeyCode.F10, KeyCode.LeftControl, KeyCode.LeftShift), "Temporary Phase 1 debug hotkey to add one non-federal murder witness to the selected human crew member. Remove before final public push.");
		VehicleGroupCombatAllowRangedWeapons = ((BaseUnityPlugin)this).Config.Bind<bool>("VehicleGroupCombat", "AllowRangedWeapons", true, "Allow grouped player vehicle attacks to use ranged/firearm weapons from inventory.");
		VehicleGroupCombatAllowMeleeWeapons = ((BaseUnityPlugin)this).Config.Bind<bool>("VehicleGroupCombat", "AllowMeleeWeapons", true, "Allow grouped player vehicle attacks to use melee weapons from inventory. If both melee and ranged are disabled, grouped attacks fall back to fists.");
		EnableWarWeaponStances = ((BaseUnityPlugin)this).Config.Bind<bool>("WarWeaponStance", "Enabled", true, "Enable heat-driven weapon stances, player warnings, AI compliance, and stance-violation consequences. Disable to restore pre-stance combat weapon behavior without deleting saved war heat or relationship history.");
		WarWeaponStanceEnableAiBreaches = ((BaseUnityPlugin)this).Config.Bind<bool>("WarWeaponStance", "EnableAiBreaches", true, "Allow AI outfits in AI-versus-AI combat to occasionally use one weapon tier above the current heat stance. Uses only weapons already in the active combat vehicle.");
		WarWeaponStanceStreetThreshold = ((BaseUnityPlugin)this).Config.Bind<int>("WarWeaponStance", "StreetWeaponsThreshold", 25, "Effective WarHeat required to advance from Hands Only to Street Weapons. Runtime ordering is clamped below the Sidearms threshold.");
		WarWeaponStanceSidearmThreshold = ((BaseUnityPlugin)this).Config.Bind<int>("WarWeaponStance", "SidearmsThreshold", 50, "Effective WarHeat required to advance from Street Weapons to Sidearms. Runtime ordering is clamped between the Street Weapons and Open Arsenal thresholds.");
		WarWeaponStanceOpenArsenalThreshold = ((BaseUnityPlugin)this).Config.Bind<int>("WarWeaponStance", "OpenArsenalThreshold", 75, "Effective WarHeat required to advance from Sidearms to Open Arsenal. Runtime ordering is clamped above the Sidearms threshold.");
		WarWeaponStanceMeleeViolationHeat = ((BaseUnityPlugin)this).Config.Bind<int>("WarWeaponStance", "MeleeViolationHeat", 10, "WarHeat added when the player breaks a stance agreement with a melee weapon.");
		WarWeaponStanceSidearmViolationHeat = ((BaseUnityPlugin)this).Config.Bind<int>("WarWeaponStance", "SidearmViolationHeat", 20, "WarHeat added when the player breaks a stance agreement with a sidearm.");
		WarWeaponStanceHeavyViolationHeat = ((BaseUnityPlugin)this).Config.Bind<int>("WarWeaponStance", "HeavyViolationHeat", 30, "WarHeat added when the player breaks a stance agreement with a long gun, automatic weapon, or unclassified weapon.");
		CompatDisableRetaliationWarWithGangWars = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "DisableRetaliationWarWithGangWars", false, "Disable GameplayTweaks retaliation-war reconciliation when GangWars is detected.");
		CompatDisableTerritoryVisualsWithExternalTerritoryMods = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "DisableTerritoryVisualsWithExternalTerritoryMods", false, "Disable GameplayTweaks territory color overrides when external territory/gang-war visuals are detected.");
		CompatDisableUiRethemeWithExternalUiEnhancer = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "DisableUiRethemeWithExternalUiEnhancer", false, "Disable GameplayTweaks popup/button retheme when an external UI enhancer is detected.");
		CompatSuppressTickerDupesWithExternalTickerEnhancer = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "SuppressTickerDupesWithExternalTickerEnhancer", false, "Suppress duplicate grapevine/ticker-style events when an external ticker enhancer is detected.");
		CompatEnableGangWarsPactAdapter = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnableGangWarsPactAdapter", false, "Enable legacy GangWars adapter fallback hooks (diagnostics only by default).");
		CompatDisableGangWarsVassals = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "DisableGangWarsVassals", false, "Disable GangWars vassal mechanics when legacy adapter fallback mode is enabled.");
		CompatEnableGangWarsTributeSystems = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnableGangWarsTributeSystems", false, "Allow GangWars tribute systems. Default false to keep tribute disabled while GameplayTweaks pact upkeep remains active.");
		CompatEnableGangTerritoryExpansion = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnableGangTerritoryExpansion", false, "Allow external AI gang territory auto-expansion systems.");
		CompatEnablePlayerAutoExpandTerritory = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnablePlayerAutoExpandTerritory", false, "Allow external player territory auto-expansion systems.");
		CompatEnableOutpostAutoExpand = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnableOutpostAutoExpand", false, "Allow external outpost-triggered auto territory expansion systems.");
		CompatEnableHumanBackgroundTerritoryReconcile = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnableHumanBackgroundTerritoryReconcile", false, "Run a human-only background territory ownership reconcile using current respect values so corners can convert even when vanilla misses the exact threshold-crossing moment. Default false because the visual fallback path is preferred and the reconcile path is heavier.");
		CompatHumanBackgroundTerritoryBusinessRespectPercent = ((BaseUnityPlugin)this).Config.Bind<int>("Compatibility", "HumanBackgroundTerritoryBusinessRespectPercent", 100, "Extra weight applied to nearby-business respect when GameplayTweaks runs human background territory reconcile. Forced to vanilla strength to avoid delayed neutralization and sticky ownership.");
		CompatReplaceGangWarsAlliancesWithPacts = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "ReplaceGangWarsAlliancesWithPacts", false, "Legacy fallback: project GameplayTweaks pacts into GangWars alliance state.");
		CompatUseGangWarsColorStyleForPacts = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "UseGangWarsColorStyleForPacts", false, "Legacy fallback: use GangWars color hooks sourced from pact state.");
		CompatPactColorWinsOverGangWarsVisuals = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "PactColorWinsOverGangWarsVisuals", false, "Legacy fallback: force pact colors over GangWars visuals.");
		CompatUseGangWarsAggroBoostForPacts = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "UseGangWarsAggroBoostForPacts", false, "Legacy fallback: apply GangWars-style aggro boosts for pact members.");
		CompatEnableCoreCheatMenuAdapter = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "EnableCoreCheatMenuAdapter", true, "Enable compatibility adapter for core cheat menu plugin (com.pia.cogcheat).");
		CompatBlockElectionAndBossManagerCheats = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "BlockElectionAndBossManagerCheats", true, "Treat election and boss-manager cheat plugins as blocked high-risk overlap mods.");
		CompatCheatMenuKeepGameplayTweaksAuthority = ((BaseUnityPlugin)this).Config.Bind<bool>("Compatibility", "CheatMenuKeepGameplayTweaksAuthority", true, "Keep GameplayTweaks authoritative for heat/trial/war/pact systems when core cheat menu is present.");
		EnableFakeTraffic = ((BaseUnityPlugin)this).Config.Bind<bool>("FakeTraffic", "Enabled", true, "Enable pooled fake street traffic visuals near the camera. This does not create gameplay entities.");
		FakeTrafficMaxVisible = ((BaseUnityPlugin)this).Config.Bind<int>("FakeTraffic", "MaxVisible", 28, "Maximum number of fake traffic visuals active near the camera.");
		FakeTrafficInnerRingRadius = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "InnerRingRadius", 95f, "World-space radius around the camera center where fake traffic may exist.");
		FakeTrafficMiddleRingRadius = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "MiddleRingRadius", 155f, "World-space radius around the camera center where fake traffic can exist at reduced update cadence.");
		FakeTrafficDespawnRadius = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "DespawnRadius", 130f, "World-space radius where fake traffic is culled if it drifts too far from the camera.");
		FakeTrafficSpawnPerRoadKm = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "SpawnPerRoadKm", 4.5f, "Approximate fake-traffic density per visible road kilometer. This uses the cached lightweight road graph, not gameplay pathing.");
		FakeTrafficUpdateBuckets = ((BaseUnityPlugin)this).Config.Bind<int>("FakeTraffic", "UpdateBuckets", 3, "How many staggered update buckets fake traffic uses outside the inner ring. Higher values reduce per-frame work.");
		FakeTrafficSelectionSuppressRadius = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "SelectionSuppressRadius", 28f, "Suppress fake-traffic spawns near the current active or focused selection so overlays stay readable.");
		FakeTrafficDowntownBias = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "DowntownBias", 2.2f, "Extra spawn and density weight for road segments inside downtown-tagged districts.");
		FakeTrafficQueueSpacing = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "QueueSpacing", 6.5f, "Approximate same-lane spacing fake traffic tries to maintain so cars can queue at busy intersections.");
		FakeTrafficIntersectionStopChance = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "IntersectionStopChance", 0.24f, "Chance that a fake vehicle briefly stops at a multi-road intersection to create light queueing.");
		FakeTrafficStopLineOffset = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "StopLineOffset", 1.9f, "How far before a junction fake traffic stops so cars wait at the corner instead of mid-block.");
		FakeTrafficStopSecondsMin = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "StopSecondsMin", 0.7f, "Minimum fake intersection stop duration.");
		FakeTrafficStopSecondsMax = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "StopSecondsMax", 1.9f, "Maximum fake intersection stop duration.");
		FakeTrafficPlatoonChance = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "PlatoonChance", 0.42f, "Chance to spawn 1-3 followers behind a downtown or busy-road lead car so traffic bunches more naturally.");
		FakeTrafficMaxPlatoonFollowers = ((BaseUnityPlugin)this).Config.Bind<int>("FakeTraffic", "MaxPlatoonFollowers", 2, "Maximum number of follower cars added behind a lead fake-traffic spawn.");
		FakeTrafficSpeedMin = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "SpeedMin", 4.5f, "Minimum fake traffic movement speed in world units per second.");
		FakeTrafficSpeedMax = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "SpeedMax", 7.5f, "Maximum fake traffic movement speed in world units per second.");
		FakeTrafficRealAmbientCap = ((BaseUnityPlugin)this).Config.Bind<int>("FakeTraffic", "RealAmbientCap", 3, "Maximum number of real ambient vehicles allowed while fake traffic is enabled.");
		FakeTrafficDiagnosticsEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("FakeTraffic", "DiagnosticsEnabled", false, "Emit periodic fake-traffic runtime counters to the verification log.");
		FakeTrafficDiagnosticsLogInterval = ((BaseUnityPlugin)this).Config.Bind<float>("FakeTraffic", "DiagnosticsLogInterval", 8f, "Seconds between fake-traffic diagnostics log lines while the system is active.");
		CrewHiringAllowBusinessAssignedCandidates = ((BaseUnityPlugin)this).Config.Bind<bool>("CrewHiring", "AllowBusinessAssignedCandidates", false, "Allow hiring candidates even if currently business- or residence-assigned. Disabled by default because business-assigned NPCs can break hostile inspect/workplace/convo targeting.");
		BusinessOwnerEnforceAfterProhibitionFamilySafety = ((BaseUnityPlugin)this).Config.Bind<bool>("BusinessOwners", "EnforceAfterProhibitionFamilySafety", false, "Use AfterProhibitionFamily's family-safety classifier to reject automatic business-owner candidates. Default false because strict family rejection can exhaust vanilla owner pools during new-game map setup.");
		PactOpsDefaultsEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("PactOpsDefaults", "Enabled", true, "Enable native pact automation systems by default for this save.");
		PactOpsDefaultsAutoProtectEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("PactOpsDefaults", "AutoProtectEnabled", true, "Enable AutoProtect turf behavior for AI pact gangs.");
		PactOpsDefaultsAutoProtectIntervalDays = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "AutoProtectIntervalDays", 3, "Days between AutoProtect passes.");
		PactOpsDefaultsAutoProtectRange = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "AutoProtectRange", 1, "Search range around outposts for AutoProtect scoring.");
		PactOpsDefaultsCoordinatedAttackAutoEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("PactOpsDefaults", "CoordinatedAttackAutoEnabled", true, "Enable automatic coordinated attacks when WarHeat threshold is met.");
		PactOpsDefaultsCoordinatedAttackCooldownDays = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "CoordinatedAttackCooldownDays", 10, "Cooldown days between coordinated attack cycles per pact gang.");
		PactOpsDefaultsCoordinatedAttackMinCrew = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "CoordinatedAttackMinCrew", 3, "Minimum crew sent during coordinated attacks for pact gangs.");
		PactOpsDefaultsCoordinatedAttackMaxCrew = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "CoordinatedAttackMaxCrew", 5, "Maximum crew sent during coordinated attacks for pact gangs.");
		PactOpsDefaultsRevengeEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("PactOpsDefaults", "RevengeEnabled", true, "Enable native revenge queue and retaliation attacks.");
		PactOpsDefaultsRevengeDelayDays = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "RevengeDelayDays", 10, "Default revenge delay in days.");
		PactOpsDefaultsWarHeatDecayPerWeek = ((BaseUnityPlugin)this).Config.Bind<float>("PactOpsDefaults", "WarHeatDecayPerWeek", 6f, "WarHeat decay amount every 7 days.");
		PactOpsDefaultsWarHeatAttackGain = ((BaseUnityPlugin)this).Config.Bind<float>("PactOpsDefaults", "WarHeatAttackGain", 22f, "WarHeat gain for non-lethal hostile attacks.");
		PactOpsDefaultsWarHeatKillGain = ((BaseUnityPlugin)this).Config.Bind<float>("PactOpsDefaults", "WarHeatKillGain", 55f, "WarHeat gain when a hostile kill is recorded.");
		PactOpsDefaultsTerritoryTakeoverAggressionPercent = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "TerritoryTakeoverAggressionPercent", PactTerritoryTakeoverAggressionDefault, "Percent multiplier for AI pact territory takeover and stale turf cleanup pressure.");
		PactOpsDefaultsFrontClosureAggressionPercent = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "FrontClosureAggressionPercent", PactFrontClosureAggressionDefault, "Percent multiplier for automatic pact coordinated attacks against enemy fronts. Lower values require more WarHeat.");
		PactOpsDefaultsOverallAggroPercent = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "OverallAggroPercent", PactOverallAggroDefault, "Percent multiplier for pact revenge and general retaliation thresholds. Lower values require more WarHeat.");
		PactOpsDefaultsHireAutomationEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("PactOpsDefaults", "HireAutomationEnabled", true, "Enable native AI hire-limit profile automation.");
		PactOpsDefaultsTopGangCrewBonusMin = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "TopGangCrewBonusMin", 18, "Minimum crew bonus for top-tier pact gangs.");
		PactOpsDefaultsTopGangCrewBonusMax = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "TopGangCrewBonusMax", 28, "Maximum crew bonus for top-tier pact gangs.");
		PactOpsDefaultsMidGangCrewBonus = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "MidGangCrewBonus", 8, "Crew bonus for mid-tier pact gangs.");
		PactOpsDefaultsBottomGangCrewBonus = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "BottomGangCrewBonus", 3, "Crew bonus for bottom-tier pact gangs.");
		PactOpsDefaultsPactCrewBonus = ((BaseUnityPlugin)this).Config.Bind<int>("PactOpsDefaults", "PactCrewBonus", 4, "Global extra crew bonus applied to gangs in active pacts.");
		GangOpsDefaultsIndependentEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("GangOpsDefaults.Independent", "Enabled", true, "Enable independent gang automation systems by default for this save.");
		GangOpsDefaultsIndependentAutoProtectEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("GangOpsDefaults.Independent", "AutoProtectEnabled", true, "Enable AutoProtect turf behavior for AI independent gangs.");
		GangOpsDefaultsIndependentAutoProtectIntervalDays = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "AutoProtectIntervalDays", 4, "Days between independent AutoProtect passes.");
		GangOpsDefaultsIndependentAutoProtectRange = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "AutoProtectRange", 1, "Search range around outposts for independent AutoProtect scoring.");
		GangOpsDefaultsIndependentCoordinatedAttackAutoEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("GangOpsDefaults.Independent", "CoordinatedAttackAutoEnabled", true, "Enable automatic coordinated attacks for independent gangs when WarHeat threshold is met.");
		GangOpsDefaultsIndependentCoordinatedAttackCooldownDays = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "CoordinatedAttackCooldownDays", 20, "Cooldown days between coordinated attack cycles per independent gang.");
		GangOpsDefaultsIndependentCoordinatedAttackMinCrew = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "CoordinatedAttackMinCrew", 1, "Minimum crew sent during coordinated attacks.");
		GangOpsDefaultsIndependentCoordinatedAttackMaxCrew = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "CoordinatedAttackMaxCrew", 2, "Maximum crew sent during coordinated attacks.");
		GangOpsDefaultsIndependentRevengeEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("GangOpsDefaults.Independent", "RevengeEnabled", true, "Enable native revenge queue and retaliation attacks for independent gangs.");
		GangOpsDefaultsIndependentRevengeDelayDays = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "RevengeDelayDays", 16, "Default revenge delay in days for independent gangs.");
		GangOpsDefaultsIndependentWarHeatDecayPerWeek = ((BaseUnityPlugin)this).Config.Bind<float>("GangOpsDefaults.Independent", "WarHeatDecayPerWeek", 10f, "WarHeat decay amount every 7 days for independent gangs.");
		GangOpsDefaultsIndependentWarHeatAttackGain = ((BaseUnityPlugin)this).Config.Bind<float>("GangOpsDefaults.Independent", "WarHeatAttackGain", 14f, "WarHeat gain for non-lethal hostile attacks for independent gangs.");
		GangOpsDefaultsIndependentWarHeatKillGain = ((BaseUnityPlugin)this).Config.Bind<float>("GangOpsDefaults.Independent", "WarHeatKillGain", 35f, "WarHeat gain when a hostile kill is recorded for independent gangs.");
		GangOpsDefaultsIndependentTerritoryTakeoverAggressionPercent = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "TerritoryTakeoverAggressionPercent", IndependentTerritoryTakeoverAggressionDefault, "Percent multiplier for independent gang territory takeover and stale turf cleanup pressure.");
		GangOpsDefaultsIndependentFrontClosureAggressionPercent = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "FrontClosureAggressionPercent", IndependentFrontClosureAggressionDefault, "Percent multiplier for automatic independent coordinated attacks against enemy fronts. Lower values require more WarHeat.");
		GangOpsDefaultsIndependentOverallAggroPercent = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "OverallAggroPercent", IndependentOverallAggroDefault, "Percent multiplier for independent revenge and general retaliation thresholds. Lower values require more WarHeat.");
		GangOpsDefaultsIndependentHireAutomationEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("GangOpsDefaults.Independent", "HireAutomationEnabled", IndependentHireAutomationDefault, "Enable native AI hire-limit profile automation for independent gangs.");
		GangOpsDefaultsIndependentTopGangCrewBonusMin = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "TopGangCrewBonusMin", IndependentTopGangCrewBonusMinDefault, "Minimum crew bonus for top-tier independent gangs.");
		GangOpsDefaultsIndependentTopGangCrewBonusMax = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "TopGangCrewBonusMax", IndependentTopGangCrewBonusMaxDefault, "Maximum crew bonus for top-tier independent gangs.");
		GangOpsDefaultsIndependentMidGangCrewBonus = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "MidGangCrewBonus", IndependentMidGangCrewBonusDefault, "Crew bonus for mid-tier independent gangs.");
		GangOpsDefaultsIndependentBottomGangCrewBonus = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "BottomGangCrewBonus", IndependentBottomGangCrewBonusDefault, "Crew bonus for bottom-tier independent gangs.");
		GangOpsDefaultsIndependentPactCrewBonus = ((BaseUnityPlugin)this).Config.Bind<int>("GangOpsDefaults.Independent", "PactCrewBonus", 0, "Global extra crew bonus applied to independent gangs.");
		bool gangOpsDefaultsChanged = ApplyGangOpsHireDefaultUpgrades();
		gangOpsDefaultsChanged |= ApplyGangOpsTerritoryProtectionDefaultUpgrades();
		if (gangOpsDefaultsChanged)
		{
			((BaseUnityPlugin)this).Config.Save();
		}
	}

	private void ApplyHarmonyPatches(Harmony harmony)
	{
		RegisterCoreAndRelationshipFeatures(harmony);
		RegisterUiAndInteractionFeatures(harmony);
		RegisterCombatAndGangOpsFeatures(harmony);
		RegisterCompatibilityAndWorldFeatures(harmony);
		RegisterVehicleAndPoliticsFeatures(harmony);
	}

	private static void InitializeSubsystems()
	{
		// CopWarSystem and CombatNameDisplayPatch moved to CopKilling mod
		EnsureCustomRelationshipBuffDefinitions();
		LogLoadedBuildBanner();
		TryInitializeLeanTweenCapacity();
		EnsureFakeTrafficSubsystem();
	}

	private static void TryInitializeLeanTweenCapacity()
	{
		try
		{
			Type leanTweenType = AccessTools.TypeByName("LeanTween");
			MethodInfo initMethod = leanTweenType?.GetMethod("init", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(int) }, null);
			if (initMethod == null)
			{
				return;
			}

			initMethod.Invoke(null, new object[] { 2400 });
			VerificationLog("UITween", "LeanTween.init capacity=2400");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] LeanTween init boost failed: " + ex.Message);
		}
	}

	private static void LogLoadedBuildBanner()
	{
		try
		{
			var assembly = typeof(GameplayTweaksPlugin).Assembly;
			string version = assembly.GetName().Version?.ToString() ?? "0.0.0.0";
			string assemblyPath = assembly.Location;
			string fileName = string.IsNullOrEmpty(assemblyPath) ? "GameplayTweaks.dll" : Path.GetFileName(assemblyPath);
			string builtAt = !string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath)
				? File.GetLastWriteTime(assemblyPath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
				: "unknown";
			string normalizedPath = string.IsNullOrEmpty(assemblyPath) ? "unknown" : assemblyPath;
			bool coreEnabled = EnableCoreAndRelationshipFeatures?.Value ?? false;
			bool crewRelationsEnabled = EnableCrewRelationsMenuFeatures?.Value ?? false;
			bool uiEnabled = EnableUiAndInteractionFeatures?.Value ?? false;
			bool combatEnabled = EnableCombatAndGangOpsFeatures?.Value ?? false;
			bool compatEnabled = EnableCompatibilityAndWorldFeatures?.Value ?? false;
			bool vehicleAndPoliticsEnabled = EnableVehicleAndPoliticsFeatures?.Value ?? false;
			bool routeSimConvenienceEnabled = EnableRouteSimulatedConvenienceActions?.Value ?? true;
			bool warWeaponStanceEnabled = EnableWarWeaponStances?.Value ?? true;
			bool warWeaponStanceAiBreachesEnabled = WarWeaponStanceEnableAiBreaches?.Value ?? true;
			GetWarWeaponStanceThresholds(out int streetThreshold, out int sidearmThreshold, out int openArsenalThreshold);
			int meleeViolationHeat = GetConfiguredWarStanceViolationHeat(WarStanceWeaponCategory.Melee);
			int sidearmViolationHeat = GetConfiguredWarStanceViolationHeat(WarStanceWeaponCategory.Sidearm);
			int heavyViolationHeat = GetConfiguredWarStanceViolationHeat(WarStanceWeaponCategory.LongGun);
			string message = $"Gameplay Tweaks Extended loaded version={version} file={fileName} built={builtAt} path={normalizedPath} coreEnabled={coreEnabled} crewRelationsEnabled={crewRelationsEnabled} uiEnabled={uiEnabled} combatEnabled={combatEnabled} warWeaponStanceEnabled={warWeaponStanceEnabled} warWeaponStanceAiBreachesEnabled={warWeaponStanceAiBreachesEnabled} warWeaponStanceThresholds={streetThreshold}/{sidearmThreshold}/{openArsenalThreshold} warWeaponStanceViolationHeat={meleeViolationHeat}/{sidearmViolationHeat}/{heavyViolationHeat} compatEnabled={compatEnabled} vehiclePoliticsEnabled={vehicleAndPoliticsEnabled} routeSimConvenienceEnabled={routeSimConvenienceEnabled}";
			Debug.Log("[GameplayTweaks] " + message);
			VerificationLog("Compat", $"loaded version={version} file={fileName} built={builtAt} path={normalizedPath} coreEnabled={coreEnabled} crewRelationsEnabled={crewRelationsEnabled} uiEnabled={uiEnabled} combatEnabled={combatEnabled} compatEnabled={compatEnabled} vehiclePoliticsEnabled={vehicleAndPoliticsEnabled} routeSimConvenienceEnabled={routeSimConvenienceEnabled}");
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] LogLoadedBuildBanner failed: " + ex.Message);
		}
	}
}
}
