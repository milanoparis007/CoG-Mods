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

		harmony.PatchAll(typeof(SpouseEthnicityLinkPatch));
		harmony.PatchAll(typeof(SpouseEthnicityCandidatePatch));
		FindRandoToMarryPatch.ApplyPatch(harmony);
		HireableAgePatch.ApplyManualDetour();
		PotentialBizOwnerEligibilityPatch.ApplyPatch(harmony);
		CrewRelationshipHandlerPatch.ApplyPatch(harmony);
		StatTrackingPatch.ApplyPatch(harmony);
		BossArrestPatch.ApplyPatch(harmony);
		DeadRelationshipCleanupPatch.ApplyPatch(harmony);
		JailSystem.Initialize();
		SaveLoadPatch.ApplyPatch(harmony);
		CopTrialRetainerPatch.ApplyPatch(harmony);
		JailedManagerGuardPatch.ApplyPatch(harmony);
		DirtyCashPatches.ApplyPatches(harmony);
		FrontTrackingPatch.ApplyPatch(harmony);
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
		CrewPeepInspectModButtonsPatch.ApplyPatch(harmony);
		OrgChartGangOpsButtonPatch.ApplyPatch(harmony);
		KeyboardBlockerPatch.ApplyPatch(harmony);
		SelectionManagerRightClickGuardPatch.ApplyPatch(harmony);
		TmpReplacementCharacterSanitizerPatch.ApplyPatch(harmony);
		SelectionFocusDeferredArrestPatch.ApplyPatch(harmony);
		CrewPickAggroRefreshStabilityPatch.ApplyPatch(harmony);
		PactColorUiPatch.ApplyPatch(harmony);
		GangVisibilityRevealPatch.ApplyPatch(harmony);
		HostileMobileSelectionFixPatch.ApplyPatch(harmony);
		GangTradeConvoStateFixPatch.ApplyPatch(harmony);
		ConvoNullFixPatch.ApplyPatch(harmony);
		CombatAdvisorNullFixPatch.ApplyPatch(harmony);
		ConnectionsTabNullFixPatch.ApplyPatch(harmony);
		GangPanelBuffDebugPatch.ApplyPatch(harmony);
	}

	private void RegisterCombatAndGangOpsFeatures(Harmony harmony)
	{
		if (!(EnableCombatAndGangOpsFeatures?.Value ?? false))
		{
			VerificationLog("Compat", "combat/gang-ops patch registration skipped by FeatureGates.EnableCombatAndGangOpsFeatures=false");
			return;
		}

		VehicleGroupCombatPatch.ApplyPatch(harmony);
		PactOpsCombatPatch.ApplyPatch(harmony);
		RealCombatGrapevinePatch.ApplyPatch(harmony);
		BossMurderWarrantPatch.ApplyPatch(harmony);
		TurnUpdatePatch.ApplyPatch(harmony);
		AICrewCapPatch.ApplyPatch(harmony);
		PlayerCrewCapPatch.ApplyPatch(harmony);
		CopDeathVehicleCleanupPatch.ApplyPatch(harmony);
		AttackAdvisorPatch.ApplyPatch(harmony);
		PrecinctAdvisorRaidPatch.ApplyPatch(harmony);
		AgentArrestHeatPatch.ApplyPatch(harmony);
		DeliveryStreetCreditPatch.ApplyPatch(harmony);
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
		CheckForConflictingMods();
		DirtyCashEconomyCompatibilityPatch.ApplyPatch(harmony);
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

		MultiCrewVehiclePatches.ApplyPatches(harmony);
		PoliticsManagerPatches.ApplyPatches(harmony);
	}
}
}
