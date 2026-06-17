using System;
using System.Reflection;
using HarmonyLib;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private void RegisterCoreAndRelationshipFeatures(Harmony harmony)
	{
		harmony.PatchAll(typeof(PersonNameSanitizerPatch));
		harmony.PatchAll(typeof(TraitLocalizationSanitizerPatch));
		harmony.PatchAll(typeof(PersonInfoTextSanitizerPatch));

		if (!(EnableCoreAndRelationshipFeatures?.Value ?? false))
		{
			if (EnableCrewRelationsMenuFeatures?.Value ?? false)
			{
				CrewRelationshipHandlerPatch.ApplyPatch(harmony);
				VerificationLog("Compat", "core/relationship patch registration reduced to Crew Relations menu slice");
			}
			else
			{
				VerificationLog("Compat", "core/relationship patch registration skipped by FeatureGates.EnableCoreAndRelationshipFeatures=false");
			}
			return;
		}

		if (IsAfterProhibitionFamilySpouseSearchAvailable())
		{
			VerificationLog("Family", "source=afterprohibition-family spouseSearch=delegated fallback=False");
		}
		else
		{
			harmony.PatchAll(typeof(SpouseEthnicityLinkPatch));
			harmony.PatchAll(typeof(SpouseEthnicityCandidatePatch));
			FindRandoToMarryPatch.ApplyPatch(harmony);
		}
		if (IsAfterProhibitionFamilyStartupGenerationAvailable())
		{
			VerificationLog("Family", "source=afterprohibition-family startupGeneration=delegated fallback=False");
		}
		else
		{
			HumanStartupParentFallbackPatch.ApplyPatch(harmony);
			StartupGeneratedPopulationPatch.ApplyPatch(harmony);
		}
		HumanStartupSafehouseReplacementPatch.ApplyPatch(harmony);
		StartupStarterPackNullGuardPatch.ApplyPatch(harmony);
		StartupNpcSafehouseOwnerGuardPatch.ApplyPatch(harmony);
		HireableAgePatch.ApplyManualDetour();
		PotentialBizOwnerEligibilityPatch.ApplyPatch(harmony);
		CrewRelationshipHandlerPatch.ApplyPatch(harmony);
		StatTrackingPatch.ApplyPatch(harmony);
		BossArrestPatch.ApplyPatch(harmony);
		if (IsAfterProhibitionFamilyDeadRelationshipCleanupAvailable())
		{
			VerificationLog("Family", "source=afterprohibition-family deadCleanup=delegated fallback=False");
		}
		else
		{
			DeadRelationshipCleanupPatch.ApplyPatch(harmony);
		}
		JailSystem.Initialize();
		SaveLoadPatch.ApplyPatch(harmony);
		CopTrialRetainerPatch.ApplyPatch(harmony);
		JailedManagerGuardPatch.ApplyPatch(harmony);
		DirtyCashPatches.ApplyPatches(harmony);
		FrontTrackingPatch.ApplyPatch(harmony);
		LogAfterProhibitionFamilyDelegationSummary();
	}

	private void RegisterUiAndInteractionFeatures(Harmony harmony)
	{
		if (!(EnableUiAndInteractionFeatures?.Value ?? false))
		{
			VerificationLog("Compat", "ui/interaction patch registration skipped by FeatureGates.EnableUiAndInteractionFeatures=false");
			return;
		}

		CrewFedArrestDialogPatch.ApplyPatch(harmony);
		CrewFedArrestEventPatch.ApplyPatch(harmony);
		CrewVehicleReassignedEventPatch.ApplyPatch(harmony);
		CrewVehicleReassignedDialogPatch.ApplyPatch(harmony);
		CrewVehicleReassignedPickPatch.ApplyPatch(harmony);
		if (IsAfterProhibitionUiCrewInfoButtonsAvailable())
		{
			VerificationLog("CrewInfoButtons", "skipped in GameplayTweaks owner=AfterProhibitionUI");
		}
		else
		{
		CrewPeepInspectModButtonsPatch.ApplyPatch(harmony);
		}
		OrgChartGangOpsButtonPatch.ApplyPatch(harmony);
		KeyboardBlockerPatch.ApplyPatch(harmony);
		CameraMapBoundsPatch.ApplyPatch(harmony);
		SelectionManagerRightClickGuardPatch.ApplyPatch(harmony);
		if (IsAfterProhibitionUiTextSanitizerAvailable())
		{
			VerificationLog("UISanitize", "skipped in GameplayTweaks owner=AfterProhibitionUI");
		}
		else
		{
			TmpReplacementCharacterSanitizerPatch.ApplyPatch(harmony);
		}
		SelectionFocusDeferredArrestPatch.ApplyPatch(harmony);
		CrewPickAggroRefreshStabilityPatch.ApplyPatch(harmony);
		if (IsAfterProhibitionUiCrewSidebarJailBarsAvailable())
		{
			VerificationLog("Jail", "crew sidebar jail-bars skipped in GameplayTweaks owner=AfterProhibitionUI");
		}
		else
		{
			CrewSidebarJailBarsPatch.ApplyPatch(harmony);
		}
		PactColorUiPatch.ApplyPatch(harmony);
		GangVisibilityRevealPatch.ApplyPatch(harmony);
		HostileMobileSelectionFixPatch.ApplyPatch(harmony);
		GangTradeConvoStateFixPatch.ApplyPatch(harmony);
		ConvoNullFixPatch.ApplyPatch(harmony);
		CombatAdvisorNullFixPatch.ApplyPatch(harmony);
		if (IsAfterProhibitionUiStalePortraitGuardsAvailable())
		{
			VerificationLog("ConnectionsTab", "skipped in GameplayTweaks owner=AfterProhibitionUI");
		}
		else
		{
			ConnectionsTabNullFixPatch.ApplyPatch(harmony);
		}
		GangPanelBuffDebugPatch.ApplyPatch(harmony);
		ResidentialEventStabilityPatch.ApplyPatch(harmony);
	}

	private static bool IsAfterProhibitionUiInstalled()
	{
		try
		{
			return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.ui");
		}
		catch
		{
			return false;
		}
	}

	private static bool IsAfterProhibitionFamilySpouseSearchAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsSpouseSearchMigration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionFamilyStartupGenerationAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsStartupFamilyGenerationFallback", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionFamilyDeadRelationshipCleanupAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsDeadRelationshipCleanupMigration", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionFamilyRelationshipSafetyAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsRelationshipSafetyClassification", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionFamilyPregnancyLifecycleAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsPregnancyLifecycle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static void LogAfterProhibitionFamilyDelegationSummary()
	{
		bool spouseSafety = IsAfterProhibitionFamilyRelationshipSafetyAvailable();
		bool spouseSearch = IsAfterProhibitionFamilySpouseSearchAvailable();
		bool pregnancy = IsAfterProhibitionFamilyPregnancyLifecycleAvailable();
		bool futureKidValidation = IsAfterProhibitionFamilyFutureKidValidationAvailable();
		bool startupGeneration = IsAfterProhibitionFamilyStartupGenerationAvailable();
		bool deadCleanup = IsAfterProhibitionFamilyDeadRelationshipCleanupAvailable();
		bool businessOwnerFamilySafety = IsAfterProhibitionFamilyBusinessOwnerFamilySafetyAvailable();
		bool businessOwnerFamilySafetyEnforced = BusinessOwnerEnforceAfterProhibitionFamilySafety?.Value ?? false;

		VerificationLog(
			"Family",
			"delegation-summary source=afterprohibition-family" +
			" spouseSafety=" + (spouseSafety ? "bridge-available" : "gameplaytweaks-fallback") +
			" spouseSearch=" + (spouseSearch ? "delegated" : "gameplaytweaks-fallback") +
			" pregnancy=" + (pregnancy ? "bridge-available" : "gameplaytweaks-fallback") +
			" futureKidValidation=" + (futureKidValidation ? "delegated" : "gameplaytweaks-fallback") +
			" startupGeneration=" + (startupGeneration ? "delegated" : "gameplaytweaks-fallback") +
			" deadCleanup=" + (deadCleanup ? "delegated" : "gameplaytweaks-fallback") +
			" businessOwnerFamilySafety=" + (businessOwnerFamilySafety ? "bridge-available" : "gameplaytweaks-fallback") +
			" businessOwnerFamilySafetyEnforced=" + (businessOwnerFamilySafetyEnforced ? "true" : "false"));

		VerificationLog(
			"Family",
			"ownership-audit source=afterprohibition-family" +
			" familyClassification=" + (spouseSafety ? "AfterProhibitionFamily" : "GameplayTweaks") +
			" spouseSearch=" + (spouseSearch ? "AfterProhibitionFamily" : "GameplayTweaks") +
			" pregnancyLifecycle=" + (pregnancy ? "AfterProhibitionFamily" : "GameplayTweaks") +
			" futureKidValidation=" + (futureKidValidation ? "AfterProhibitionFamily" : "GameplayTweaks") +
			" startupFamilyGeneration=" + (startupGeneration ? "AfterProhibitionFamily" : "GameplayTweaks") +
			" deadRelationshipCleanup=" + (deadCleanup ? "AfterProhibitionFamily" : "GameplayTweaks") +
			" safehouseRepair=GameplayTweaks" +
			" businessOwnerMutation=GameplayTweaks" +
			" hiringExecution=GameplayTweaks" +
			" economy=GameplayTweaks");
	}

	private static bool IsAfterProhibitionFamilyFutureKidValidationAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsFutureKidValidation", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionFamilyBusinessOwnerFamilySafetyAvailable()
	{
		try
		{
			if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				MethodInfo ownsMethod = pluginType?.GetMethod("OwnsBusinessOwnerFamilySafety", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null)
				{
					continue;
				}

				object result = ownsMethod.Invoke(null, null);
				return result is bool enabled && enabled;
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionUiCrewInfoButtonsAvailable()
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionUI.AfterProhibitionUIPlugin", throwOnError: false);
				MethodInfo enabledMethod = pluginType?.GetMethod("IsCrewInfoButtonsEnabledForExternalOwnerCheck", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
				if (enabledMethod != null)
				{
					object result = enabledMethod.Invoke(null, null);
					return result is bool enabled && enabled;
				}

				Type patchType = assembly.GetType("AfterProhibitionUI.CrewInfoButtonsPatch", throwOnError: false);
				if (patchType != null)
				{
					return true;
				}
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionUiTextSanitizerAvailable()
	{
		return IsAfterProhibitionUiFeatureEnabled(
			"IsTextSanitizerEnabledForExternalOwnerCheck",
			"AfterProhibitionUI.TmpReplacementCharacterSanitizerPatch");
	}

	private static bool IsAfterProhibitionUiCrewSidebarJailBarsAvailable()
	{
		return IsAfterProhibitionUiFeatureEnabled(
			"IsCrewSidebarJailBarsEnabledForExternalOwnerCheck",
			"AfterProhibitionUI.CrewSidebarJailBarsPatch");
	}

	private static bool IsAfterProhibitionUiFeatureEnabled(string enabledMethodName, string fallbackPatchTypeName)
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionUI.AfterProhibitionUIPlugin", throwOnError: false);
				MethodInfo enabledMethod = pluginType?.GetMethod(enabledMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (enabledMethod != null)
				{
					object result = enabledMethod.Invoke(null, null);
					return result is bool enabled && enabled;
				}

				Type patchType = assembly.GetType(fallbackPatchTypeName, throwOnError: false);
				if (patchType != null)
				{
					return true;
				}
			}
		}
		catch
		{
		}

		return false;
	}

	private static bool IsAfterProhibitionUiStalePortraitGuardsAvailable()
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionUI.AfterProhibitionUIPlugin", throwOnError: false);
				MethodInfo enabledMethod = pluginType?.GetMethod("IsStalePortraitGuardsEnabledForExternalOwnerCheck", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (enabledMethod != null)
				{
					object result = enabledMethod.Invoke(null, null);
					return result is bool enabled && enabled;
				}

				Type patchType = assembly.GetType("AfterProhibitionUI.StalePortraitAndConnectionGuardPatch", throwOnError: false);
				if (patchType != null)
				{
					return true;
				}
			}
		}
		catch
		{
		}

		return false;
	}

	internal static bool IsAfterProhibitionUiCrewManagementJailVisualsAvailable()
	{
		if (!IsAfterProhibitionUiInstalled())
		{
			return false;
		}

		try
		{
			foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionUI.AfterProhibitionUIPlugin", throwOnError: false);
				MethodInfo enabledMethod = pluginType?.GetMethod("IsCrewManagementJailVisualsEnabledForExternalOwnerCheck", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (enabledMethod != null)
				{
					object result = enabledMethod.Invoke(null, null);
					return result is bool enabled && enabled;
				}

				Type patchType = assembly.GetType("AfterProhibitionUI.CrewManagementJailVisualPatch", throwOnError: false);
				if (patchType != null)
				{
					return true;
				}
			}
		}
		catch
		{
		}

		return false;
	}

	private void RegisterCombatAndGangOpsFeatures(Harmony harmony)
	{
		if (!(EnableCombatAndGangOpsFeatures?.Value ?? false))
		{
			VerificationLog("Compat", "combat/gang-ops patch registration skipped by FeatureGates.EnableCombatAndGangOpsFeatures=false");
			return;
		}

		VehicleGroupCombatPatch.ApplyPatch(harmony);
		CommandAttackRouteAuthorityPatch.ApplyPatch(harmony);
		ScriptDispatcherAttackTargetRouteAuthorityPatch.ApplyPatch(harmony);
		PactOpsCombatPatch.ApplyPatch(harmony);
		RealCombatGrapevinePatch.ApplyPatch(harmony);
		BossMurderWarrantPatch.ApplyPatch(harmony);
		TurnUpdatePatch.ApplyPatch(harmony);
		PlayerAIWillStealTickerPatch.ApplyPatch(harmony);
		AICrewCapPatch.ApplyPatch(harmony);
		PlayerCrewCapPatch.ApplyPatch(harmony);
		CopDeathVehicleCleanupPatch.ApplyPatch(harmony);
		AttackAdvisorPatch.ApplyPatch(harmony);
		PrecinctAdvisorRaidPatch.ApplyPatch(harmony);
		AgentArrestHeatPatch.ApplyPatch(harmony);
		DeliveryStreetCreditPatch.ApplyPatch(harmony);
		DeliveryFrontExpansionPatch.ApplyPatch(harmony);
	}

	private void RegisterCompatibilityAndWorldFeatures(Harmony harmony)
	{
		if (!(EnableCompatibilityAndWorldFeatures?.Value ?? false))
		{
			VerificationLog("Compat", "compat/world patch registration skipped by FeatureGates.EnableCompatibilityAndWorldFeatures=false");
			return;
		}

		TerritoryColorPatch.ApplyPatch(harmony);
		AfterProhibitionAssemblyPortPatches.ApplyPatches(harmony);
		CivicNonpurchaseFallbackPatch.ApplyPatch(harmony);
		FakeTrafficAmbientCapPatch.ApplyPatch(harmony);
		TurnPerformanceOptimizationsPatch.ApplyPatch(harmony);
		GoonAdvisorPerformancePatch.ApplyPatch(harmony);
		UnitsAdvisorPerformancePatch.ApplyPatch(harmony);
		TurnPerformanceDiagnosticsPatch.ApplyPatch(harmony);
		CheckForConflictingMods();
		DirtyCashEconomyCompatibilityPatch.ApplyPatch(harmony);
		GamblerDebtDiagnosticsPatch.ApplyPatch(harmony);
		ApplyCoreCheatCompatibilityGuards();
		GangWarsAdapterPatch.ApplyPatch(harmony);
	}

	private void RegisterVehicleAndPoliticsFeatures(Harmony harmony)
	{
		if (!(EnableVehicleAndPoliticsFeatures?.Value ?? false))
		{
			VerificationLog("Compat", "vehicle/politics patch registration skipped by FeatureGates.EnableVehicleAndPoliticsFeatures=false");
			return;
		}

		SchemeVehicleDiagnosticsPatch.ApplyPatch(harmony);
		MultiCrewVehiclePatches.ApplyPatches(harmony);
		PoliticsManagerPatches.ApplyPatches(harmony);
	}
}
}
