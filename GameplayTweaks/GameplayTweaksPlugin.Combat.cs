using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Combat;
using Game.UI.Mouseovers;
using Game.UI.Util;
using HarmonyLib;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace GameplayTweaks
{
public static class CombatObserverBridge
{
	public static event Action<CombatResults, string> CombatResolved;

	public static void Publish(CombatResults result, string source)
	{
		if (result == null)
		{
			return;
		}
		Action<CombatResults, string> combatResolved = CombatResolved;
		if (combatResolved == null)
		{
			return;
		}
		foreach (Action<CombatResults, string> item in combatResolved.GetInvocationList().Cast<Action<CombatResults, string>>())
		{
			try
			{
				item(result, source);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CombatObserverBridge.Publish: " + ex.Message);
			}
		}
	}
}

public partial class GameplayTweaksPlugin
{
	private static readonly List<ulong> _activeGroupedCombatAttackerPeepIds = new List<ulong>();

	private static string _activeGroupedCombatAttackerSnapshotSource = string.Empty;

	private static MethodInfo _copWarShouldSuppressIncomingAiCopCombatMethod;

	internal static void SetActiveGroupedCombatAttackerSnapshot(string transactionSource, IEnumerable<CrewAssignment> attackers)
	{
		_activeGroupedCombatAttackerPeepIds.Clear();
		_activeGroupedCombatAttackerSnapshotSource = transactionSource ?? string.Empty;
		if (attackers == null)
		{
			return;
		}
		foreach (CrewAssignment attacker in attackers)
		{
			if (!attacker.IsValid || !attacker.peepId.IsValid)
			{
				continue;
			}
			ulong peepId = attacker.peepId.id;
			if (!_activeGroupedCombatAttackerPeepIds.Contains(peepId))
			{
				_activeGroupedCombatAttackerPeepIds.Add(peepId);
			}
		}
	}

	internal static void ClearActiveGroupedCombatAttackerSnapshot()
	{
		_activeGroupedCombatAttackerPeepIds.Clear();
		_activeGroupedCombatAttackerSnapshotSource = string.Empty;
	}

	internal static bool TryGetActiveGroupedCombatAttackerSnapshot(out List<EntityID> attackerPeepIds, out string snapshotSource)
	{
		attackerPeepIds = new List<EntityID>();
		snapshotSource = _activeGroupedCombatAttackerSnapshotSource ?? string.Empty;
		if (_activeGroupedCombatAttackerPeepIds.Count <= 0)
		{
			return false;
		}

		foreach (ulong rawPeepId in _activeGroupedCombatAttackerPeepIds)
		{
			try
			{
				EntityID peepId = EntityID.FromID(rawPeepId);
				if (peepId.IsValid)
				{
					attackerPeepIds.Add(peepId);
				}
			}
			catch
			{
			}
		}

		return attackerPeepIds.Count > 0;
	}

	private static class StatTrackingPatch
	{
		private static FieldInfo _entityField;

		private static int _lastStreetCreditGainLogDay = -1;

		private static ulong _lastStreetCreditGainLogPeep = ulong.MaxValue;

		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Entities.AgentComponent");
				if (!(type == null))
				{
					MethodInfo method = type.GetMethod("IncrementStat", BindingFlags.Instance | BindingFlags.Public);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, (HarmonyMethod)null, new HarmonyMethod(typeof(StatTrackingPatch), "IncrementStatPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Stat tracking enabled");
					}
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] StatTrackingPatch failed: {arg}");
			}
		}

		private static void IncrementStatPostfix(object __instance, CrewStats key, int delta)
		{

			if (!EnableCrewStats.Value)
			{
				return;
			}
			try
			{
				if (_entityField == null)
				{
					_entityField = typeof(AgentComponent).BaseType?.GetField("_entity", BindingFlags.Instance | BindingFlags.NonPublic);
				}
				object obj = _entityField?.GetValue(__instance);
				Entity val = obj as Entity;
				if (val == null)
				{
					return;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || val.data.agent.pid != humanPlayer.PID)
				{
					return;
				}
				CrewModState orCreateCrewState = GetOrCreateCrewState(val.Id);
				if (orCreateCrewState == null)
				{
					return;
				}
				if ((int)key == 25)
				{
					orCreateCrewState.StreetCreditProgress += (float)delta * STREET_CREDIT_GAIN_FIGHT;
					float loyaltyMul = GetLoyaltyHappinessPenaltyMultiplier(orCreateCrewState);
					orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - CREW_HAPPINESS_LOSS_FIGHT * (float)delta * loyaltyMul);
					LogStreetCreditGainOncePerDay(val.Id, "fight", delta, STREET_CREDIT_GAIN_FIGHT);
				}
				else if ((int)key == 0)
				{
					orCreateCrewState.StreetCreditProgress += (float)delta * STREET_CREDIT_GAIN_KILL;
					string fullName = val.data.person.FullName;
					LogGrapevine($"DEATH: {delta} rival(s) fell to {fullName}");
					float loyaltyMul2 = GetLoyaltyHappinessPenaltyMultiplier(orCreateCrewState);
					orCreateCrewState.HappinessValue = Mathf.Clamp01(orCreateCrewState.HappinessValue - CREW_HAPPINESS_LOSS_KILL * (float)delta * loyaltyMul2);
					LogStreetCreditGainOncePerDay(val.Id, "kill", delta, STREET_CREDIT_GAIN_KILL);
					bool hasCopKillFederalWitness = HasActiveImportantWitnessForCrewAndSource(val.Id, NATIONAL_HEAT_SOURCE_COP_KILL);
					for (int i = 0; i < delta; i++)
					{
						if (hasCopKillFederalWitness)
						{
							continue;
						}
						if (SharedRng.NextDouble() < 0.30000001192092896)
						{
							orCreateCrewState.WitnessCount++;
							orCreateCrewState.HasWitness = true;
							orCreateCrewState.WitnessThreatAttempted = false;
							orCreateCrewState.WitnessThreatenedSuccessfully = false;
							if (orCreateCrewState.WitnessCount >= 3)
							{
								SetLocalHeatProgress(orCreateCrewState, 0.75f, G.GetNow().days, refreshDecayAnchor: true);
							}
							else if (orCreateCrewState.WitnessCount == 2)
							{
								SetLocalHeatProgress(orCreateCrewState, Mathf.Max(orCreateCrewState.LocalHeatProgress, 0.5f), G.GetNow().days, refreshDecayAnchor: true);
							}
							else
							{
								SetLocalHeatProgress(orCreateCrewState, Mathf.Max(orCreateCrewState.LocalHeatProgress, 0.25f), G.GetNow().days, refreshDecayAnchor: true);
							}
							Debug.Log($"[GameplayTweaks] Witness #{orCreateCrewState.WitnessCount} saw {val.data.person.FullName} commit a kill!");
						}
					}
				}
				ResolveStreetCreditProgressLevelUps(orCreateCrewState, humanPlayer, val.Id, "combat");
				SyncLegacyWantedFields(orCreateCrewState);
			}
			catch
			{
			}
		}

		private static void LogStreetCreditGainOncePerDay(EntityID peepId, string source, int delta, float gainPerEvent)
		{
			if (delta <= 0 || peepId.IsNotValid)
			{
				return;
			}
			int days = G.GetNow().days;
			if (_lastStreetCreditGainLogDay == days && _lastStreetCreditGainLogPeep == peepId.id)
			{
				return;
			}
			_lastStreetCreditGainLogDay = days;
			_lastStreetCreditGainLogPeep = peepId.id;
			VerificationLog("StreetCredit", $"source={source} peep={peepId.id} delta={delta} gain={(float)delta * gainPerEvent:0.000}");
		}
	}


	private static class VehicleGroupCombatPatch
	{
		private sealed class WeaponPoolEntry
		{
			public string WeaponId;

			public WeaponConfig Weapon;

			public int Remaining;
		}

		private struct AppliedDamageInfo
		{
			public Fixnum damage;

			public bool wasHurt;

			public bool isDead;
		}

		private sealed class CombatExchangeDebugInfo
		{
			public List<CrewAssignment> DefenderRecipients;

			public List<CrewAssignment> AttackerRecipients;

			public Dictionary<EntityID, Fixnum> PlannedDamageToDefenders;

			public Dictionary<EntityID, Fixnum> PlannedDamageToAttackers;

			public Dictionary<EntityID, AppliedDamageInfo> AppliedDamageToDefenders;

			public Dictionary<EntityID, AppliedDamageInfo> AppliedDamageToAttackers;

			public string DefenderDistributionMode;

			public EntityID FocusedDefenderPeepId;

			public float? FocusShare;

			public string DistributionFallbackReason;
		}

		private sealed class PopupCombatAction
		{
			public Entity RepresentativePeep;

			public Entity Target;

			public WeaponConfig Weapon;

			public CrewAssignment AttackerCrew;

			public CrewAssignment TargetCrew;

			public bool IsGrouped;

			public string ActionKey;
		}

		private enum GroupedCombatMode
		{
			OnFoot,
			DriveBy
		}

		private enum OnFootWeaponFilter
		{
			Any,
			Melee,
			Ranged
		}

		private enum DriveByTargetMode
		{
			FocusOne,
			SpreadAll
		}

		private static readonly MethodInfo PopupFindTargetForMethod = typeof(CombatPopupPlanning).GetMethod("FindTargetFor", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly MethodInfo PopupFindWeaponForMethod = typeof(CombatPopupPlanning).GetMethod("FindWeaponFor", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly MethodInfo PopupOnTargetRowSelectionMethod = typeof(CombatPopupPlanning).GetMethod("OnTargetRowSelection", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(TMP_Dropdown), typeof(GameObject), typeof(int) }, null);

		private static readonly MethodInfo CombatShowCombatResultsMethod = typeof(CombatManager).GetMethod("ShowCombatResults", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(List<CombatResults>), typeof(bool), typeof(bool) }, null);

		private static readonly FieldInfo PopupMyCrewField = typeof(CombatPopupPlanning).GetField("_mycrew", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly FieldInfo PopupEnemiesField = typeof(CombatPopupPlanning).GetField("_enemies", BindingFlags.Instance | BindingFlags.NonPublic);

		private static readonly MethodInfo CombatDoPayAttackCostMethod = typeof(CombatManager).GetMethod("DoPayAttackCost", BindingFlags.Instance | BindingFlags.NonPublic);

		private const string PopupPanelPath = "Panel";

		private const string PopupHumanContentsPath = "Panel/Human List/Viewport/Content";

		private const string PopupTargetDropdownPath = "Target/Dropdown";

		private const string PopupMethodDropdownPath = "Method/Dropdown";

		private const string PopupRangedToggleAnchorName = "GT_Combat_RangedToggleAnchor";

		private const string PopupRangedToggleButtonName = "GT_Combat_RangedToggle";

		private const string PopupOnFootCountAnchorName = "GT_Combat_OnFootCountAnchor";

		private const string PopupOnFootCountButtonName = "GT_Combat_OnFootCount";

		private const string PopupOnFootFilterAnchorName = "GT_Combat_OnFootFilterAnchor";

		private const string PopupOnFootFilterButtonName = "GT_Combat_OnFootFilter";

		private const string PopupDriveByTargetAnchorName = "GT_Combat_DriveByTargetAnchor";

		private const string PopupDriveByTargetButtonName = "GT_Combat_DriveByTarget";

		private const string PopupVehicleInfoAnchorName = "GT_Combat_VehicleInfoAnchor";

		private const string PopupVehicleInfoLabelName = "GT_Combat_VehicleInfoLabel";

		private const string PopupTargetVehicleInfoAnchorName = "GT_Combat_TargetVehicleInfoAnchor";

		private const string PopupTargetVehicleInfoLabelName = "GT_Combat_TargetVehicleInfoLabel";

		private static readonly HashSet<string> PopupCommitTokens = new HashSet<string>(StringComparer.Ordinal);

		private static readonly Dictionary<int, string> PopupCommitTokenById = new Dictionary<int, string>();

		private static readonly Dictionary<int, GroupedCombatMode> PopupCombatModeById = new Dictionary<int, GroupedCombatMode>();

		private static readonly Dictionary<int, int> PopupOnFootCountById = new Dictionary<int, int>();

		private static readonly Dictionary<int, OnFootWeaponFilter> PopupOnFootFilterById = new Dictionary<int, OnFootWeaponFilter>();

		private static readonly Dictionary<int, DriveByTargetMode> PopupDriveByTargetModeById = new Dictionary<int, DriveByTargetMode>();

		private static readonly Dictionary<int, Dictionary<ulong, ulong>> PopupSelectedTargetPeepIdsById = new Dictionary<int, Dictionary<ulong, ulong>>();

		private static readonly Dictionary<int, Dictionary<ulong, string>> PopupSelectedWeaponIdsById = new Dictionary<int, Dictionary<ulong, string>>();

		private static readonly HashSet<string> PopupVehicleCrewInfoLogKeys = new HashSet<string>(StringComparer.Ordinal);

		private static int _groupedCombatTransactionDepth;

		private static int _groupedCombatTransactionSerial;

		private static string _groupedCombatTransactionSource = string.Empty;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo popupOnFight = typeof(CombatPopupPlanning).GetMethod("OnFight", BindingFlags.Instance | BindingFlags.NonPublic);
				if (popupOnFight != null)
				{
					harmony.Patch(popupOnFight, prefix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(OnFightPrefix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatPopupPlanning.OnFight");
				}

				MethodInfo performHumanCombat = typeof(CombatManager).GetMethod("PerformHumanCombat", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Entity), typeof(Entity), typeof(WeaponConfig) }, null);
				if (performHumanCombat != null)
				{
					harmony.Patch(performHumanCombat, prefix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(PerformHumanCombatPrefix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatManager.PerformHumanCombat");
				}

				MethodInfo performAiCombat = typeof(CombatManager).GetMethod("PerformAICombat", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(CrewAssignment), typeof(CrewAssignment) }, null);
				if (performAiCombat != null)
				{
					harmony.Patch(performAiCombat, prefix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(PerformAICombatPrefix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatManager.PerformAICombat");
				}

				MethodInfo refreshContents = typeof(CombatPopupPlanning).GetMethod("RefreshContents", BindingFlags.Instance | BindingFlags.NonPublic);
				if (refreshContents != null)
				{
					harmony.Patch(refreshContents, prefix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(CombatPopupRefreshContentsPrefix)), postfix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(CombatPopupRefreshContentsPostfix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatPopupPlanning.RefreshContents");
				}

				MethodInfo popupOnCancel = typeof(CombatPopupPlanning).GetMethod("OnCancel", BindingFlags.Instance | BindingFlags.NonPublic);
				if (popupOnCancel != null)
				{
					harmony.Patch(popupOnCancel, prefix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(OnCancelPrefix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatPopupPlanning.OnCancel");
				}

				MethodInfo popupRelease = typeof(CombatPopupPlanning).GetMethod("ReleaseOnPop", BindingFlags.Instance | BindingFlags.NonPublic);
				if (popupRelease != null)
				{
					harmony.Patch(popupRelease, postfix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(ReleaseOnPopPostfix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatPopupPlanning.ReleaseOnPop");
				}

				MethodInfo onTargetRowSelection = typeof(CombatPopupPlanning).GetMethod("OnTargetRowSelection", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(TMP_Dropdown), typeof(GameObject), typeof(int) }, null);
				if (onTargetRowSelection != null)
				{
					harmony.Patch(onTargetRowSelection, postfix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(CombatPopupTargetSelectionPostfix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatPopupPlanning.OnTargetRowSelection");
				}

				MethodInfo onArrow = typeof(CombatPopupPlanning).GetMethod("OnArrow", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(CombatCardContext), typeof(int) }, null);
				if (onArrow != null)
				{
					harmony.Patch(onArrow, postfix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(CombatPopupArrowPostfix)));
					VerificationLog("VehicleGroupCombat", "hooked CombatPopupPlanning.OnArrow");
				}

				MethodInfo unassignCrewFromVehicle = typeof(PlayerCrew).GetMethod("UnassignCrewFromVehicle", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(EntityID) }, null);
				if (unassignCrewFromVehicle != null)
				{
					harmony.Patch(unassignCrewFromVehicle, prefix: new HarmonyMethod(typeof(VehicleGroupCombatPatch), nameof(UnassignCrewFromVehiclePrefix)));
					VerificationLog("VehicleGroupCombat", "hooked PlayerCrew.UnassignCrewFromVehicle");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch failed: " + ex.Message);
			}
		}

		private static void CombatPopupRefreshContentsPrefix(CombatPopupPlanning __instance)
		{
			try
			{
				CapturePopupSelectionState(__instance);
				NormalizePopupCombatLists(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.CombatPopupRefreshContentsPrefix: " + ex.Message);
			}
		}

		private static void CombatPopupRefreshContentsPostfix(CombatPopupPlanning __instance)
		{
			try
			{
				EnsureValidPopupCombatMode(__instance);
				EnsureCombatPopupRangedToggle(__instance);
				EnsureCombatPopupOnFootCountToggle(__instance);
				EnsureCombatPopupOnFootFilterToggle(__instance);
				EnsureCombatPopupDriveByTargetToggle(__instance);
				EnsureCombatPopupVehicleInfoLabel(__instance);
				EnsureCombatPopupTargetVehicleInfoLabel(__instance);
				RestorePopupTargetSelections(__instance);
				RefreshGroupedPopupWeaponRows(__instance);
				RefreshPopupVehicleLabels(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.CombatPopupRefreshContents: " + ex.Message);
			}
		}

		private static void CombatPopupTargetSelectionPostfix(CombatPopupPlanning __instance)
		{
			try
			{
				RefreshPopupVehicleLabels(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.CombatPopupTargetSelectionPostfix: " + ex.Message);
			}
		}

		private static void CombatPopupArrowPostfix(CombatPopupPlanning __instance)
		{
			try
			{
				RefreshPopupVehicleLabels(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.CombatPopupArrowPostfix: " + ex.Message);
			}
		}

		private static void RefreshPopupVehicleLabels(CombatPopupPlanning popup)
		{
			if (popup == null)
			{
				return;
			}
			EnsureCombatPopupOnFootCountToggle(popup);
			EnsureCombatPopupOnFootFilterToggle(popup);
			EnsureCombatPopupDriveByTargetToggle(popup);
			EnsureCombatPopupVehicleInfoLabel(popup);
			EnsureCombatPopupTargetVehicleInfoLabel(popup);
			LogPopupVehicleCrewInfo(popup);
		}

		private static void CapturePopupSelectionState(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return;
			}
			GameObject popupRoot = popup?.GameObject;
			GameObject humanContents = popupRoot?.GetChild(PopupHumanContentsPath);
			if (humanContents == null)
			{
				return;
			}
			var targetSelections = new Dictionary<ulong, ulong>();
			var weaponSelections = new Dictionary<ulong, string>();
			foreach (Transform child in humanContents.transform)
			{
				CombatCardContext ctx = child.gameObject.GetComponent<CombatCardContext>();
				if (ctx?.peep == null || !ctx.peep.Id.IsValid)
				{
					continue;
				}
				TMP_Dropdown targetDropdown = child.gameObject.GetDropdown(PopupTargetDropdownPath);
				Entity selectedTarget = targetDropdown?.GetCurrentOptionsItem()?.data as Entity;
				if (selectedTarget != null && selectedTarget.Id.IsValid)
				{
					targetSelections[ctx.peep.Id.id] = selectedTarget.Id.id;
				}
				TMP_Dropdown methodDropdown = child.gameObject.GetDropdown(PopupMethodDropdownPath);
				WeaponConfig selectedWeapon = methodDropdown?.GetCurrentOptionsItem()?.data as WeaponConfig;
				if (selectedWeapon != null)
				{
					weaponSelections[ctx.peep.Id.id] = GetWeaponId(selectedWeapon);
				}
			}
			if (targetSelections.Count > 0)
			{
				PopupSelectedTargetPeepIdsById[popupId] = targetSelections;
			}
			if (weaponSelections.Count > 0)
			{
				PopupSelectedWeaponIdsById[popupId] = weaponSelections;
			}
		}

		private static void RestorePopupTargetSelections(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0 || !PopupSelectedTargetPeepIdsById.TryGetValue(popupId, out Dictionary<ulong, ulong> selectedTargets) || selectedTargets == null || selectedTargets.Count == 0)
			{
				return;
			}
			GameObject popupRoot = popup?.GameObject;
			GameObject humanContents = popupRoot?.GetChild(PopupHumanContentsPath);
			if (humanContents == null)
			{
				return;
			}
			foreach (Transform child in humanContents.transform)
			{
				GameObject card = child.gameObject;
				CombatCardContext ctx = card.GetComponent<CombatCardContext>();
				if (ctx?.peep == null || !ctx.peep.Id.IsValid || !selectedTargets.TryGetValue(ctx.peep.Id.id, out ulong targetPeepId))
				{
					continue;
				}
				TMP_Dropdown dropdown = card.GetDropdown(PopupTargetDropdownPath);
				if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0)
				{
					continue;
				}
				int selectedIndex = -1;
				for (int optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
				{
					Entity optionTarget = dropdown.GetOptionsItem(optionIndex)?.data as Entity;
					if (optionTarget != null && optionTarget.Id.id == targetPeepId)
					{
						selectedIndex = optionIndex;
						break;
					}
				}
				if (selectedIndex < 0)
				{
					continue;
				}
				dropdown.SetValueWithoutNotify(selectedIndex);
				PopupOnTargetRowSelectionMethod?.Invoke(popup, new object[] { dropdown, card, selectedIndex });
			}
		}

		private static void OnCancelPrefix(CombatPopupPlanning __instance)
		{
			string token = GetPopupCommitToken(__instance);
			if (!PopupCommitTokens.Contains(token))
			{
				VerificationLog("VehicleGroupCombat", $"popup-cancel token={token} committed=false");
			}
			CleanupPopupCommitState(__instance);
		}

		private static void ReleaseOnPopPostfix(CombatPopupPlanning __instance)
		{
			CleanupPopupCommitState(__instance);
		}

		private static bool UnassignCrewFromVehiclePrefix(PlayerCrew __instance, EntityID peepId, ref EntityID __result)
		{
			if (GameplayTweaksPlugin.TrySuppressFederalArrestVehicleUnassign(__instance, peepId, out EntityID vehicleId))
			{
				__result = vehicleId;
				return false;
			}

			if (!IsGroupedCombatTransactionActive())
			{
				return true;
			}
			VerificationLog("VehicleGroupCombat", $"vehicle-reassign source={_groupedCombatTransactionSource} pid={__instance?.PID.id ?? -1} peep={peepId.id}");
			return true;
		}

		private static int GetPopupId(CombatPopupPlanning popup)
		{
			return popup == null ? 0 : RuntimeHelpers.GetHashCode(popup);
		}

		private static string GetPopupCommitToken(CombatPopupPlanning popup)
		{
			int day = G.GetNow().days;
			int popupId = GetPopupId(popup);
			return $"{day}:{popupId}";
		}

		private static void CleanupPopupCommitState(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return;
			}
			if (PopupCommitTokenById.TryGetValue(popupId, out string token))
			{
				PopupCommitTokens.Remove(token);
				PopupCommitTokenById.Remove(popupId);
			}
			PopupCombatModeById.Remove(popupId);
			PopupOnFootCountById.Remove(popupId);
			PopupOnFootFilterById.Remove(popupId);
			PopupDriveByTargetModeById.Remove(popupId);
			PopupSelectedTargetPeepIdsById.Remove(popupId);
			PopupSelectedWeaponIdsById.Remove(popupId);
		}

		private static GroupedCombatMode GetPopupCombatMode(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return GroupedCombatMode.OnFoot;
			}
			if (!PopupCombatModeById.TryGetValue(popupId, out GroupedCombatMode mode))
			{
				mode = GetDefaultPopupCombatMode(popup);
				PopupCombatModeById[popupId] = mode;
			}
			return mode;
		}

		private static void SetPopupCombatMode(CombatPopupPlanning popup, GroupedCombatMode mode)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return;
			}
			PopupCombatModeById[popupId] = mode;
		}

		private static int GetPopupOnFootCount(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return 1;
			}
			int max = Math.Max(1, GetMaxOnFootPopupAttackers(popup));
			if (!PopupOnFootCountById.TryGetValue(popupId, out int count))
			{
				count = max;
				PopupOnFootCountById[popupId] = count;
			}
			return Mathf.Clamp(count, 1, max);
		}

		private static void SetPopupOnFootCount(CombatPopupPlanning popup, int count)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return;
			}
			int max = Math.Max(1, GetMaxOnFootPopupAttackers(popup));
			PopupOnFootCountById[popupId] = Mathf.Clamp(count, 1, max);
		}

		private static OnFootWeaponFilter GetPopupOnFootWeaponFilter(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return OnFootWeaponFilter.Any;
			}
			if (!PopupOnFootFilterById.TryGetValue(popupId, out OnFootWeaponFilter filter))
			{
				filter = OnFootWeaponFilter.Any;
				PopupOnFootFilterById[popupId] = filter;
			}
			return filter;
		}

		private static void SetPopupOnFootWeaponFilter(CombatPopupPlanning popup, OnFootWeaponFilter filter)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return;
			}
			PopupOnFootFilterById[popupId] = filter;
		}

		private static DriveByTargetMode GetPopupDriveByTargetMode(CombatPopupPlanning popup)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return DriveByTargetMode.FocusOne;
			}
			if (!PopupDriveByTargetModeById.TryGetValue(popupId, out DriveByTargetMode mode))
			{
				mode = DriveByTargetMode.FocusOne;
				PopupDriveByTargetModeById[popupId] = mode;
			}
			return mode;
		}

		private static void SetPopupDriveByTargetMode(CombatPopupPlanning popup, DriveByTargetMode mode)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0)
			{
				return;
			}
			PopupDriveByTargetModeById[popupId] = mode;
		}

		private static GroupedCombatMode GetDefaultPopupCombatMode(CombatPopupPlanning popup)
		{
			return IsDriveByModeAvailableForPopup(popup)
				? GroupedCombatMode.DriveBy
				: GroupedCombatMode.OnFoot;
		}

		private static void EnsureValidPopupCombatMode(CombatPopupPlanning popup)
		{
			GroupedCombatMode mode = GetPopupCombatMode(popup);
			if (mode == GroupedCombatMode.DriveBy && !IsDriveByModeAvailableForPopup(popup))
			{
				SetPopupCombatMode(popup, GroupedCombatMode.OnFoot);
			}
			int max = Math.Max(1, GetMaxOnFootPopupAttackers(popup));
			SetPopupOnFootCount(popup, max > 0 ? GetPopupOnFootCount(popup) : 1);
		}

		private static int GetMaxOnFootPopupAttackers(CombatPopupPlanning popup)
		{
			List<Entity> myCrew = PopupMyCrewField?.GetValue(popup) as List<Entity>;
			if (myCrew == null)
			{
				return 1;
			}
			int max = 1;
			foreach (Entity peep in myCrew)
			{
				CrewAssignment crew = TryFindCrewForPeep(peep);
				if (!crew.IsValid || !crew.IsInVehicle)
				{
					continue;
				}
				max = Math.Max(max, GetEligibleAttackersForVehicle(crew, requireActionPoints: true, GroupedCombatMode.OnFoot).Count);
			}
			return max;
		}

		private static bool IsDriveByModeAvailableForPopup(CombatPopupPlanning popup)
		{
			List<Entity> myCrew = PopupMyCrewField?.GetValue(popup) as List<Entity>;
			if (myCrew == null)
			{
				return false;
			}
			return myCrew
				.Where(ShouldUseGroupedHumanVehicleCombat)
				.Select(TryFindCrewForPeep)
				.Any(crew => crew.IsValid && HasEligibleDriveByAttackers(crew, requireActionPoints: true));
		}

		private static void NormalizePopupCombatLists(CombatPopupPlanning popup)
		{
			if (popup == null)
			{
				return;
			}
			List<Entity> myCrew = PopupMyCrewField?.GetValue(popup) as List<Entity>;
			List<Entity> enemies = PopupEnemiesField?.GetValue(popup) as List<Entity>;
			List<Entity> normalizedMyCrew = NormalizePopupEntityList(myCrew, preferMappedHumanVehicleRepresentative: true, "human", collapseVehicleOccupants: true);
			List<Entity> expandedEnemies = ExpandPopupEnemyTargets(enemies);
			List<Entity> normalizedEnemies = NormalizePopupEntityList(expandedEnemies, preferMappedHumanVehicleRepresentative: false, "enemy", collapseVehicleOccupants: false);
			if (normalizedMyCrew != null)
			{
				PopupMyCrewField?.SetValue(popup, normalizedMyCrew);
			}
			if (normalizedEnemies != null)
			{
				PopupEnemiesField?.SetValue(popup, normalizedEnemies);
			}
			VerificationLog("VehicleGroupCombat", $"popup-dedupe token={GetPopupCommitToken(popup)} humanRaw={myCrew?.Count ?? 0} humanDeduped={normalizedMyCrew?.Count ?? 0} enemyRaw={enemies?.Count ?? 0} enemyDeduped={normalizedEnemies?.Count ?? 0}");
		}

		private static List<Entity> ExpandPopupEnemyTargets(List<Entity> enemies)
		{
			var expanded = new List<Entity>();
			var seenPeepIds = new HashSet<ulong>();
			foreach (Entity enemy in enemies ?? Enumerable.Empty<Entity>())
			{
				foreach (Entity expandedEnemy in ExpandPopupEnemyTargetEntity(enemy))
				{
					if (expandedEnemy == null || !expandedEnemy.Id.IsValid || !seenPeepIds.Add(expandedEnemy.Id.id))
					{
						continue;
					}
					expanded.Add(expandedEnemy);
				}
			}
			return expanded;
		}

		private static IEnumerable<Entity> ExpandPopupEnemyTargetEntity(Entity enemy)
		{
			if (enemy == null)
			{
				yield break;
			}
			CrewAssignment crew = TryFindCrewForPeep(enemy);
			if (!crew.IsValid || !crew.IsInVehicle || !crew.VehicleID.IsValid)
			{
				yield return enemy;
				yield break;
			}
			PlayerCrew playerCrew = crew.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
			if (playerCrew == null)
			{
				yield return enemy;
				yield break;
			}
			List<CrewAssignment> occupants = GetVehicleCombatOccupants(crew)
				.Where(item => item.IsValid && item.IsNotDead && item.peepId.IsValid)
				.OrderBy(item => item.peepId == enemy.Id ? 0 : 1)
				.ThenBy(item => item.peepId.id)
				.ToList();
			if (occupants.Count == 0)
			{
				yield return enemy;
				yield break;
			}
			foreach (CrewAssignment occupant in occupants)
			{
				Entity peep = occupant.GetPeep();
				if (peep != null && peep.components?.agent?.HasHealthPointsLeft == true)
				{
					yield return peep;
				}
			}
		}

		private static List<Entity> NormalizePopupEntityList(List<Entity> entities, bool preferMappedHumanVehicleRepresentative, string side, bool collapseVehicleOccupants)
		{
			if (entities == null)
			{
				return null;
			}
			var normalized = new List<Entity>();
			var grouped = new Dictionary<string, List<Entity>>(StringComparer.Ordinal);
			var orderedKeys = new List<string>();
			foreach (Entity entity in entities)
			{
				string key = BuildPopupEntityGroupKey(entity, collapseVehicleOccupants);
				if (!grouped.TryGetValue(key, out List<Entity> bucket))
				{
					bucket = new List<Entity>();
					grouped[key] = bucket;
					orderedKeys.Add(key);
				}
				if (entity != null)
				{
					bucket.Add(entity);
				}
			}
			foreach (string key in orderedKeys)
			{
				List<Entity> bucket = grouped[key];
				Entity representative = ChoosePopupRepresentative(bucket, preferMappedHumanVehicleRepresentative);
				if (representative == null)
				{
					continue;
				}
				normalized.Add(representative);
				if (bucket.Count > 1)
				{
					VerificationLog("VehicleGroupCombat", $"popup-group side={side} key={key} representative={representative.Id.id} members={bucket.Count}");
				}
			}
			return normalized;
		}

		private static string BuildPopupEntityGroupKey(Entity peep, bool collapseVehicleOccupants = true)
		{
			CrewAssignment crew = TryFindCrewForPeep(peep);
			if (collapseVehicleOccupants && crew.IsValid && crew.IsInVehicle && crew.VehicleID.IsValid)
			{
				return "vehicle:" + crew.VehicleID.id.ToString(CultureInfo.InvariantCulture);
			}
			return "peep:" + (peep?.Id.id.ToString(CultureInfo.InvariantCulture) ?? "0");
		}

		private static Entity ChoosePopupRepresentative(List<Entity> bucket, bool preferMappedHumanVehicleRepresentative)
		{
			if (bucket == null || bucket.Count == 0)
			{
				return null;
			}
			Entity first = bucket[0];
			if (!preferMappedHumanVehicleRepresentative)
			{
				return first;
			}
			CrewAssignment crew = TryFindCrewForPeep(first);
			if (!crew.IsValid || !crew.IsInVehicle || !crew.VehicleID.IsValid)
			{
				return first;
			}
			PlayerInfo player = first.data?.agent?.pid.FindPlayer();
			PlayerCrew playerCrew = player?.crew;
			if (playerCrew == null || !playerCrew.PID.IsHumanPlayer)
			{
				return first;
			}
			EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(playerCrew, crew.VehicleID);
			if (!driverPeepId.IsValid)
			{
				return first;
			}
			Entity representative = bucket.FirstOrDefault(item => item != null && item.Id == driverPeepId);
			return representative ?? driverPeepId.FindEntity() ?? first;
		}

		private static List<PopupCombatAction> BuildPopupCombatActions(CombatPopupPlanning popup, List<Entity> myCrew)
		{
			var actions = new List<PopupCombatAction>();
			if (popup == null || myCrew == null)
			{
				return actions;
			}
			var seenKeys = new HashSet<string>(StringComparer.Ordinal);
			foreach (Entity peep in myCrew)
			{
				if (peep == null)
				{
					continue;
				}
				Entity target = PopupFindTargetForMethod?.Invoke(popup, new object[] { peep }) as Entity;
				WeaponConfig weapon = PopupFindWeaponForMethod?.Invoke(popup, new object[] { peep }) as WeaponConfig;
				if (target == null || weapon == null || !target.components.agent.HasHealthPointsLeft)
				{
					continue;
				}
				CrewAssignment attackerCrew = TryFindCrewForPeep(peep);
				CrewAssignment targetCrew = TryFindCrewForPeep(target);
				if (!attackerCrew.IsValid || !targetCrew.IsValid)
				{
					continue;
				}
				bool isGrouped = ShouldUseGroupedHumanVehicleCombat(peep);
				string actionKey = isGrouped && attackerCrew.VehicleID.IsValid
					? "vehicle:" + attackerCrew.VehicleID.id.ToString(CultureInfo.InvariantCulture)
					: "peep:" + peep.Id.id.ToString(CultureInfo.InvariantCulture);
				if (!seenKeys.Add(actionKey))
				{
					VerificationLog("VehicleGroupCombat", $"popup-skip-duplicate token={GetPopupCommitToken(popup)} key={actionKey} peep={peep.Id.id} target={target.Id.id}");
					continue;
				}
				actions.Add(new PopupCombatAction
				{
					RepresentativePeep = peep,
					Target = target,
					Weapon = weapon,
					AttackerCrew = attackerCrew,
					TargetCrew = targetCrew,
					IsGrouped = isGrouped,
					ActionKey = actionKey
				});
			}
			return actions;
		}

		private static bool IsGroupedCombatTransactionActive()
		{
			return _groupedCombatTransactionDepth > 0;
		}

		private static void BeginGroupedCombatTransaction(string source, CrewAssignment attacker, CrewAssignment target)
		{
			_groupedCombatTransactionDepth++;
			_groupedCombatTransactionSerial++;
			string attackerVehicle = attacker.IsValid && attacker.VehicleID.IsValid ? attacker.VehicleID.id.ToString(CultureInfo.InvariantCulture) : "0";
			string targetVehicle = target.IsValid && target.VehicleID.IsValid ? target.VehicleID.id.ToString(CultureInfo.InvariantCulture) : "0";
			_groupedCombatTransactionSource = $"{source}:txn={_groupedCombatTransactionSerial}:attackerVehicle={attackerVehicle}:targetVehicle={targetVehicle}";
		}

		private static void EndGroupedCombatTransaction()
		{
			if (_groupedCombatTransactionDepth > 0)
			{
				_groupedCombatTransactionDepth--;
			}
			if (_groupedCombatTransactionDepth <= 0)
			{
				_groupedCombatTransactionDepth = 0;
				_groupedCombatTransactionSource = string.Empty;
				ClearActiveGroupedCombatAttackerSnapshot();
			}
		}

		private static bool OnFightPrefix(CombatPopupPlanning __instance)
		{
			try
			{
				List<Entity> myCrew = PopupMyCrewField?.GetValue(__instance) as List<Entity>;
				if (myCrew == null || myCrew.Count == 0 || !myCrew.Any(ShouldUseGroupedHumanVehicleCombat))
				{
					return true;
				}
				string popupToken = GetPopupCommitToken(__instance);
				if (!PopupCommitTokens.Add(popupToken))
				{
					VerificationLog("VehicleGroupCombat", $"fight-duplicate token={popupToken} blocked=true");
					return false;
				}
				PopupCommitTokenById[GetPopupId(__instance)] = popupToken;

				var results = new List<CombatResults>();
				List<PopupCombatAction> actions = BuildPopupCombatActions(__instance, myCrew);
				VerificationLog("VehicleGroupCombat", $"fight-commit token={popupToken} rawRows={myCrew.Count} dedupedActions={actions.Count} groupedActions={actions.Count(item => item.IsGrouped)}");
				foreach (PopupCombatAction action in actions)
				{
					if (action.IsGrouped)
					{
						GroupedCombatMode mode = GetEffectiveCombatModeForCrew(__instance, action.AttackerCrew, requireActionPoints: true);
						int attackerCount = mode == GroupedCombatMode.OnFoot ? GetRequestedOnFootAttackerCount(__instance, action.AttackerCrew, requireActionPoints: true) : CountEligibleDriveByShooters(global::Game.Game.ctx?.simman?.combat, action.AttackerCrew, requireActionPoints: true, applyPlayerWeaponFilters: true);
						VerificationLog("VehicleGroupCombat", $"grouped-mode source=CombatPopup mode={FormatCombatMode(mode)} count={attackerCount} attackerVehicle={action.AttackerCrew.VehicleID.id} targetVehicle={action.TargetCrew.VehicleID.id}");
						ExecuteVehicleGroupCombat(global::Game.Game.ctx.simman.combat, action.AttackerCrew, action.TargetCrew, action.Weapon, mode, consumeHumanAttackCost: true, results, observerSource: "CombatPopup", requestedOnFootCount: mode == GroupedCombatMode.OnFoot ? (int?)attackerCount : null, driveByTargetMode: GetPopupDriveByTargetMode(__instance));
					}
					else
					{
						CombatResults result = global::Game.Game.ctx.simman.combat.PerformHumanCombat(action.RepresentativePeep, action.Target, action.Weapon);
						if (result != null)
							results.Add(result);
					}
				}

				global::Game.Game.serv.ui.RemovePopup(__instance);
				global::Game.Game.ctx.selection.ClearActive();
				ShowCombatResults(results, immediate: true, isAttackerAI: false);
				return false;
			}
			catch (Exception ex)
			{
				CleanupPopupCommitState(__instance);
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.OnFight: " + ex.Message);
				return true;
			}
		}

		private static bool PerformHumanCombatPrefix(CombatManager __instance, Entity attacker, Entity target, WeaponConfig attackWeapon, ref CombatResults __result)
		{
			try
			{
				if (!ShouldUseGroupedHumanVehicleCombat(attacker))
					return true;

				CrewAssignment attackerCrew = TryFindCrewForPeep(attacker);
				CrewAssignment targetCrew = TryFindCrewForPeep(target);
				if (!attackerCrew.IsValid || !targetCrew.IsValid)
					return true;

				var results = new List<CombatResults>();
				GroupedCombatMode mode = GetDefaultExecutionMode(attackerCrew, attackWeapon, requireActionPoints: true);
				VerificationLog("VehicleGroupCombat", $"grouped-mode source=PerformHumanCombat mode={FormatCombatMode(mode)} count={(mode == GroupedCombatMode.OnFoot ? GetEligibleAttackersForVehicle(attackerCrew, requireActionPoints: true, GroupedCombatMode.OnFoot).Count : CountEligibleDriveByShooters(__instance, attackerCrew, requireActionPoints: true, applyPlayerWeaponFilters: true))} attackerVehicle={attackerCrew.VehicleID.id} targetVehicle={targetCrew.VehicleID.id}");
				ExecuteVehicleGroupCombat(__instance, attackerCrew, targetCrew, attackWeapon, mode, consumeHumanAttackCost: true, results, observerSource: "PerformHumanCombat", driveByTargetMode: DriveByTargetMode.FocusOne);
				__result = results.FirstOrDefault();
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.PerformHumanCombat: " + ex.Message);
				return true;
			}
		}

		private static bool PerformAICombatPrefix(CombatManager __instance, CrewAssignment attacker, CrewAssignment target)
		{
			try
			{
				if (!attacker.IsValid || !target.IsValid)
				{
					return true;
				}

				Entity attackerPeep = attacker.GetPeep();
				Entity targetPeep = target.GetPeep();
				bool attackerIsHuman = attackerPeep?.data?.agent?.pid.IsHumanPlayer == true;
				bool targetIsHuman = targetPeep?.data?.agent?.pid.IsHumanPlayer == true;
				bool humanInvolved = attackerIsHuman || targetIsHuman;
				if (!attacker.IsInVehicle && !(targetIsHuman && target.IsInVehicle))
				{
					return true;
				}

				int attackerEligibleOccupants = attacker.IsValid ? GetEligibleAttackersForVehicle(attacker, requireActionPoints: false).Count : 0;
				int targetEligibleDefenders = target.IsValid ? GetEligibleDefendersForVehicle(target).Count : 0;
				int aiDriveByShooterCount = attacker.IsValid ? CountEligibleDriveByShooters(__instance, attacker, requireActionPoints: false, applyPlayerWeaponFilters: false) : 0;
				int aiLivingGangCrewCount = attacker.IsValid ? GetLivingGangCrewCount(attacker) : 0;
				bool attackerIsAi = attacker.IsValid && !attackerIsHuman;
				bool attackerIsCopRetaliatingOnHuman = attacker.IsValid
					&& attackerPeep?.data?.agent?.pid.FindPlayer()?.IsJustCop == true
					&& target.IsValid
					&& targetIsHuman;
				int minAiLivingCrewForVehicleCombat = attackerIsCopRetaliatingOnHuman ? 1 : 3;
				int minAiDriveByShooters = attackerIsCopRetaliatingOnHuman ? 1 : 2;
				int minAiOnFootOccupants = attackerIsCopRetaliatingOnHuman ? 1 : 2;
				bool aiCanGroup = attackerIsAi
					&& attacker.IsInVehicle
					&& aiLivingGangCrewCount >= minAiLivingCrewForVehicleCombat;
				bool aiDriveByAttack = attackerIsAi
					&& aiCanGroup
					&& aiDriveByShooterCount >= minAiDriveByShooters;
				bool aiGroupedOnFoot = attackerIsAi
					&& aiCanGroup
					&& attackerEligibleOccupants >= minAiOnFootOccupants;
				bool humanGroupedAttack = attacker.IsValid && attackerIsHuman && attackerEligibleOccupants > 1;
				bool defendingHumanVehicle = target.IsValid && targetIsHuman && target.IsInVehicle && targetEligibleDefenders > 1;
				if (!humanGroupedAttack && !defendingHumanVehicle && !aiDriveByAttack && !aiGroupedOnFoot)
				{
					if (humanInvolved && attackerIsAi && attacker.IsInVehicle)
					{
						string reason = aiLivingGangCrewCount < minAiLivingCrewForVehicleCombat
							? (attackerIsCopRetaliatingOnHuman ? "cop-retaliation-no-crew" : "gang-too-small")
							: (attackerEligibleOccupants <= 1
								? "single-occupant"
								: (aiDriveByShooterCount <= 0 ? "no-passenger-shooters" : "no-valid-weapons"));
						VerificationLog("VehicleGroupCombat.AI", $"mode-skip source=PerformAICombat reason={reason} attackerVehicle={attacker.VehicleID.id} livingCrew={aiLivingGangCrewCount} shooterCount={aiDriveByShooterCount} eligibleOccupants={attackerEligibleOccupants} copRetaliationOverride={attackerIsCopRetaliatingOnHuman}");
					}
					return true;
				}
				PlayerID? attackerPid = attackerPeep?.data?.agent?.pid;
				PlayerID? targetPid = targetPeep?.data?.agent?.pid;
				if (attackerIsCopRetaliatingOnHuman
					&& attackerPid.HasValue
					&& targetPid.HasValue
					&& ShouldSuppressIncomingAiCopCombat(attackerPid.Value, targetPid.Value))
				{
					VerificationLog("VehicleGroupCombat.AI", $"mode-skip source=PerformAICombat reason=cop-retaliation-suppressed attackerVehicle={attacker.VehicleID.id} targetVehicle={target.VehicleID.id} attackerPid={attackerPid.Value.id} targetPid={targetPid.Value.id}");
					return false;
				}
				GroupedCombatMode mode = aiDriveByAttack ? GroupedCombatMode.DriveBy : GroupedCombatMode.OnFoot;
				if (humanInvolved)
				{
					VerificationLog("VehicleGroupCombat.AI", $"entry source=PerformAICombat attackerPid={(attackerPid.HasValue ? attackerPid.Value.id : -1)} targetPid={(targetPid.HasValue ? targetPid.Value.id : -1)} humanGroupedAttack={humanGroupedAttack} defendingHumanVehicle={defendingHumanVehicle} aiDriveByAttack={aiDriveByAttack} aiGroupedOnFoot={aiGroupedOnFoot} attackerInVehicle={attacker.IsValid && attacker.IsInVehicle} targetInVehicle={target.IsValid && target.IsInVehicle} attackerEligibleOccupants={attackerEligibleOccupants} targetEligibleDefenders={targetEligibleDefenders} aiDriveByShooterCount={aiDriveByShooterCount} livingGangCrew={aiLivingGangCrewCount} copRetaliationOverride={attackerIsCopRetaliatingOnHuman}");
					VerificationLog("VehicleGroupCombat.AI", $"ai-vehicle-combat mode={FormatCombatMode(mode)} grouped={(aiDriveByAttack || aiGroupedOnFoot)} attackerVehicle={attacker.VehicleID.id} targetVehicle={target.VehicleID.id} copRetaliationOverride={attackerIsCopRetaliatingOnHuman}");
					VerificationLog("VehicleGroupCombat.AI", $"mode source=PerformAICombat mode={FormatCombatMode(mode)} attackerVehicle={attacker.VehicleID.id} targetVehicle={target.VehicleID.id}");
				}

				var results = new List<CombatResults>();
				ExecuteVehicleGroupCombat(__instance, attacker, target, selectedAttackWeapon: null, mode, consumeHumanAttackCost: false, results, observerSource: "PerformAICombat", driveByTargetMode: DriveByTargetMode.FocusOne);
				if (targetIsHuman)
				{
					ShowCombatResults(results, immediate: false, isAttackerAI: true);
				}
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.PerformAICombat: " + ex.Message);
				return true;
			}
		}

		private static bool ShouldSuppressIncomingAiCopCombat(PlayerID attackerPid, PlayerID targetPid)
		{
			try
			{
				Type copWarSystemType = Type.GetType("CopKilling.CopWarSystem, CopKilling");
				if (copWarSystemType == null)
				{
					return false;
				}

				if (_copWarShouldSuppressIncomingAiCopCombatMethod == null
					|| _copWarShouldSuppressIncomingAiCopCombatMethod.DeclaringType != copWarSystemType)
				{
					_copWarShouldSuppressIncomingAiCopCombatMethod = copWarSystemType.GetMethod("ShouldSuppressIncomingAiCopCombat", BindingFlags.Static | BindingFlags.Public);
				}

				if (_copWarShouldSuppressIncomingAiCopCombatMethod?.Invoke(null, new object[] { attackerPid, targetPid }) is bool shouldSuppress)
				{
					return shouldSuppress;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ShouldSuppressIncomingAiCopCombat failed: " + ex.Message);
			}

			return false;
		}

		private static void EnsureCombatPopupRangedToggle(CombatPopupPlanning popup)
		{
			GameObject panel = GetCombatPopupPanel(popup);
			if (panel == null)
			{
				return;
			}
			bool showToggle = IsGroupedHumanVehiclePopup(popup);
			GameObject anchor = panel.transform.Find(PopupRangedToggleAnchorName)?.gameObject;
			if (!showToggle)
			{
				if (anchor != null)
				{
					anchor.SetActive(false);
				}
				return;
			}
			if (anchor == null)
			{
				anchor = CreateCombatPopupRangedToggleAnchor(panel.transform);
			}
			Button button = GetOrCreateCombatPopupRangedToggleButton(anchor.transform);
			if (button == null)
			{
				return;
			}
			anchor.SetActive(true);
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(delegate
			{
				OnCombatPopupRangedToggleClicked(popup);
			});
			UpdateCombatPopupRangedToggleVisual(popup, button);
		}

		private static void EnsureCombatPopupOnFootCountToggle(CombatPopupPlanning popup)
		{
			GameObject panel = GetCombatPopupPanel(popup);
			if (panel == null)
			{
				return;
			}
			bool showToggle = IsGroupedHumanVehiclePopup(popup);
			GameObject anchor = panel.transform.Find(PopupOnFootCountAnchorName)?.gameObject;
			if (!showToggle)
			{
				if (anchor != null)
				{
					anchor.SetActive(false);
				}
				return;
			}
			if (anchor == null)
			{
				anchor = CreateCombatPopupOnFootCountAnchor(panel.transform);
			}
			Button button = GetOrCreateCombatPopupOnFootCountButton(anchor.transform);
			if (button == null)
			{
				return;
			}
			anchor.SetActive(true);
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(delegate
			{
				OnCombatPopupOnFootCountClicked(popup);
			});
			UpdateCombatPopupOnFootCountVisual(popup, button);
		}

		private static void EnsureCombatPopupOnFootFilterToggle(CombatPopupPlanning popup)
		{
			GameObject panel = GetCombatPopupPanel(popup);
			if (panel == null)
			{
				return;
			}
			bool showToggle = IsGroupedHumanVehiclePopup(popup);
			GameObject anchor = panel.transform.Find(PopupOnFootFilterAnchorName)?.gameObject;
			if (!showToggle)
			{
				if (anchor != null)
				{
					anchor.SetActive(false);
				}
				return;
			}
			if (anchor == null)
			{
				anchor = CreateCombatPopupOnFootFilterAnchor(panel.transform);
			}
			Button button = GetOrCreateCombatPopupOnFootFilterButton(anchor.transform);
			if (button == null)
			{
				return;
			}
			anchor.SetActive(true);
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(delegate
			{
				OnCombatPopupOnFootFilterClicked(popup);
			});
			UpdateCombatPopupOnFootFilterVisual(popup, button);
		}

		private static void EnsureCombatPopupDriveByTargetToggle(CombatPopupPlanning popup)
		{
			GameObject panel = GetCombatPopupPanel(popup);
			if (panel == null)
			{
				return;
			}
			bool showToggle = IsGroupedHumanVehiclePopup(popup);
			GameObject anchor = panel.transform.Find(PopupDriveByTargetAnchorName)?.gameObject;
			if (!showToggle)
			{
				if (anchor != null)
				{
					anchor.SetActive(false);
				}
				return;
			}
			if (anchor == null)
			{
				anchor = CreateCombatPopupDriveByTargetAnchor(panel.transform);
			}
			Button button = GetOrCreateCombatPopupDriveByTargetButton(anchor.transform);
			if (button == null)
			{
				return;
			}
			anchor.SetActive(true);
			button.onClick.RemoveAllListeners();
			button.onClick.AddListener(delegate
			{
				OnCombatPopupDriveByTargetClicked(popup);
			});
			UpdateCombatPopupDriveByTargetVisual(popup, button);
		}

		private static void EnsureCombatPopupVehicleInfoLabel(CombatPopupPlanning popup)
		{
			GameObject panel = GetCombatPopupPanel(popup);
			if (panel == null)
			{
				return;
			}
			bool showLabel = IsGroupedHumanVehiclePopup(popup);
			GameObject anchor = panel.transform.Find(PopupVehicleInfoAnchorName)?.gameObject;
			if (!showLabel)
			{
				if (anchor != null)
				{
					anchor.SetActive(false);
				}
				return;
			}
			if (anchor == null)
			{
				anchor = CreateCombatPopupVehicleInfoAnchor(panel.transform);
			}
			Text label = GetOrCreateCombatPopupVehicleInfoLabel(anchor.transform);
			if (label == null)
			{
				return;
			}
			anchor.SetActive(true);
			UpdateCombatPopupVehicleInfoLabel(popup, label);
		}

		private static void EnsureCombatPopupTargetVehicleInfoLabel(CombatPopupPlanning popup)
		{
			GameObject panel = GetCombatPopupPanel(popup);
			if (panel == null)
			{
				return;
			}
			bool showLabel = IsGroupedHumanVehiclePopup(popup);
			GameObject anchor = panel.transform.Find(PopupTargetVehicleInfoAnchorName)?.gameObject;
			if (!showLabel)
			{
				if (anchor != null)
				{
					anchor.SetActive(false);
				}
				return;
			}
			if (anchor == null)
			{
				anchor = CreateCombatPopupTargetVehicleInfoAnchor(panel.transform);
			}
			Text label = GetOrCreateCombatPopupTargetVehicleInfoLabel(anchor.transform);
			if (label == null)
			{
				return;
			}
			anchor.SetActive(true);
			UpdateCombatPopupTargetVehicleInfoLabel(popup, label);
		}

		private static GameObject GetCombatPopupPanel(CombatPopupPlanning popup)
		{
			GameObject popupRoot = popup?.GameObject;
			if (popupRoot == null)
			{
				return null;
			}
			return popupRoot.GetChild(PopupPanelPath);
		}

		private static GameObject CreateCombatPopupRangedToggleAnchor(Transform panelTransform)
		{
			GameObject anchor = new GameObject(PopupRangedToggleAnchorName, typeof(RectTransform));
			anchor.transform.SetParent(panelTransform, false);
			RectTransform rect = anchor.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(0f, 0f);
			rect.pivot = new Vector2(0f, 0f);
			rect.anchoredPosition = new Vector2(12f, 52f);
			rect.sizeDelta = new Vector2(122f, 24f);
			return anchor;
		}

		private static GameObject CreateCombatPopupOnFootCountAnchor(Transform panelTransform)
		{
			GameObject anchor = new GameObject(PopupOnFootCountAnchorName, typeof(RectTransform));
			anchor.transform.SetParent(panelTransform, false);
			RectTransform rect = anchor.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(140f, -6f);
			rect.sizeDelta = new Vector2(132f, 24f);
			return anchor;
		}

		private static GameObject CreateCombatPopupOnFootFilterAnchor(Transform panelTransform)
		{
			GameObject anchor = new GameObject(PopupOnFootFilterAnchorName, typeof(RectTransform));
			anchor.transform.SetParent(panelTransform, false);
			RectTransform rect = anchor.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(278f, -6f);
			rect.sizeDelta = new Vector2(104f, 24f);
			return anchor;
		}

		private static GameObject CreateCombatPopupDriveByTargetAnchor(Transform panelTransform)
		{
			GameObject anchor = new GameObject(PopupDriveByTargetAnchorName, typeof(RectTransform));
			anchor.transform.SetParent(panelTransform, false);
			RectTransform rect = anchor.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 0f);
			rect.anchorMax = new Vector2(0f, 0f);
			rect.pivot = new Vector2(0f, 0f);
			rect.anchoredPosition = new Vector2(140f, 52f);
			rect.sizeDelta = new Vector2(186f, 24f);
			return anchor;
		}

		private static GameObject CreateCombatPopupVehicleInfoAnchor(Transform panelTransform)
		{
			GameObject anchor = new GameObject(PopupVehicleInfoAnchorName, typeof(RectTransform));
			anchor.transform.SetParent(panelTransform, false);
			RectTransform rect = anchor.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(12f, -34f);
			rect.sizeDelta = new Vector2(260f, 18f);
			return anchor;
		}

		private static GameObject CreateCombatPopupTargetVehicleInfoAnchor(Transform panelTransform)
		{
			GameObject anchor = new GameObject(PopupTargetVehicleInfoAnchorName, typeof(RectTransform));
			anchor.transform.SetParent(panelTransform, false);
			RectTransform rect = anchor.GetComponent<RectTransform>();
			rect.anchorMin = new Vector2(0f, 1f);
			rect.anchorMax = new Vector2(0f, 1f);
			rect.pivot = new Vector2(0f, 1f);
			rect.anchoredPosition = new Vector2(12f, -52f);
			rect.sizeDelta = new Vector2(300f, 18f);
			return anchor;
		}

		private static Button GetOrCreateCombatPopupRangedToggleButton(Transform parent)
		{
			Button button = parent.Find(PopupRangedToggleButtonName)?.GetComponent<Button>();
			if (button == null)
			{
				button = CrewRelationshipHandlerPatch.CreateButton(parent, PopupRangedToggleButtonName, string.Empty, null);
				if (button == null)
				{
					return null;
				}
				RectTransform rect = ((Component)button).GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
				LayoutElement layout = ((Component)button).GetComponent<LayoutElement>();
				if (layout != null)
				{
					layout.minWidth = 122f;
					layout.preferredWidth = 122f;
					layout.flexibleWidth = 0f;
					layout.minHeight = 24f;
					layout.preferredHeight = 24f;
				}
				Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
				if (text != null)
				{
					text.fontSize = Mathf.RoundToInt(11f * UiTextScale);
				}
			}
			return button;
		}

		private static Button GetOrCreateCombatPopupOnFootCountButton(Transform parent)
		{
			Button button = parent.Find(PopupOnFootCountButtonName)?.GetComponent<Button>();
			if (button == null)
			{
				button = CrewRelationshipHandlerPatch.CreateButton(parent, PopupOnFootCountButtonName, string.Empty, null);
				if (button == null)
				{
					return null;
				}
				RectTransform rect = ((Component)button).GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
				LayoutElement layout = ((Component)button).GetComponent<LayoutElement>();
				if (layout != null)
				{
					layout.minWidth = 132f;
					layout.preferredWidth = 132f;
					layout.flexibleWidth = 0f;
					layout.minHeight = 24f;
					layout.preferredHeight = 24f;
				}
				Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
				if (text != null)
				{
					text.fontSize = Mathf.RoundToInt(11f * UiTextScale);
				}
			}
			return button;
		}

		private static Button GetOrCreateCombatPopupOnFootFilterButton(Transform parent)
		{
			Button button = parent.Find(PopupOnFootFilterButtonName)?.GetComponent<Button>();
			if (button == null)
			{
				button = CrewRelationshipHandlerPatch.CreateButton(parent, PopupOnFootFilterButtonName, string.Empty, null);
				if (button == null)
				{
					return null;
				}
				RectTransform rect = ((Component)button).GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
				LayoutElement layout = ((Component)button).GetComponent<LayoutElement>();
				if (layout != null)
				{
					layout.minWidth = 104f;
					layout.preferredWidth = 104f;
					layout.flexibleWidth = 0f;
					layout.minHeight = 24f;
					layout.preferredHeight = 24f;
				}
				Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
				if (text != null)
				{
					text.fontSize = Mathf.RoundToInt(11f * UiTextScale);
				}
			}
			return button;
		}

		private static Button GetOrCreateCombatPopupDriveByTargetButton(Transform parent)
		{
			Button button = parent.Find(PopupDriveByTargetButtonName)?.GetComponent<Button>();
			if (button == null)
			{
				button = CrewRelationshipHandlerPatch.CreateButton(parent, PopupDriveByTargetButtonName, string.Empty, null);
				if (button == null)
				{
					return null;
				}
				RectTransform rect = ((Component)button).GetComponent<RectTransform>();
				rect.anchorMin = Vector2.zero;
				rect.anchorMax = Vector2.one;
				rect.offsetMin = Vector2.zero;
				rect.offsetMax = Vector2.zero;
				LayoutElement layout = ((Component)button).GetComponent<LayoutElement>();
				if (layout != null)
				{
					layout.minWidth = 186f;
					layout.preferredWidth = 186f;
					layout.flexibleWidth = 0f;
					layout.minHeight = 24f;
					layout.preferredHeight = 24f;
				}
				Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
				if (text != null)
				{
					text.fontSize = Mathf.RoundToInt(11f * UiTextScale);
				}
			}
			return button;
		}

		private static Text GetOrCreateCombatPopupVehicleInfoLabel(Transform parent)
		{
			Text label = parent.Find(PopupVehicleInfoLabelName)?.GetComponent<Text>();
			if (label != null)
			{
				return label;
			}
			GameObject gameObject = new GameObject(PopupVehicleInfoLabelName, typeof(RectTransform), typeof(Text));
			gameObject.transform.SetParent(parent, false);
			RectTransform rect = gameObject.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			label = gameObject.GetComponent<Text>();
			Button referenceButton = parent.parent?.Find(PopupRangedToggleAnchorName + "/" + PopupRangedToggleButtonName)?.GetComponent<Button>();
			Text referenceText = referenceButton != null ? referenceButton.GetComponentInChildren<Text>(includeInactive: true) : null;
			label.font = referenceText != null ? referenceText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
			label.fontSize = Mathf.RoundToInt(11f * UiTextScale);
			label.alignment = TextAnchor.MiddleLeft;
			label.horizontalOverflow = HorizontalWrapMode.Wrap;
			label.verticalOverflow = VerticalWrapMode.Truncate;
			label.color = referenceText != null ? referenceText.color : Color.white;
			return label;
		}

		private static Text GetOrCreateCombatPopupTargetVehicleInfoLabel(Transform parent)
		{
			Text label = parent.Find(PopupTargetVehicleInfoLabelName)?.GetComponent<Text>();
			if (label != null)
			{
				return label;
			}
			GameObject gameObject = new GameObject(PopupTargetVehicleInfoLabelName, typeof(RectTransform), typeof(Text));
			gameObject.transform.SetParent(parent, false);
			RectTransform rect = gameObject.GetComponent<RectTransform>();
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			label = gameObject.GetComponent<Text>();
			Button referenceButton = parent.parent?.Find(PopupRangedToggleAnchorName + "/" + PopupRangedToggleButtonName)?.GetComponent<Button>();
			Text referenceText = referenceButton != null ? referenceButton.GetComponentInChildren<Text>(includeInactive: true) : null;
			label.font = referenceText != null ? referenceText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");
			label.fontSize = Mathf.RoundToInt(11f * UiTextScale);
			label.alignment = TextAnchor.MiddleLeft;
			label.horizontalOverflow = HorizontalWrapMode.Wrap;
			label.verticalOverflow = VerticalWrapMode.Truncate;
			label.color = referenceText != null ? referenceText.color : Color.white;
			return label;
		}

		private static void UpdateCombatPopupRangedToggleVisual(CombatPopupPlanning popup, Button button)
		{
			if (button == null)
			{
				return;
			}
			bool driveByAvailable = IsDriveByModeAvailableForPopup(popup);
			GroupedCombatMode mode = GetPopupCombatMode(popup);
			GetPopupVehicleCrewDisplayInfo(popup, out int passengerCount, out int crewCount, out int slots);
			Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
			if (text != null)
			{
				string modeLabel = mode == GroupedCombatMode.DriveBy ? "Mode: Drive-By" : "Mode: On-Foot";
				text.text = modeLabel;
			}
			button.interactable = driveByAvailable;
			UiButtonVariant variant = !driveByAvailable
				? UiButtonVariant.Default
				: (mode == GroupedCombatMode.DriveBy ? UiButtonVariant.Success : UiButtonVariant.Accent);
			CrewRelationshipHandlerPatch.ApplyStandardButtonTheme(button, variant);
		}

		private static void UpdateCombatPopupVehicleInfoLabel(CombatPopupPlanning popup, Text label)
		{
			if (label == null)
			{
				return;
			}
			GetPopupVehicleCrewDisplayInfo(popup, out int passengerCount, out int crewCount, out int slots);
			label.text = $"Passengers: {passengerCount} | Crew {crewCount}/{slots}";
		}

		private static void UpdateCombatPopupTargetVehicleInfoLabel(CombatPopupPlanning popup, Text label)
		{
			if (label == null)
			{
				return;
			}
			GetPopupTargetVehicleCrewDisplayInfo(popup, out int passengerCount, out int crewCount, out int slots);
			string summary = BuildPopupTargetVehicleOccupantSummary(popup);
			label.text = string.IsNullOrEmpty(summary)
				? $"Target Passengers: {passengerCount} | Crew {crewCount}/{slots}"
				: $"Target Passengers: {passengerCount} | Crew {crewCount}/{slots}\n{summary}";
		}

		private static void UpdateCombatPopupOnFootCountVisual(CombatPopupPlanning popup, Button button)
		{
			if (button == null)
			{
				return;
			}
			int max = Math.Max(1, GetMaxOnFootPopupAttackers(popup));
			int count = Mathf.Clamp(GetPopupOnFootCount(popup), 1, max);
			Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
			if (text != null)
			{
				text.text = $"On-Foot Crew: {count} of {max}";
			}
			bool enabled = GetPopupCombatMode(popup) == GroupedCombatMode.OnFoot && max > 1;
			button.interactable = enabled;
			CrewRelationshipHandlerPatch.ApplyStandardButtonTheme(button, enabled ? UiButtonVariant.Accent : UiButtonVariant.Default);
		}

		private static void UpdateCombatPopupOnFootFilterVisual(CombatPopupPlanning popup, Button button)
		{
			if (button == null)
			{
				return;
			}
			OnFootWeaponFilter filter = GetPopupOnFootWeaponFilter(popup);
			Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
			if (text != null)
			{
				text.text = "On-Foot Weapons: " + FormatOnFootWeaponFilter(filter);
			}
			bool enabled = GetPopupCombatMode(popup) == GroupedCombatMode.OnFoot;
			button.interactable = enabled;
			CrewRelationshipHandlerPatch.ApplyStandardButtonTheme(button, enabled ? UiButtonVariant.Accent : UiButtonVariant.Default);
		}

		private static void UpdateCombatPopupDriveByTargetVisual(CombatPopupPlanning popup, Button button)
		{
			if (button == null)
			{
				return;
			}
			DriveByTargetMode mode = GetPopupDriveByTargetMode(popup);
			Text text = ((Component)button).GetComponentInChildren<Text>(includeInactive: true);
			if (text != null)
			{
				text.text = "Drive-By Target: " + FormatDriveByTargetMode(mode);
			}
			bool enabled = GetPopupCombatMode(popup) == GroupedCombatMode.DriveBy && HasMultipleDriveByTargets(popup);
			button.interactable = enabled;
			UiButtonVariant variant = !enabled
				? UiButtonVariant.Default
				: (mode == DriveByTargetMode.FocusOne ? UiButtonVariant.Success : UiButtonVariant.Accent);
			CrewRelationshipHandlerPatch.ApplyStandardButtonTheme(button, variant);
		}

		private static void OnCombatPopupRangedToggleClicked(CombatPopupPlanning popup)
		{
			if (!IsDriveByModeAvailableForPopup(popup))
			{
				return;
			}
			GroupedCombatMode nextMode = GetPopupCombatMode(popup) == GroupedCombatMode.DriveBy
				? GroupedCombatMode.OnFoot
				: GroupedCombatMode.DriveBy;
			SetPopupCombatMode(popup, nextMode);
			VerificationLog("VehicleGroupCombat", $"ui-toggle popup=CombatPopupPlanning mode={FormatCombatMode(nextMode)}");
			EnsureCombatPopupRangedToggle(popup);
			EnsureCombatPopupOnFootCountToggle(popup);
			EnsureCombatPopupOnFootFilterToggle(popup);
			EnsureCombatPopupDriveByTargetToggle(popup);
			EnsureCombatPopupVehicleInfoLabel(popup);
			EnsureCombatPopupTargetVehicleInfoLabel(popup);
			LogPopupVehicleCrewInfo(popup);
			RefreshGroupedPopupWeaponRows(popup);
		}

		private static void OnCombatPopupOnFootCountClicked(CombatPopupPlanning popup)
		{
			int max = Math.Max(1, GetMaxOnFootPopupAttackers(popup));
			if (max <= 1)
			{
				return;
			}
			int current = Mathf.Clamp(GetPopupOnFootCount(popup), 1, max);
			int next = current >= max ? 1 : current + 1;
			SetPopupOnFootCount(popup, next);
			VerificationLog("VehicleGroupCombat", $"onfoot-count popup=CombatPopupPlanning selected={next} max={max}");
			EnsureCombatPopupOnFootCountToggle(popup);
			EnsureCombatPopupOnFootFilterToggle(popup);
			EnsureCombatPopupVehicleInfoLabel(popup);
			EnsureCombatPopupTargetVehicleInfoLabel(popup);
		}

		private static void OnCombatPopupOnFootFilterClicked(CombatPopupPlanning popup)
		{
			OnFootWeaponFilter current = GetPopupOnFootWeaponFilter(popup);
			OnFootWeaponFilter next = current == OnFootWeaponFilter.Any
				? OnFootWeaponFilter.Melee
				: (current == OnFootWeaponFilter.Melee ? OnFootWeaponFilter.Ranged : OnFootWeaponFilter.Any);
			SetPopupOnFootWeaponFilter(popup, next);
			VerificationLog("VehicleGroupCombat", $"onfoot-filter popup=CombatPopupPlanning filter={FormatOnFootWeaponFilter(next)}");
			EnsureCombatPopupOnFootFilterToggle(popup);
			RefreshGroupedPopupWeaponRows(popup);
		}

		private static void OnCombatPopupDriveByTargetClicked(CombatPopupPlanning popup)
		{
			DriveByTargetMode current = GetPopupDriveByTargetMode(popup);
			DriveByTargetMode next = current == DriveByTargetMode.FocusOne
				? DriveByTargetMode.SpreadAll
				: DriveByTargetMode.FocusOne;
			SetPopupDriveByTargetMode(popup, next);
			VerificationLog("VehicleGroupCombat", $"driveby-target-mode popup=CombatPopupPlanning mode={FormatDriveByTargetMode(next)}");
			EnsureCombatPopupDriveByTargetToggle(popup);
			EnsureCombatPopupRangedToggle(popup);
			RefreshPopupVehicleLabels(popup);
		}

		private static bool IsGroupedHumanVehiclePopup(CombatPopupPlanning popup)
		{
			List<Entity> myCrew = PopupMyCrewField?.GetValue(popup) as List<Entity>;
			return myCrew != null && myCrew.Any(ShouldUseGroupedHumanVehicleCombat);
		}

		private static bool HasMultipleDriveByTargets(CombatPopupPlanning popup)
		{
			if (!TryGetPrimaryPopupTargetVehicleCrew(popup, out CrewAssignment crew))
			{
				return false;
			}
			return GetEligibleDefendersForVehicle(crew).Count > 1;
		}

		private static bool TryGetPrimaryPopupVehicleCrew(CombatPopupPlanning popup, out CrewAssignment crew)
		{
			crew = CrewAssignment.EMPTY;
			List<Entity> myCrew = PopupMyCrewField?.GetValue(popup) as List<Entity>;
			if (myCrew == null)
			{
				return false;
			}
			foreach (Entity peep in myCrew)
			{
				CrewAssignment candidate = TryFindCrewForPeep(peep);
				if (!candidate.IsValid || !candidate.IsInVehicle || !candidate.VehicleID.IsValid)
				{
					continue;
				}
				crew = candidate;
				return true;
			}
			return false;
		}

		private static void GetPopupVehicleCrewDisplayInfo(CombatPopupPlanning popup, out int passengerCount, out int crewCount, out int slots)
		{
			passengerCount = 0;
			crewCount = 0;
			slots = 1;
			if (!TryGetPrimaryPopupVehicleCrew(popup, out CrewAssignment crew))
			{
				return;
			}
			PlayerCrew playerCrew = crew.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
			if (playerCrew == null)
			{
				return;
			}
			passengerCount = MultiCrewVehicleHelper.GetPassengers(playerCrew, crew.VehicleID)
				.Count(item => item.IsValid && item.IsNotDead && playerCrew.IsOnBoard(item.peepId));
			crewCount = Math.Max(1, MultiCrewVehicleHelper.GetLiveVehicleCrewCount(playerCrew, crew.VehicleID));
			slots = Math.Max(1, MultiCrewVehicleHelper.GetVehicleCrewSlots(crew.VehicleID));
		}

		private static bool TryGetPrimaryPopupTargetEntity(CombatPopupPlanning popup, out Entity target)
		{
			target = null;
			List<Entity> myCrew = PopupMyCrewField?.GetValue(popup) as List<Entity>;
			if (myCrew == null)
			{
				return false;
			}
			foreach (Entity peep in myCrew)
			{
				if (peep == null)
				{
					continue;
				}
				target = PopupFindTargetForMethod?.Invoke(popup, new object[] { peep }) as Entity;
				if (target != null)
				{
					return true;
				}
			}
			target = null;
			return false;
		}

		private static bool TryGetPrimaryPopupTargetVehicleCrew(CombatPopupPlanning popup, out CrewAssignment crew)
		{
			crew = CrewAssignment.EMPTY;
			if (!TryGetPrimaryPopupTargetEntity(popup, out Entity target))
			{
				return false;
			}
			CrewAssignment candidate = TryFindCrewForPeep(target);
			if (!candidate.IsValid || !candidate.IsInVehicle || !candidate.VehicleID.IsValid)
			{
				return false;
			}
			crew = candidate;
			return true;
		}

		private static void GetPopupTargetVehicleCrewDisplayInfo(CombatPopupPlanning popup, out int passengerCount, out int crewCount, out int slots)
		{
			passengerCount = 0;
			crewCount = 0;
			slots = 1;
			if (!TryGetPrimaryPopupTargetVehicleCrew(popup, out CrewAssignment crew))
			{
				return;
			}
			PlayerCrew playerCrew = crew.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
			if (playerCrew == null)
			{
				return;
			}
			passengerCount = MultiCrewVehicleHelper.GetPassengers(playerCrew, crew.VehicleID)
				.Count(item => item.IsValid && item.IsNotDead && playerCrew.IsOnBoard(item.peepId));
			crewCount = Math.Max(1, MultiCrewVehicleHelper.GetLiveVehicleCrewCount(playerCrew, crew.VehicleID));
			slots = Math.Max(1, MultiCrewVehicleHelper.GetVehicleCrewSlots(crew.VehicleID));
		}

		private static string BuildPopupTargetVehicleOccupantSummary(CombatPopupPlanning popup)
		{
			if (!TryGetPrimaryPopupTargetVehicleCrew(popup, out CrewAssignment crew))
			{
				return string.Empty;
			}
			PlayerCrew playerCrew = crew.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
			if (playerCrew == null)
			{
				return string.Empty;
			}
			Entity selectedTarget = null;
			TryGetPrimaryPopupTargetEntity(popup, out selectedTarget);
			EntityID selectedTargetPeepId = selectedTarget?.Id ?? EntityID.INVALID;
			EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(playerCrew, crew.VehicleID);
			List<string> occupants = MultiCrewVehicleHelper.GetAllCrewInVehicle(playerCrew, crew.VehicleID)
				.Where(item => item.IsValid && item.IsNotDead && item.peepId.IsValid && playerCrew.IsOnBoard(item.peepId))
				.OrderBy(item => item.peepId == selectedTargetPeepId ? 0 : 1)
				.ThenBy(item => item.peepId == driverPeepId ? 0 : 1)
				.ThenBy(item => item.peepId.id)
				.Select(item =>
				{
					Entity peep = item.GetPeep();
					string name = peep?.data?.person?.FullName ?? ("Crew " + item.peepId.id.ToString(CultureInfo.InvariantCulture));
					if (item.peepId == selectedTargetPeepId)
					{
						name = ">" + name;
					}
					else if (item.peepId == driverPeepId)
					{
						name = "D:" + name;
					}
					return name;
				})
				.ToList();
			return occupants.Count == 0 ? string.Empty : "Targets: " + string.Join(" | ", occupants);
		}

		private static void LogPopupVehicleCrewInfo(CombatPopupPlanning popup)
		{
			GetPopupVehicleCrewDisplayInfo(popup, out int passengerCount, out int crewCount, out int slots);
			if (!TryGetPrimaryPopupVehicleCrew(popup, out CrewAssignment crew) || !crew.VehicleID.IsValid)
			{
				goto Target;
			}
			string key = $"{GetPopupCommitToken(popup)}:{FormatCombatMode(GetPopupCombatMode(popup))}:{crew.VehicleID.id}:{passengerCount}:{crewCount}:{slots}";
			if (PopupVehicleCrewInfoLogKeys.Add(key))
			{
				VerificationLog("VehicleGroupCombat", $"popup-vehicle-crew attackerVehicle={crew.VehicleID.id} passengers={passengerCount} crew={crewCount} slots={slots} mode={FormatCombatMode(GetPopupCombatMode(popup))}");
			}
Target:
			GetPopupTargetVehicleCrewDisplayInfo(popup, out int targetPassengerCount, out int targetCrewCount, out int targetSlots);
			if (!TryGetPrimaryPopupTargetVehicleCrew(popup, out CrewAssignment targetCrew) || !targetCrew.VehicleID.IsValid)
			{
				return;
			}
			string targetKey = $"{GetPopupCommitToken(popup)}:target:{FormatCombatMode(GetPopupCombatMode(popup))}:{targetCrew.VehicleID.id}:{targetPassengerCount}:{targetCrewCount}:{targetSlots}";
			if (PopupVehicleCrewInfoLogKeys.Add(targetKey))
			{
				VerificationLog("VehicleGroupCombat", $"popup-target-vehicle-crew targetVehicle={targetCrew.VehicleID.id} passengers={targetPassengerCount} crew={targetCrewCount} slots={targetSlots} mode={FormatCombatMode(GetPopupCombatMode(popup))}");
			}
		}

		private static string FormatOnFootWeaponFilter(OnFootWeaponFilter filter)
		{
			return filter == OnFootWeaponFilter.Melee
				? "Melee"
				: (filter == OnFootWeaponFilter.Ranged ? "Ranged" : "Any");
		}

		private static string FormatDriveByTargetMode(DriveByTargetMode mode)
		{
			return mode == DriveByTargetMode.SpreadAll ? "Spread All" : "Focus One";
		}

		private static string FormatDriveByTargetModeLogValue(DriveByTargetMode mode)
		{
			return mode == DriveByTargetMode.SpreadAll ? "spread-all" : "focus-one";
		}

		private static void RefreshGroupedPopupWeaponRows(CombatPopupPlanning popup)
		{
			GameObject popupRoot = popup?.GameObject;
			if (popupRoot == null)
			{
				return;
			}
			GameObject humanContents = popupRoot.GetChild(PopupHumanContentsPath);
			if (humanContents == null)
			{
				return;
			}
			foreach (Transform child in humanContents.transform)
			{
				GameObject card = child.gameObject;
				CombatCardContext ctx = card.GetComponent<CombatCardContext>();
				if (ctx == null || ctx.peep == null || !ShouldUseGroupedHumanVehicleCombat(ctx.peep))
				{
					continue;
				}
				ApplyGroupedPopupWeaponFilter(popup, card, ctx);
			}
			global::Game.Game.serv.mouseovers.Refresh(MouseoverType.CombatMethodButton);
		}

		private static void ApplyGroupedPopupWeaponFilter(CombatPopupPlanning popup, GameObject card, CombatCardContext ctx)
		{
			if (card == null || ctx == null || ctx.peep == null)
			{
				return;
			}
			TMP_Dropdown dropdown = card.GetDropdown(PopupMethodDropdownPath);
			if (dropdown == null)
			{
				return;
			}
			WeaponConfig currentWeapon = dropdown.GetCurrentOptionsItem()?.data as WeaponConfig;
			bool wasInteractable = dropdown.interactable;
			List<WeaponConfig> filteredWeapons = GetFilteredGroupedPopupWeapons(popup, ctx);
			if (filteredWeapons.Count == 0)
			{
				return;
			}
			dropdown.options.Clear();
			foreach (WeaponConfig weapon in filteredWeapons)
			{
				Resource resource = weapon.FindResource();
				dropdown.AddOptionsItem(resource.GetIconAndName(), weapon);
			}
			int selectedIndex = 0;
			string storedWeaponId = TryGetStoredPopupWeaponId(popup, ctx.peep.Id);
			bool preferBestNonFists = currentWeapon == null && string.IsNullOrEmpty(storedWeaponId);
			if (!string.IsNullOrEmpty(storedWeaponId))
			{
				int storedIndex = filteredWeapons.FindIndex(weapon => string.Equals(GetWeaponId(weapon), storedWeaponId, StringComparison.OrdinalIgnoreCase));
				if (storedIndex >= 0)
				{
					selectedIndex = storedIndex;
					preferBestNonFists = false;
				}
			}
			else if (currentWeapon != null)
			{
				int existingIndex = filteredWeapons.FindIndex(weapon => SameWeapon(weapon, currentWeapon));
				if (existingIndex >= 0)
				{
					selectedIndex = existingIndex;
				}
			}
			if (preferBestNonFists)
			{
				int preferredIndex = filteredWeapons.FindIndex(weapon => !IsUnlimitedFallbackWeapon(weapon));
				if (preferredIndex >= 0)
				{
					selectedIndex = preferredIndex;
				}
			}
			dropdown.SetValueWithoutNotify(Mathf.Clamp(selectedIndex, 0, filteredWeapons.Count - 1));
			dropdown.RefreshShownValue();
			dropdown.interactable = wasInteractable && dropdown.options.Count > 1;
			UpdateCombatCardWeaponDisplay(card, ctx, filteredWeapons[Mathf.Clamp(selectedIndex, 0, filteredWeapons.Count - 1)]);
		}

		private static List<WeaponConfig> GetFilteredGroupedPopupWeapons(CombatPopupPlanning popup, CombatCardContext ctx)
		{
			var filteredWeapons = new List<WeaponConfig>();
			var seenWeaponIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			CrewAssignment crew = TryFindCrewForPeep(ctx?.peep);
			GroupedCombatMode mode = GetEffectiveCombatModeForCrew(popup, crew, requireActionPoints: true);
			OnFootWeaponFilter onFootFilter = GetPopupOnFootWeaponFilter(popup);
			IEnumerable<WeaponConfig> sourceWeapons = mode == GroupedCombatMode.DriveBy
				? GetAvailableDriveByWeaponsForVehicle(crew)
				: GetAvailableOnFootPopupWeapons(crew, ctx);
			foreach (WeaponConfig weapon in sourceWeapons)
			{
				if (weapon == null || !IsAllowedGroupedPlayerWeapon(weapon, mode) || !MatchesPopupOnFootWeaponFilter(weapon, mode, onFootFilter))
				{
					continue;
				}
				string weaponId = GetWeaponId(weapon);
				if (!seenWeaponIds.Add(weaponId))
				{
					continue;
				}
				filteredWeapons.Add(weapon);
			}
			if (filteredWeapons.Count == 0)
			{
				WeaponConfig fists = ctx.allweapons?.FirstOrDefault(IsUnlimitedFallbackWeapon) ?? ctx.bestWeapon;
				if (fists != null)
				{
					filteredWeapons.Add(fists);
				}
			}
			return filteredWeapons;
		}

		private static string TryGetStoredPopupWeaponId(CombatPopupPlanning popup, EntityID peepId)
		{
			int popupId = GetPopupId(popup);
			if (popupId == 0 || !peepId.IsValid)
			{
				return null;
			}
			if (PopupSelectedWeaponIdsById.TryGetValue(popupId, out Dictionary<ulong, string> weaponSelections)
				&& weaponSelections != null
				&& weaponSelections.TryGetValue(peepId.id, out string weaponId)
				&& !string.IsNullOrEmpty(weaponId))
			{
				return weaponId;
			}
			return null;
		}

		private static bool MatchesPopupOnFootWeaponFilter(WeaponConfig weapon, GroupedCombatMode mode, OnFootWeaponFilter filter)
		{
			if (mode != GroupedCombatMode.OnFoot || filter == OnFootWeaponFilter.Any || weapon == null)
			{
				return true;
			}
			if (IsUnlimitedFallbackWeapon(weapon))
			{
				return filter != OnFootWeaponFilter.Ranged;
			}
			bool isRanged = IsRangedWeapon(weapon);
			return filter == OnFootWeaponFilter.Ranged ? isRanged : !isRanged;
		}

		private static IEnumerable<WeaponConfig> GetAvailableOnFootPopupWeapons(CrewAssignment crew, CombatCardContext ctx)
		{
			var result = new List<WeaponConfig>();
			var seenWeaponIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			void AddWeapon(WeaponConfig weapon)
			{
				if (weapon == null || !IsAllowedGroupedPlayerWeapon(weapon, GroupedCombatMode.OnFoot))
				{
					return;
				}
				string weaponId = GetWeaponId(weapon);
				if (string.IsNullOrEmpty(weaponId) || !seenWeaponIds.Add(weaponId))
				{
					return;
				}
				result.Add(weapon);
			}

			if (global::Game.Game.ctx?.simman?.combat != null)
			{
				AddWeapon(global::Game.Game.ctx.simman.combat.GetFistsWeapon());
			}

			foreach (WeaponConfig weapon in ctx?.allweapons ?? Enumerable.Empty<WeaponConfig>())
			{
				AddWeapon(weapon);
			}

			if (global::Game.Game.ctx?.simman?.combat != null)
			{
				foreach (CrewAssignment attacker in GetEligibleAttackersForVehicle(crew, requireActionPoints: true, GroupedCombatMode.OnFoot))
				{
					AddWeapon(global::Game.Game.ctx.simman.combat.FindBestWeapon(attacker));
				}
				foreach (WeaponPoolEntry entry in BuildWeaponPool(global::Game.Game.ctx.simman.combat, crew.GetVehicle(), applyPlayerWeaponFilters: true))
				{
					AddWeapon(entry.Weapon);
				}
			}

			return result
				.OrderBy(weapon => IsUnlimitedFallbackWeapon(weapon) ? 1 : 0)
				.ThenByDescending(weapon => weapon?.high ?? Fixnum.ZERO)
				.ThenBy(weapon => GetWeaponId(weapon), StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static void UpdateCombatCardWeaponDisplay(GameObject card, CombatCardContext ctx, WeaponConfig weapon)
		{
			if (card == null || ctx == null || weapon == null)
			{
				return;
			}
			card.SetText("Weapon", ctx.MakeWeapon(weapon));
			card.SetText("Name", ctx.MakeCrewNameDesc());
			card.SetText("Health", ctx.MakeHealth());
		}

		private static bool SameWeapon(WeaponConfig a, WeaponConfig b)
		{
			if (ReferenceEquals(a, b))
			{
				return true;
			}
			return string.Equals(GetWeaponId(a), GetWeaponId(b), StringComparison.OrdinalIgnoreCase);
		}

		private static bool ShouldUseGroupedHumanVehicleCombat(Entity attacker)
		{
			CrewAssignment attackerCrew = TryFindCrewForPeep(attacker);
			return attackerCrew.IsValid
				&& attacker?.data?.agent?.pid.IsHumanPlayer == true
				&& HasMultipleEligibleVehicleOccupants(attackerCrew, requireActionPoints: true);
		}

		private static bool HasMultipleEligibleVehicleOccupants(CrewAssignment crew, bool requireActionPoints)
		{
			return GetEligibleAttackersForVehicle(crew, requireActionPoints, GroupedCombatMode.OnFoot).Count > 1;
		}

		private static string FormatCombatMode(GroupedCombatMode mode)
		{
			return mode == GroupedCombatMode.DriveBy ? "driveby" : "onfoot";
		}

		private static GroupedCombatMode GetEffectiveCombatModeForCrew(CombatPopupPlanning popup, CrewAssignment crew, bool requireActionPoints)
		{
			GroupedCombatMode requestedMode = GetPopupCombatMode(popup);
			if (requestedMode == GroupedCombatMode.DriveBy && HasEligibleDriveByAttackers(crew, requireActionPoints))
			{
				return GroupedCombatMode.DriveBy;
			}
			return GroupedCombatMode.OnFoot;
		}

		private static GroupedCombatMode GetDefaultExecutionMode(CrewAssignment crew, WeaponConfig selectedAttackWeapon, bool requireActionPoints)
		{
			if (crew.IsValid
				&& crew.IsInVehicle
				&& IsRangedWeapon(selectedAttackWeapon)
				&& HasEligibleDriveByAttackers(crew, requireActionPoints))
			{
				return GroupedCombatMode.DriveBy;
			}
			return GroupedCombatMode.OnFoot;
		}

		private static int GetRequestedOnFootAttackerCount(CombatPopupPlanning popup, CrewAssignment crew, bool requireActionPoints)
		{
			int max = Math.Max(1, GetEligibleAttackersForVehicle(crew, requireActionPoints, GroupedCombatMode.OnFoot).Count);
			return Mathf.Clamp(GetPopupOnFootCount(popup), 1, max);
		}

		private static CrewAssignment TryFindCrewForPeep(Entity peep)
		{
			try
			{
				if (peep == null)
					return CrewAssignment.EMPTY;
				PlayerInfo player = peep.data?.agent?.pid.FindPlayer();
				PlayerCrew crew = player?.crew;
				if (crew == null)
				{
					return CrewAssignment.EMPTY;
				}
				CrewAssignment assignment = crew.GetCrewForPeep(peep.Id);
				if (!assignment.IsValid)
				{
					assignment = crew.GetCrewForTarget(peep.Id);
				}
				if (!assignment.IsValid)
				{
					assignment = crew.GetLiving()
						.FirstOrDefault(item => item.IsValid && item.peepId.IsValid && item.peepId == peep.Id);
				}
				if (!assignment.IsValid)
				{
					assignment = crew.GetLiving()
						.FirstOrDefault(item => item.IsValid && item.GetPeep()?.Id == peep.Id);
				}
				if (!assignment.IsValid && player.IsCopOrFed)
				{
					CrewAssignment founder = crew.GetCrewForPlayerPeep();
					if (founder.IsValid)
					{
						assignment = founder;
					}
					if (!assignment.IsValid)
					{
						assignment = crew.GetLiving().FirstOrDefault(item => item.IsValid);
					}
					if (assignment.IsValid)
					{
						VerificationLog("VehicleGroupCombat", $"cop-popup-crew-fallback target={peep.Id.id} targetPid={player.PID.id} resolvedPeep={assignment.peepId.id} vehicle={assignment.VehicleID.id}");
					}
				}
				return assignment;
			}
			catch
			{
				return CrewAssignment.EMPTY;
			}
		}

		private static int GetLivingGangCrewCount(CrewAssignment crew)
		{
			try
			{
				PlayerCrew playerCrew = crew.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
				return playerCrew?.GetLiving().Count(item => item.IsValid && item.IsNotDead) ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		private static List<CrewAssignment> GetVehicleCombatOccupants(CrewAssignment crew)
		{
			if (!crew.IsValid)
				return new List<CrewAssignment>();
			if (!crew.IsInVehicle)
				return new List<CrewAssignment> { crew };
			PlayerInfo player = crew.GetPeep()?.data?.agent?.pid.FindPlayer();
			PlayerCrew playerCrew = player?.crew;
			if (playerCrew == null)
				return new List<CrewAssignment> { crew };
			return MultiCrewVehicleHelper.GetAllCrewInVehicle(playerCrew, crew.VehicleID)
				.Where(item => item.IsValid && item.IsNotDead && item.GetPeep() != null)
				.ToList();
		}

		private static List<CrewAssignment> GetEligibleAttackersForVehicle(CrewAssignment crew, bool requireActionPoints, GroupedCombatMode mode = GroupedCombatMode.OnFoot)
		{
			IEnumerable<CrewAssignment> occupants = GetVehicleCombatOccupants(crew)
				.Where(item => IsEligibleAttacker(item, requireActionPoints));
			EntityID driverPeepId = GetDriverPeepId(crew);
			if (mode == GroupedCombatMode.DriveBy)
			{
				occupants = occupants.Where(item => item.peepId != driverPeepId);
			}
			return occupants
				.OrderBy(item => item.peepId == driverPeepId ? 0 : 1)
				.ThenBy(item => item.peepId.id)
				.ToList();
		}

		private static List<CrewAssignment> GetEligibleDefendersForVehicle(CrewAssignment crew)
		{
			return GetVehicleCombatOccupants(crew)
				.Where(item => item.IsValid && item.IsNotDead && item.GetPeep()?.components?.agent?.HasHealthPointsLeft == true)
				.ToList();
		}

		private static bool IsEligibleAttacker(CrewAssignment crew, bool requireActionPoints)
		{
			Entity peep = crew.GetPeep();
			if (!crew.IsValid || peep == null || !peep.components.agent.HasHealthPointsLeft)
				return false;
			if (!CombatCardContext.GetCrewHealthInfo(peep).category.canwork)
				return false;
			if (!requireActionPoints)
				return true;
			return global::Game.Game.ctx.simman.combat.CanPayAttackCost(peep);
		}

		private static EntityID GetDriverPeepId(CrewAssignment crew)
		{
			if (!crew.IsValid || !crew.IsInVehicle)
			{
				return EntityID.INVALID;
			}
			PlayerCrew playerCrew = crew.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
			return playerCrew == null ? EntityID.INVALID : MultiCrewVehicleHelper.GetDriverPeepId(playerCrew, crew.VehicleID);
		}

		private static bool HasEligibleDriveByAttackers(CrewAssignment crew, bool requireActionPoints)
		{
			return HasEligibleDriveByAttackers(crew, requireActionPoints, applyPlayerWeaponFilters: true);
		}

		private static bool HasEligibleDriveByAttackers(CrewAssignment crew, bool requireActionPoints, bool applyPlayerWeaponFilters)
		{
			return CountEligibleDriveByShooters(global::Game.Game.ctx?.simman?.combat, crew, requireActionPoints, applyPlayerWeaponFilters) > 0;
		}

		private static int CountEligibleDriveByShooters(CombatManager combatManager, CrewAssignment crew, bool requireActionPoints, bool applyPlayerWeaponFilters)
		{
			if (!crew.IsValid || !crew.IsInVehicle || global::Game.Game.ctx?.simman?.combat == null)
			{
				return 0;
			}
			List<CrewAssignment> candidates = GetEligibleAttackersForVehicle(crew, requireActionPoints, GroupedCombatMode.DriveBy);
			if (candidates.Count == 0)
			{
				return 0;
			}
			int weaponSlots = BuildWeaponPool(combatManager ?? global::Game.Game.ctx.simman.combat, crew.GetVehicle(), applyPlayerWeaponFilters)
				.Count(entry => entry.Remaining > 0 && IsAllowedWeaponForCombatMode(entry.Weapon, GroupedCombatMode.DriveBy));
			return Math.Min(candidates.Count, weaponSlots);
		}

		private static List<WeaponConfig> GetAvailableDriveByWeaponsForVehicle(CrewAssignment crew)
		{
			if (!crew.IsValid || global::Game.Game.ctx?.simman?.combat == null)
			{
				return new List<WeaponConfig>();
			}
			return BuildWeaponPool(global::Game.Game.ctx.simman.combat, crew.GetVehicle(), applyPlayerWeaponFilters: true)
				.Where(entry => entry.Remaining > 0 && IsAllowedGroupedPlayerWeapon(entry.Weapon, GroupedCombatMode.DriveBy))
				.Select(entry => entry.Weapon)
				.Where(weapon => weapon != null)
				.GroupBy(GetWeaponId, StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.OrderByDescending(weapon => weapon.high)
				.ThenBy(weapon => GetWeaponId(weapon), StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static List<CrewAssignment> SelectDriveByAttackers(CombatManager combatManager, CrewAssignment rootAttacker, bool requireActionPoints, WeaponConfig selectedAttackWeapon, bool applyPlayerWeaponFilters, out Dictionary<ulong, WeaponConfig> assignedWeapons, out Dictionary<ulong, string> assignmentSources)
		{
			assignedWeapons = new Dictionary<ulong, WeaponConfig>();
			assignmentSources = new Dictionary<ulong, string>();
			List<CrewAssignment> candidates = GetEligibleAttackersForVehicle(rootAttacker, requireActionPoints, GroupedCombatMode.DriveBy)
				.OrderBy(item => item.peepId.id)
				.ToList();
			if (candidates.Count == 0)
			{
				return new List<CrewAssignment>();
			}

			List<WeaponPoolEntry> rangedPool = BuildWeaponPool(combatManager, rootAttacker.GetVehicle(), applyPlayerWeaponFilters)
				.Where(entry => entry.Remaining > 0 && IsAllowedWeaponForCombatMode(entry.Weapon, GroupedCombatMode.DriveBy))
				.ToList();
			if (rangedPool.Count == 0)
			{
				return new List<CrewAssignment>();
			}

			var attackers = new List<CrewAssignment>();
			int maxShooters = Math.Min(3, candidates.Count);
			int nextCandidateIndex = 0;
			if (selectedAttackWeapon != null
				&& !IsUnlimitedFallbackWeapon(selectedAttackWeapon)
				&& IsAllowedWeaponForCombatMode(selectedAttackWeapon, GroupedCombatMode.DriveBy)
				&& TryReserveSpecificWeapon(rangedPool, selectedAttackWeapon))
			{
				CrewAssignment selectedShooter = candidates[nextCandidateIndex++];
				attackers.Add(selectedShooter);
				assignedWeapons[selectedShooter.peepId.id] = selectedAttackWeapon;
				assignmentSources[selectedShooter.peepId.id] = "selected-vehicle";
			}

			while (attackers.Count < maxShooters && nextCandidateIndex < candidates.Count)
			{
				WeaponConfig weapon = TakeBestAvailableWeapon(rangedPool);
				if (weapon == null || !IsAllowedWeaponForCombatMode(weapon, GroupedCombatMode.DriveBy))
				{
					break;
				}
				CrewAssignment shooter = candidates[nextCandidateIndex++];
				attackers.Add(shooter);
				assignedWeapons[shooter.peepId.id] = weapon;
				assignmentSources[shooter.peepId.id] = "vehicle-best";
			}

			return attackers;
		}

		private static void ExecuteVehicleGroupCombat(CombatManager combatManager, CrewAssignment rootAttacker, CrewAssignment rootTarget, WeaponConfig selectedAttackWeapon, GroupedCombatMode combatMode, bool consumeHumanAttackCost, List<CombatResults> results, string observerSource, int? requestedOnFootCount = null, DriveByTargetMode driveByTargetMode = DriveByTargetMode.FocusOne)
		{
			if (!rootAttacker.IsValid || !rootTarget.IsValid)
				return;

			Entity attackerPeep = rootAttacker.GetPeep();
			if (attackerPeep == null || rootTarget.GetPeep() == null)
				return;
			BeginGroupedCombatTransaction(observerSource, rootAttacker, rootTarget);
			try
			{
				bool driverExcluded = false;
				Dictionary<ulong, WeaponConfig> assignedWeapons = new Dictionary<ulong, WeaponConfig>();
				Dictionary<ulong, string> assignmentSources = new Dictionary<ulong, string>();
				List<CrewAssignment> attackers = new List<CrewAssignment>();
				bool isHumanAttacker = rootAttacker.GetPeep()?.data?.agent?.pid.IsHumanPlayer == true;
				if (isHumanAttacker)
				{
					if (combatMode == GroupedCombatMode.DriveBy)
					{
						attackers = SelectDriveByAttackers(combatManager, rootAttacker, consumeHumanAttackCost, selectedAttackWeapon, applyPlayerWeaponFilters: true, out assignedWeapons, out assignmentSources);
						driverExcluded = rootAttacker.IsInVehicle;
						if (attackers.Count == 0)
						{
							combatMode = GroupedCombatMode.OnFoot;
						}
					}
					if (combatMode == GroupedCombatMode.OnFoot)
					{
						attackers = GetEligibleAttackersForVehicle(rootAttacker, requireActionPoints: consumeHumanAttackCost, GroupedCombatMode.OnFoot);
						if (requestedOnFootCount.HasValue)
						{
							attackers = attackers.Take(Mathf.Clamp(requestedOnFootCount.Value, 1, attackers.Count)).ToList();
						}
						assignedWeapons = BuildRoundWeaponAssignments(combatManager, rootAttacker, attackers, selectedAttackWeapon, combatMode, out assignmentSources);
					}
				}
				else
				{
					if (combatMode == GroupedCombatMode.DriveBy && HasEligibleDriveByAttackers(rootAttacker, requireActionPoints: false, applyPlayerWeaponFilters: false))
					{
						attackers = SelectDriveByAttackers(combatManager, rootAttacker, requireActionPoints: false, selectedAttackWeapon: null, applyPlayerWeaponFilters: false, out assignedWeapons, out assignmentSources);
						driverExcluded = rootAttacker.IsInVehicle;
						if (attackers.Count == 0)
						{
							combatMode = GroupedCombatMode.OnFoot;
						}
					}
					if (combatMode == GroupedCombatMode.OnFoot)
					{
						attackers = GetEligibleAttackersForVehicle(rootAttacker, requireActionPoints: false, GroupedCombatMode.OnFoot);
						if (GetLivingGangCrewCount(rootAttacker) <= 2 || attackers.Count <= 1)
						{
							attackers = new List<CrewAssignment> { rootAttacker };
						}
						assignedWeapons = BuildRoundWeaponAssignments(combatManager, rootAttacker, attackers, selectedAttackWeapon, combatMode, out assignmentSources);
					}
				}
				if (attackers.Count == 0)
				{
					return;
				}
				bool logCombatDetails = ShouldLogVehicleCombatDetails(observerSource, rootAttacker, rootTarget);
				if (logCombatDetails)
				{
					LogWeaponAssignments(observerSource, attackerPeep, rootAttacker, attackers, assignedWeapons, assignmentSources, selectedAttackWeapon, combatMode);
					if (IsAiVehicleGroupCombatSource(observerSource))
					{
						LogAiCombatRoundSummary(observerSource, rootAttacker, rootTarget, attackers, assignedWeapons, combatMode);
					}
				}
				List<CrewAssignment> driveByTargetRotation = combatMode == GroupedCombatMode.DriveBy
					? BuildDriveByTargetRotation(rootTarget)
					: null;
				Dictionary<ulong, float> driveBySpreadWeights = combatMode == GroupedCombatMode.DriveBy && driveByTargetMode == DriveByTargetMode.SpreadAll
					? BuildDriveBySpreadWeights(driveByTargetRotation)
					: null;
				Dictionary<ulong, ulong> targetAssignments = new Dictionary<ulong, ulong>();
				if (logCombatDetails)
				{
					VerificationLog("VehicleGroupCombat", $"execute source={observerSource} mode={FormatCombatMode(combatMode)} attackerVehicle={rootAttacker.VehicleID.id} targetVehicle={rootTarget.VehicleID.id} attackers={attackers.Count} consumeHumanAttackCost={consumeHumanAttackCost}");
				}
				SetActiveGroupedCombatAttackerSnapshot(_groupedCombatTransactionSource, attackers);
				if (combatMode == GroupedCombatMode.DriveBy && logCombatDetails)
				{
					VerificationLog("VehicleGroupCombat", $"driveby-attackers source={observerSource} attackerVehicle={rootAttacker.VehicleID.id} targetVehicle={rootTarget.VehicleID.id} driverExcluded={driverExcluded} occupants={attackers.Count} targetMode={FormatDriveByTargetModeLogValue(driveByTargetMode)} peeps={string.Join(",", attackers.Where(item => item.peepId.IsValid).Select(item => item.peepId.id.ToString(CultureInfo.InvariantCulture)))}");
					if (IsAiVehicleGroupCombatSource(observerSource))
					{
						VerificationLog("VehicleGroupCombat.AI", $"driveby source={observerSource} attackerVehicle={rootAttacker.VehicleID.id} targetVehicle={rootTarget.VehicleID.id} driverExcluded={driverExcluded} occupants={attackers.Count} targetMode={FormatDriveByTargetModeLogValue(driveByTargetMode)} peeps={string.Join(",", attackers.Where(item => item.peepId.IsValid).Select(item => item.peepId.id.ToString(CultureInfo.InvariantCulture)))}");
					}
				}

				for (int attackerIndex = 0; attackerIndex < attackers.Count; attackerIndex++)
				{
					CrewAssignment attacker = attackers[attackerIndex];
					Entity currentAttackerPeep = attacker.GetPeep();
					if (currentAttackerPeep == null || !currentAttackerPeep.components.agent.HasHealthPointsLeft)
						continue;

					if (consumeHumanAttackCost)
						InvokeAttackCost(combatManager, currentAttackerPeep);

					WeaponConfig attackerWeapon = ResolveAssignedWeapon(assignedWeapons, attacker.peepId, combatManager);
					if (attackerWeapon == null)
						continue;

					CrewAssignment currentTargetCrew = rootTarget;
					if (combatMode == GroupedCombatMode.DriveBy && ShouldUseDriveByTargetReassignment(rootAttacker, attackerWeapon, driveByTargetRotation))
					{
						currentTargetCrew = ChooseDriveByTargetForAttacker(rootTarget, driveByTargetRotation, attackerIndex, driveByTargetMode, driveBySpreadWeights) ?? currentTargetCrew;
					}
					Entity currentTargetPeep = currentTargetCrew.GetPeep();
					if (!currentTargetCrew.IsValid || currentTargetPeep == null || !currentTargetPeep.components.agent.HasHealthPointsLeft)
					{
						break;
					}
					if (currentTargetCrew.IsValid && attacker.peepId.IsValid && currentTargetCrew.peepId.IsValid)
					{
						targetAssignments[attacker.peepId.id] = currentTargetCrew.peepId.id;
					}
					WeaponConfig defenderWeapon = combatManager.FindBestWeapon(currentTargetCrew);
					if (defenderWeapon == null)
						defenderWeapon = attackerWeapon;

					CombatExchangeDebugInfo debugInfo = new CombatExchangeDebugInfo();
					CombatResults result = ResolveVehicleGroupExchange(combatManager, attacker, currentTargetCrew, attackerWeapon, defenderWeapon, combatMode, debugInfo, observerSource, driveByTargetMode);
					if (result == null)
						continue;
					if (logCombatDetails)
					{
						LogVehicleDamageSplit(observerSource, currentTargetCrew, debugInfo, combatMode);
						if (IsAiVehicleGroupCombatSource(observerSource))
						{
							LogAiCombatResult(observerSource, result);
						}
					}

					results.Add(result);
					DispatchCombatObservers(result, observerSource);
				}
				if (logCombatDetails)
				{
					if (combatMode == GroupedCombatMode.DriveBy)
					{
						LogDriveByTargetAssignments(observerSource, rootAttacker, rootTarget, targetAssignments, assignedWeapons, driveByTargetMode);
					}
					else
					{
						LogOnFootTargetAssignments(observerSource, rootAttacker, rootTarget, targetAssignments, assignedWeapons);
					}
				}
			}
			finally
			{
				EndGroupedCombatTransaction();
			}
		}

		private static bool ShouldUseDriveByTargetReassignment(CrewAssignment rootAttacker, WeaponConfig attackerWeapon, List<CrewAssignment> driveByTargetRotation)
		{
			return rootAttacker.IsInVehicle
				&& IsRangedWeapon(attackerWeapon)
				&& driveByTargetRotation != null
				&& driveByTargetRotation.Count > 1;
		}

		private static List<CrewAssignment> BuildDriveByTargetRotation(CrewAssignment rootTarget)
		{
			List<CrewAssignment> defenders = GetEligibleDefendersForVehicle(rootTarget)
				.Where(item => item.IsValid && item.peepId.IsValid)
				.OrderBy(item => item.peepId == rootTarget.peepId ? 0 : 1)
				.ThenBy(item => item.peepId.id)
				.ToList();
			if (defenders.Count == 0 && rootTarget.IsValid)
			{
				defenders.Add(rootTarget);
			}
			return defenders;
		}

		private static Dictionary<ulong, float> BuildDriveBySpreadWeights(List<CrewAssignment> driveByTargetRotation)
		{
			var weights = new Dictionary<ulong, float>();
			foreach (CrewAssignment target in driveByTargetRotation ?? Enumerable.Empty<CrewAssignment>())
			{
				if (!target.peepId.IsValid)
				{
					continue;
				}
				weights[target.peepId.id] = 0.8f + (float)SharedRng.NextDouble();
			}
			return weights;
		}

		private static CrewAssignment? ChooseDriveByTargetForAttacker(CrewAssignment rootTarget, List<CrewAssignment> driveByTargetRotation, int attackerIndex, DriveByTargetMode driveByTargetMode, Dictionary<ulong, float> spreadWeights)
		{
			if (driveByTargetRotation == null || driveByTargetRotation.Count == 0)
			{
				return null;
			}
			CrewAssignment focusedTarget = FindFirstLivingTarget(driveByTargetRotation, rootTarget.peepId) ?? driveByTargetRotation[0];
			if (driveByTargetMode == DriveByTargetMode.SpreadAll)
			{
				return ChooseWeightedLivingDriveByTarget(driveByTargetRotation, spreadWeights, focusedTarget.peepId) ?? focusedTarget;
			}
			if (attackerIndex <= 0)
			{
				return focusedTarget;
			}
			List<CrewAssignment> spillTargets = driveByTargetRotation
				.Where(item => item.IsValid && item.peepId.IsValid && item.peepId != focusedTarget.peepId)
				.ToList();
			if (spillTargets.Count == 0)
			{
				return focusedTarget;
			}
			for (int remaining = spillTargets.Count; remaining > 0; remaining--)
			{
				int startIndex = SharedRng.Next(spillTargets.Count);
				CrewAssignment candidate = spillTargets[startIndex];
				Entity peep = candidate.GetPeep();
				if (candidate.IsValid && candidate.peepId.IsValid && peep != null && peep.components.agent.HasHealthPointsLeft)
				{
					return candidate;
				}
				spillTargets.RemoveAt(startIndex);
			}
			return FindFirstLivingTarget(driveByTargetRotation, EntityID.INVALID) ?? focusedTarget;
		}

		private static CrewAssignment? ChooseWeightedLivingDriveByTarget(List<CrewAssignment> driveByTargetRotation, Dictionary<ulong, float> spreadWeights, EntityID preferredPeepId)
		{
			List<CrewAssignment> livingTargets = driveByTargetRotation
				.Where(item => item.IsValid && item.peepId.IsValid && item.GetPeep()?.components?.agent?.HasHealthPointsLeft == true)
				.ToList();
			if (livingTargets.Count == 0)
			{
				return null;
			}
			float totalWeight = 0f;
			foreach (CrewAssignment livingTarget in livingTargets)
			{
				float weight = 1f;
				if (spreadWeights != null && spreadWeights.TryGetValue(livingTarget.peepId.id, out float storedWeight) && storedWeight > 0f)
				{
					weight = storedWeight;
				}
				if (preferredPeepId.IsValid && livingTarget.peepId == preferredPeepId)
				{
					weight += 0.25f;
				}
				totalWeight += weight;
			}
			if (totalWeight <= 0f)
			{
				return livingTargets[SharedRng.Next(livingTargets.Count)];
			}
			float roll = (float)SharedRng.NextDouble() * totalWeight;
			foreach (CrewAssignment livingTarget in livingTargets)
			{
				float weight = 1f;
				if (spreadWeights != null && spreadWeights.TryGetValue(livingTarget.peepId.id, out float storedWeight) && storedWeight > 0f)
				{
					weight = storedWeight;
				}
				if (preferredPeepId.IsValid && livingTarget.peepId == preferredPeepId)
				{
					weight += 0.25f;
				}
				roll -= weight;
				if (roll <= 0f)
				{
					return livingTarget;
				}
			}
			return livingTargets[livingTargets.Count - 1];
		}

		private static CrewAssignment? FindFirstLivingTarget(List<CrewAssignment> driveByTargetRotation, EntityID preferredPeepId)
		{
			if (driveByTargetRotation == null || driveByTargetRotation.Count == 0)
			{
				return null;
			}
			if (preferredPeepId.IsValid)
			{
				CrewAssignment preferred = driveByTargetRotation.FirstOrDefault(item => item.peepId == preferredPeepId);
				Entity preferredPeep = preferred.GetPeep();
				if (preferred.IsValid && preferred.peepId.IsValid && preferredPeep != null && preferredPeep.components.agent.HasHealthPointsLeft)
				{
					return preferred;
				}
			}
			foreach (CrewAssignment candidate in driveByTargetRotation)
			{
				Entity peep = candidate.GetPeep();
				if (candidate.IsValid && candidate.peepId.IsValid && peep != null && peep.components.agent.HasHealthPointsLeft)
				{
					return candidate;
				}
			}
			return null;
		}

		private static void LogDriveByTargetAssignments(string observerSource, CrewAssignment rootAttacker, CrewAssignment rootTarget, Dictionary<ulong, ulong> driveByAssignments, Dictionary<ulong, WeaponConfig> assignedWeapons, DriveByTargetMode driveByTargetMode)
		{
			if (driveByAssignments == null || driveByAssignments.Count == 0)
			{
				return;
			}
			string assignments = string.Join(",", driveByAssignments
				.OrderBy(item => item.Key)
				.Select(item => item.Key.ToString(CultureInfo.InvariantCulture) + ">" + item.Value.ToString(CultureInfo.InvariantCulture) + ":" + GetWeaponId(assignedWeapons.TryGetValue(item.Key, out WeaponConfig weapon) ? weapon : null)));
			VerificationLog("VehicleGroupCombat", $"driveby-distribution source={observerSource} mode={FormatDriveByTargetModeLogValue(driveByTargetMode)} attackerVehicle={rootAttacker.VehicleID.id} targetVehicle={rootTarget.VehicleID.id} assignments={assignments}");
		}

		private static void LogOnFootTargetAssignments(string observerSource, CrewAssignment rootAttacker, CrewAssignment rootTarget, Dictionary<ulong, ulong> targetAssignments, Dictionary<ulong, WeaponConfig> assignedWeapons)
		{
			if (targetAssignments == null || targetAssignments.Count == 0)
			{
				return;
			}
			string assignments = string.Join(",", targetAssignments
				.OrderBy(item => item.Key)
				.Select(item => item.Key.ToString(CultureInfo.InvariantCulture) + ">" + item.Value.ToString(CultureInfo.InvariantCulture) + ":" + GetWeaponId(assignedWeapons.TryGetValue(item.Key, out WeaponConfig weapon) ? weapon : null)));
			VerificationLog("VehicleGroupCombat", $"onfoot-distribution source={observerSource} mode=root-target attackerVehicle={rootAttacker.VehicleID.id} targetVehicle={rootTarget.VehicleID.id} assignments={assignments}");
		}

		private static Dictionary<ulong, WeaponConfig> BuildRoundWeaponAssignments(CombatManager combatManager, CrewAssignment rootAttacker, List<CrewAssignment> attackers, WeaponConfig selectedAttackWeapon, GroupedCombatMode combatMode, out Dictionary<ulong, string> assignmentSources)
		{
			assignmentSources = new Dictionary<ulong, string>();
			var assignments = new Dictionary<ulong, WeaponConfig>();
			if (combatManager == null || attackers == null || attackers.Count == 0)
			{
				return assignments;
			}

			bool applyPlayerWeaponFilters = rootAttacker.GetPeep()?.data?.agent?.pid.IsHumanPlayer == true;
			List<WeaponPoolEntry> pool = BuildWeaponPool(combatManager, rootAttacker.GetVehicle(), applyPlayerWeaponFilters);
			WeaponConfig fallbackWeapon = combatManager.GetFistsWeapon();
			if (combatMode == GroupedCombatMode.OnFoot && selectedAttackWeapon == null)
			{
				selectedAttackWeapon = FindPreferredOnFootSelectedWeapon(combatManager, rootAttacker, attackers, applyPlayerWeaponFilters) ?? selectedAttackWeapon;
			}
			EntityID selectedWeaponRecipientId = combatMode == GroupedCombatMode.DriveBy && attackers.Count > 0
				? attackers[0].peepId
				: rootAttacker.peepId;
			foreach (CrewAssignment attacker in attackers)
			{
				if (!attacker.peepId.IsValid)
				{
					continue;
				}
				WeaponConfig assignedWeapon = null;
				string source = "fallback-fists";
				bool selectedWeaponAllowed = !applyPlayerWeaponFilters || IsAllowedGroupedPlayerWeapon(selectedAttackWeapon, combatMode);
				bool explicitFistsSelection = combatMode == GroupedCombatMode.OnFoot && IsUnlimitedFallbackWeapon(selectedAttackWeapon);
				if (attacker.peepId == selectedWeaponRecipientId && selectedAttackWeapon != null && selectedWeaponAllowed)
				{
					if (!explicitFistsSelection && TryReserveSpecificWeapon(pool, selectedAttackWeapon))
					{
						assignedWeapon = selectedAttackWeapon;
						source = "selected-vehicle";
					}
					else
					{
						assignedWeapon = selectedAttackWeapon;
						source = "selected-popup";
					}
				}
				if (assignedWeapon == null && combatMode == GroupedCombatMode.OnFoot)
				{
					WeaponConfig bestPersonalWeapon = combatManager.FindBestWeapon(attacker);
					if (bestPersonalWeapon != null && !IsUnlimitedFallbackWeapon(bestPersonalWeapon) && (!applyPlayerWeaponFilters || IsAllowedGroupedPlayerWeapon(bestPersonalWeapon, combatMode)))
					{
						if (TryReserveSpecificWeapon(pool, bestPersonalWeapon))
						{
							assignedWeapon = bestPersonalWeapon;
							source = "vehicle-best";
						}
						else
						{
							assignedWeapon = bestPersonalWeapon;
							source = "peep-best";
						}
					}
				}
				if (assignedWeapon == null)
				{
					assignedWeapon = TakeBestAvailableWeapon(pool);
					if (assignedWeapon != null)
					{
						source = "vehicle-best";
					}
				}
				assignments[attacker.peepId.id] = assignedWeapon ?? fallbackWeapon;
				assignmentSources[attacker.peepId.id] = assignedWeapon != null ? source : "fallback-fists";
			}
			return assignments;
		}

		private static WeaponConfig FindPreferredOnFootSelectedWeapon(CombatManager combatManager, CrewAssignment rootAttacker, List<CrewAssignment> attackers, bool applyPlayerWeaponFilters)
		{
			if (combatManager == null)
			{
				return null;
			}
			foreach (CrewAssignment attacker in attackers ?? Enumerable.Empty<CrewAssignment>())
			{
				WeaponConfig bestPersonalWeapon = combatManager.FindBestWeapon(attacker);
				if (bestPersonalWeapon != null && !IsUnlimitedFallbackWeapon(bestPersonalWeapon) && (!applyPlayerWeaponFilters || IsAllowedGroupedPlayerWeapon(bestPersonalWeapon, GroupedCombatMode.OnFoot)))
				{
					return bestPersonalWeapon;
				}
			}
			return BuildWeaponPool(combatManager, rootAttacker.GetVehicle(), applyPlayerWeaponFilters)
				.Select(entry => entry.Weapon)
				.FirstOrDefault(weapon => weapon != null && !IsUnlimitedFallbackWeapon(weapon) && (!applyPlayerWeaponFilters || IsAllowedGroupedPlayerWeapon(weapon, GroupedCombatMode.OnFoot)));
		}

		private static List<WeaponPoolEntry> BuildWeaponPool(CombatManager combatManager, Entity vehicle, bool applyPlayerWeaponFilters)
		{
			var pool = new Dictionary<string, WeaponPoolEntry>(StringComparer.OrdinalIgnoreCase);
			InventoryModule inventory = ModulesUtil.GetInventory(vehicle);
			if (inventory?.data?.contents != null)
			{
				foreach (ResourceAndQty content in inventory.data.contents)
				{
					if (content.qty <= 0)
					{
						continue;
					}
					WeaponConfig weapon = combatManager.FindWeaponConfig(content.id);
					if (weapon == null || IsUnlimitedFallbackWeapon(weapon))
					{
						continue;
					}
					if (applyPlayerWeaponFilters && !IsAllowedGroupedPlayerWeapon(weapon))
					{
						continue;
					}
					int count = Math.Max(0, content.qty.IntFloor());
					if (count <= 0)
					{
						continue;
					}
					string weaponId = GetWeaponId(weapon);
					if (string.IsNullOrEmpty(weaponId))
					{
						continue;
					}
					if (!pool.TryGetValue(weaponId, out WeaponPoolEntry entry))
					{
						entry = new WeaponPoolEntry
						{
							WeaponId = weaponId,
							Weapon = weapon,
							Remaining = 0
						};
						pool[weaponId] = entry;
					}
					entry.Remaining += count;
				}
			}
			return pool.Values
				.OrderByDescending(entry => entry.Weapon?.high ?? Fixnum.ZERO)
				.ThenBy(entry => entry.WeaponId, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static bool TryReserveSpecificWeapon(List<WeaponPoolEntry> pool, WeaponConfig weapon)
		{
			if (weapon == null || pool == null)
			{
				return false;
			}
			string weaponId = GetWeaponId(weapon);
			WeaponPoolEntry match = pool.FirstOrDefault(entry => entry.Remaining > 0 && string.Equals(entry.WeaponId, weaponId, StringComparison.OrdinalIgnoreCase));
			if (match == null)
			{
				return false;
			}
			match.Remaining--;
			return true;
		}

		private static WeaponConfig TakeBestAvailableWeapon(List<WeaponPoolEntry> pool)
		{
			if (pool == null)
			{
				return null;
			}
			WeaponPoolEntry match = pool.FirstOrDefault(entry => entry.Remaining > 0);
			if (match == null)
			{
				return null;
			}
			match.Remaining--;
			return match.Weapon;
		}

		private static WeaponConfig ResolveAssignedWeapon(Dictionary<ulong, WeaponConfig> assignedWeapons, EntityID peepId, CombatManager combatManager)
		{
			if (assignedWeapons != null && peepId.IsValid && assignedWeapons.TryGetValue(peepId.id, out WeaponConfig assignedWeapon) && assignedWeapon != null)
			{
				return assignedWeapon;
			}
			return combatManager?.GetFistsWeapon();
		}

		private static void LogWeaponAssignments(string observerSource, Entity attackerPeep, CrewAssignment rootAttacker, List<CrewAssignment> attackers, Dictionary<ulong, WeaponConfig> assignedWeapons, Dictionary<ulong, string> assignmentSources, WeaponConfig selectedAttackWeapon, GroupedCombatMode combatMode)
		{
			if (attackers == null || attackers.Count == 0)
			{
				return;
			}
			int playerId = attackerPeep?.data?.agent?.pid.id ?? -1;
			ulong vehicleId = rootAttacker.GetVehicle()?.Id.id ?? 0UL;
			bool allowRanged = VehicleGroupCombatAllowRangedWeapons?.Value ?? true;
			bool allowMelee = VehicleGroupCombatAllowMeleeWeapons?.Value ?? true;
			string allocations = BuildWeaponAllocationSummary(attackers, assignedWeapons, assignmentSources);
			VerificationLog("VehicleGroupCombat", $"source={observerSource} mode={FormatCombatMode(combatMode)} player={playerId} vehicle={vehicleId} attackers={attackers.Count} allowRanged={allowRanged} allowMelee={allowMelee} selectedPopup={GetWeaponId(selectedAttackWeapon)} allocations={allocations}");
		}

		private static bool IsAiVehicleGroupCombatSource(string observerSource)
		{
			return string.Equals(observerSource, "PerformAICombat", StringComparison.Ordinal);
		}

		private static bool ShouldLogVehicleCombatDetails(string observerSource, CrewAssignment rootAttacker, CrewAssignment rootTarget)
		{
			if (!IsAiVehicleGroupCombatSource(observerSource))
			{
				return true;
			}

			return rootAttacker.GetPeep()?.data?.agent?.pid.IsHumanPlayer == true
				|| rootTarget.GetPeep()?.data?.agent?.pid.IsHumanPlayer == true;
		}

		private static string BuildWeaponAllocationSummary(List<CrewAssignment> attackers, Dictionary<ulong, WeaponConfig> assignedWeapons, Dictionary<ulong, string> assignmentSources)
		{
			if (attackers == null || attackers.Count == 0)
			{
				return "none";
			}
			return string.Join(",", attackers
				.Where(item => item.peepId.IsValid)
				.Select(item =>
				{
					string source = assignmentSources != null && assignmentSources.TryGetValue(item.peepId.id, out string resolvedSource)
						? resolvedSource
						: "unknown";
					return item.peepId.id + ":" + GetWeaponId(ResolveAssignedWeapon(assignedWeapons, item.peepId, null)) + "@" + source;
				}));
		}

		private static void LogAiCombatRoundSummary(string observerSource, CrewAssignment rootAttacker, CrewAssignment rootTarget, List<CrewAssignment> attackers, Dictionary<ulong, WeaponConfig> assignedWeapons, GroupedCombatMode combatMode)
		{
			Entity attackerPeep = rootAttacker.GetPeep();
			Entity targetPeep = rootTarget.GetPeep();
			PlayerID? attackerPid = attackerPeep?.data?.agent?.pid;
			PlayerID? targetPid = targetPeep?.data?.agent?.pid;
			List<CrewAssignment> defenderRecipients = GetEligibleDefendersForVehicle(rootTarget);
			int defenderCount = defenderRecipients.Count;
			string allocations = BuildWeaponAllocationSummary(attackers, assignedWeapons, null);
			VerificationLog("VehicleGroupCombat.AI", $"round source={observerSource} mode={FormatCombatMode(combatMode)} attackerPid={(attackerPid.HasValue ? attackerPid.Value.id : -1)} targetPid={(targetPid.HasValue ? targetPid.Value.id : -1)} attackerVehicle={rootAttacker.GetVehicle()?.Id.id ?? 0UL} targetVehicle={rootTarget.GetVehicle()?.Id.id ?? 0UL} attackerCount={attackers?.Count ?? 0} defenderCount={defenderCount} attackerGrouped={(attackers?.Count ?? 0) > 1} defenderGrouped={defenderCount > 1} allocations={allocations}");
		}

		private static void LogAiCombatResult(string observerSource, CombatResults result)
		{
			if (result == null || result.attacker.peep == null || result.target.peep == null)
			{
				return;
			}
			PlayerID? attackerPid = result.attacker.peep.data?.agent?.pid;
			PlayerID? targetPid = result.target.peep.data?.agent?.pid;
			VerificationLog("VehicleGroupCombat.AI", $"result source={observerSource} attackerPid={(attackerPid.HasValue ? attackerPid.Value.id : -1)} attackerPeep={result.attacker.peep.Id.id} attackerWeapon={GetWeaponId(result.attacker.weapon)} targetPid={(targetPid.HasValue ? targetPid.Value.id : -1)} targetPeep={result.target.peep.Id.id} defenderWeapon={GetWeaponId(result.target.weapon)} targetDamage={result.target.damage} counterDamage={result.attacker.damage} targetWasHurt={result.target.wasHurt} attackerWasHurt={result.attacker.wasHurt} targetDead={result.target.IsDead} attackerDead={result.attacker.IsDead}");
		}

		private static void LogAiDamageSplit(string observerSource, string label, CombatExchangeDebugInfo debugInfo)
		{
			string summary = BuildDamageSplitSummary(debugInfo?.DefenderRecipients, debugInfo?.PlannedDamageToDefenders, debugInfo?.AppliedDamageToDefenders);
			if (string.IsNullOrEmpty(summary))
			{
				return;
			}
			string distribution = string.IsNullOrEmpty(debugInfo?.DefenderDistributionMode) ? "even" : debugInfo.DefenderDistributionMode;
			string focusFields = string.Empty;
			if (distribution == "focused-ranged")
			{
				focusFields = $" focusPeep={debugInfo?.FocusedDefenderPeepId.id ?? 0UL} focusShare={(debugInfo?.FocusShare ?? 0f):0.00}";
			}
			string spillRecipients = BuildSpillRecipientSummary(debugInfo?.DefenderRecipients, debugInfo?.PlannedDamageToDefenders, debugInfo?.FocusedDefenderPeepId ?? EntityID.INVALID);
			string fallbackField = string.IsNullOrEmpty(debugInfo?.DistributionFallbackReason) ? string.Empty : $" fallback={debugInfo.DistributionFallbackReason}";
			VerificationLog("VehicleGroupCombat.AI", $"split source={observerSource} distribution={distribution} defenders={debugInfo?.DefenderRecipients?.Count ?? 0}{focusFields}{fallbackField} spill={spillRecipients} {label}={summary}");
		}

		private static void LogVehicleDamageSplit(string observerSource, CrewAssignment target, CombatExchangeDebugInfo debugInfo, GroupedCombatMode combatMode)
		{
			if (!target.IsValid || !target.IsInVehicle)
			{
				return;
			}
			string summary = BuildDamageSplitSummary(debugInfo?.DefenderRecipients, debugInfo?.PlannedDamageToDefenders, debugInfo?.AppliedDamageToDefenders);
			if (string.IsNullOrEmpty(summary))
			{
				return;
			}
			string distribution = string.IsNullOrEmpty(debugInfo?.DefenderDistributionMode) ? "even" : debugInfo.DefenderDistributionMode;
			string focusFields = distribution == "focused-ranged"
				? $" focusPeep={debugInfo?.FocusedDefenderPeepId.id ?? 0UL} focusShare={(debugInfo?.FocusShare ?? 0f):0.00}"
				: string.Empty;
			string spillRecipients = BuildSpillRecipientSummary(debugInfo?.DefenderRecipients, debugInfo?.PlannedDamageToDefenders, debugInfo?.FocusedDefenderPeepId ?? EntityID.INVALID);
			VerificationLog("VehicleGroupCombat", $"defender-split source={observerSource} mode={distribution} combatMode={FormatCombatMode(combatMode)} targetVehicle={target.VehicleID.id} defenders={debugInfo?.DefenderRecipients?.Count ?? 0}{focusFields} spill={spillRecipients} split={summary}");
			if (IsAiVehicleGroupCombatSource(observerSource))
			{
				LogAiDamageSplit(observerSource, "defenderSplit", debugInfo);
			}
		}

		private static string BuildDamageSplitSummary(List<CrewAssignment> recipients, Dictionary<EntityID, Fixnum> plannedDamage, Dictionary<EntityID, AppliedDamageInfo> appliedDamage)
		{
			if (recipients == null || recipients.Count == 0)
			{
				return string.Empty;
			}
			return string.Join(",", recipients
				.Where(item => item.peepId.IsValid)
				.Select(item =>
				{
					Fixnum planned = Fixnum.ZERO;
					AppliedDamageInfo applied = default;
					bool hasApplied = false;
					if (plannedDamage != null)
					{
						plannedDamage.TryGetValue(item.peepId, out planned);
					}
					if (appliedDamage != null)
					{
						hasApplied = appliedDamage.TryGetValue(item.peepId, out applied);
					}
					return item.peepId.id + ":" + planned + ":" + (hasApplied ? applied.damage.ToString() : Fixnum.ZERO.ToString()) + ":" + (hasApplied && applied.isDead);
				}));
		}

		private static string BuildSpillRecipientSummary(List<CrewAssignment> recipients, Dictionary<EntityID, Fixnum> plannedDamage, EntityID focusedPeepId)
		{
			if (recipients == null || recipients.Count == 0 || plannedDamage == null)
			{
				return "none";
			}
			List<string> spillRecipients = recipients
				.Where(item => item.peepId.IsValid && item.peepId != focusedPeepId && plannedDamage.TryGetValue(item.peepId, out Fixnum planned) && !planned.IsZero)
				.Select(item => item.peepId.id.ToString(CultureInfo.InvariantCulture))
				.ToList();
			return spillRecipients.Count == 0 ? "none" : string.Join(",", spillRecipients);
		}

		private static bool IsUnlimitedFallbackWeapon(WeaponConfig weapon)
		{
			return weapon != null && weapon.resid == EntityConstants.DEFAULT_WEAPON;
		}

		private static bool IsAllowedGroupedPlayerWeapon(WeaponConfig weapon, GroupedCombatMode mode = GroupedCombatMode.OnFoot)
		{
			if (weapon == null || IsUnlimitedFallbackWeapon(weapon))
			{
				return true;
			}
			if (mode == GroupedCombatMode.DriveBy && !IsRangedWeapon(weapon))
			{
				return false;
			}
			bool allowRanged = VehicleGroupCombatAllowRangedWeapons?.Value ?? true;
			bool allowMelee = VehicleGroupCombatAllowMeleeWeapons?.Value ?? true;
			bool isRanged = IsRangedWeapon(weapon);
			return isRanged ? allowRanged : allowMelee;
		}

		private static bool IsAllowedWeaponForCombatMode(WeaponConfig weapon, GroupedCombatMode mode)
		{
			if (weapon == null || IsUnlimitedFallbackWeapon(weapon))
			{
				return true;
			}
			if (mode == GroupedCombatMode.DriveBy)
			{
				return IsRangedWeapon(weapon);
			}
			return true;
		}

		private static bool IsRangedWeapon(WeaponConfig weapon)
		{
			if (weapon == null)
			{
				return false;
			}
			if (weapon.firearm)
			{
				return true;
			}
			string weaponId = GetWeaponId(weapon);
			return weaponId.IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("revolver", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("shotgun", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("thompson", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("tommy", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string GetWeaponId(WeaponConfig weapon)
		{
			return weapon?.resid.String ?? "weapon-fists";
		}

		private static CombatResults ResolveVehicleGroupExchange(CombatManager combatManager, CrewAssignment attacker, CrewAssignment target, WeaponConfig attackerWeapon, WeaponConfig defenderWeapon, GroupedCombatMode combatMode, CombatExchangeDebugInfo debugInfo = null, string observerSource = null, DriveByTargetMode driveByTargetMode = DriveByTargetMode.FocusOne)
		{
			Entity attackerPeep = attacker.GetPeep();
			Entity targetPeep = target.GetPeep();
			if (attackerPeep == null || targetPeep == null)
				return null;

			PlayerID attackerPid = attackerPeep.data.agent.pid;
			PlayerID targetPid = targetPeep.data.agent.pid;
			Node node = attackerPeep.components.agent.GetNode();
			if (node == null)
				return null;

			Fixnum attackDamage = CombatManager.CalculateDamage(attackerPeep, attackerWeapon, targetPeep);
			Fixnum counterDamage = CombatManager.CalculateDamage(targetPeep, defenderWeapon, attackerPeep);
			var vehicleDamageReduction = global::Game.Game.serv.globals.settings.people.combatSettings.vehicleDamageReduction;
			Fixnum attackerDefended = MathUtil.ClampMax(vehicleDamageReduction.Evaluate(new ModQuery(attackerPid, attackerPeep.Id, attackerPeep.Id)), counterDamage);
			Fixnum targetDefended = MathUtil.ClampMax(vehicleDamageReduction.Evaluate(new ModQuery(targetPid, targetPeep.Id, targetPeep.Id)), attackDamage);
			attackDamage -= targetDefended;
			counterDamage -= attackerDefended;

			List<CrewAssignment> defenderRecipients = GetEligibleDefendersForVehicle(target);
			if (defenderRecipients.Count == 0)
				defenderRecipients.Add(target);
			List<CrewAssignment> attackerRecipients = combatMode == GroupedCombatMode.DriveBy
				? new List<CrewAssignment> { attacker }
				: GetEligibleDefendersForVehicle(attacker);
			if (attackerRecipients.Count == 0)
				attackerRecipients.Add(attacker);

			Dictionary<EntityID, Fixnum> splitAttackDamage;
			string defenderDistributionMode = "even";
			EntityID focusedDefenderPeepId = EntityID.INVALID;
			float? focusShare = null;
			string distributionFallbackReason = null;
			if (combatMode == GroupedCombatMode.DriveBy && driveByTargetMode == DriveByTargetMode.SpreadAll && ShouldUseFocusedVehicleTargetDistribution(attackerWeapon, target, defenderRecipients))
			{
				defenderDistributionMode = "spread-all";
				splitAttackDamage = DistributeSpreadAllDamageAcrossOccupants(defenderRecipients, target.peepId, attackDamage, out distributionFallbackReason);
			}
			else if (ShouldUseFocusedVehicleTargetDistribution(attackerWeapon, target, defenderRecipients))
			{
				defenderDistributionMode = "focused-ranged";
				focusedDefenderPeepId = target.peepId;
				focusShare = GetFocusedRangedDamageShare(attackDamage);
				splitAttackDamage = DistributeFocusedRangedDamageAcrossOccupants(defenderRecipients, target.peepId, attackDamage, focusShare.Value);
			}
			else
			{
				splitAttackDamage = DistributeDamageAcrossOccupants(defenderRecipients, target.peepId, attackDamage);
			}
			Dictionary<EntityID, Fixnum> splitCounterDamage = DistributeDamageAcrossOccupants(attackerRecipients, attacker.peepId, counterDamage);
			Dictionary<EntityID, AppliedDamageInfo> defenderApplied = ApplyDistributedDamage(attackerPeep, defenderRecipients, splitAttackDamage);
			Dictionary<EntityID, AppliedDamageInfo> attackerApplied = ApplyDistributedDamage(targetPeep, attackerRecipients, splitCounterDamage);
			if (debugInfo != null)
			{
				debugInfo.DefenderRecipients = defenderRecipients;
				debugInfo.AttackerRecipients = attackerRecipients;
				debugInfo.PlannedDamageToDefenders = splitAttackDamage;
				debugInfo.PlannedDamageToAttackers = splitCounterDamage;
				debugInfo.AppliedDamageToDefenders = defenderApplied;
				debugInfo.AppliedDamageToAttackers = attackerApplied;
				debugInfo.DefenderDistributionMode = defenderDistributionMode;
				debugInfo.FocusedDefenderPeepId = focusedDefenderPeepId;
				debugInfo.FocusShare = focusShare;
				debugInfo.DistributionFallbackReason = distributionFallbackReason;
			}

			CombatResults result = BuildCombatResult(attacker, target, attackerWeapon, defenderWeapon, attackerDefended, targetDefended, attackerApplied, defenderApplied, node.id);
			ApplyCombatAftermath(result, attacker, target, attackerWeapon, defenderWeapon, attackerApplied, defenderApplied, node);
			return result;
		}

		private static Dictionary<EntityID, Fixnum> DistributeSpreadAllDamageAcrossOccupants(List<CrewAssignment> occupants, EntityID preferredPeepId, Fixnum totalDamage, out string fallbackReason)
		{
			fallbackReason = null;
			var result = new Dictionary<EntityID, Fixnum>();
			List<CrewAssignment> validOccupants = occupants?
				.Where(item => item.IsValid && item.peepId.IsValid)
				.ToList() ?? new List<CrewAssignment>();
			if (validOccupants.Count == 0 || totalDamage.IsZero)
			{
				fallbackReason = "no-occupants";
				return result;
			}
			if (validOccupants.Count == 1)
			{
				result[validOccupants[0].peepId] = totalDamage;
				fallbackReason = "single-occupant";
				return result;
			}
			if ((float)totalDamage <= validOccupants.Count)
			{
				fallbackReason = "low-damage-even";
				return DistributeDamageAcrossOccupants(validOccupants, preferredPeepId, totalDamage);
			}

			bool evenSpread = SharedRng.NextDouble() < 0.35;
			if (evenSpread)
			{
				fallbackReason = "even-roll";
				return DistributeDamageAcrossOccupants(validOccupants, preferredPeepId, totalDamage);
			}

			var weights = new Dictionary<ulong, float>();
			foreach (CrewAssignment occupant in validOccupants)
			{
				weights[occupant.peepId.id] = 0.55f + (float)SharedRng.NextDouble();
			}
			CrewAssignment heavyTarget = ChooseFocusedRangedDefender(validOccupants);
			if (!heavyTarget.IsValid || !heavyTarget.peepId.IsValid)
			{
				heavyTarget = FindFirstLivingTarget(validOccupants, preferredPeepId) ?? validOccupants[0];
			}
			if (heavyTarget.peepId.IsValid && weights.TryGetValue(heavyTarget.peepId.id, out float heavyWeight))
			{
				weights[heavyTarget.peepId.id] = heavyWeight + 1.4f + (float)SharedRng.NextDouble();
			}
			if (SharedRng.NextDouble() < 0.25)
			{
				CrewAssignment secondaryTarget = ChooseWeightedLivingDriveByTarget(validOccupants, weights, heavyTarget.peepId) ?? CrewAssignment.EMPTY;
				if (secondaryTarget.IsValid && secondaryTarget.peepId.IsValid && secondaryTarget.peepId != heavyTarget.peepId && weights.TryGetValue(secondaryTarget.peepId.id, out float secondaryWeight))
				{
					weights[secondaryTarget.peepId.id] = secondaryWeight + 0.4f + (float)SharedRng.NextDouble() * 0.6f;
				}
			}
			return AllocateWeightedDamage(validOccupants, weights, preferredPeepId, totalDamage);
		}

		private static Dictionary<EntityID, Fixnum> DistributeDamageAcrossOccupants(List<CrewAssignment> occupants, EntityID primaryPeepId, Fixnum totalDamage)
		{
			var result = new Dictionary<EntityID, Fixnum>();
			if (occupants == null || occupants.Count == 0 || totalDamage.IsZero)
				return result;

			Fixnum share = totalDamage / occupants.Count;
			Fixnum remainder = totalDamage - share * occupants.Count;
			foreach (CrewAssignment occupant in occupants)
			{
				if (!occupant.peepId.IsValid)
					continue;
				result[occupant.peepId] = share;
			}

			if (result.ContainsKey(primaryPeepId))
				result[primaryPeepId] += remainder;
			else
				result[occupants[0].peepId] += remainder;

			return result;
		}

		private static bool ShouldUseFocusedVehicleTargetDistribution(WeaponConfig attackerWeapon, CrewAssignment target, List<CrewAssignment> defenderRecipients)
		{
			return IsRangedWeapon(attackerWeapon)
				&& target.IsInVehicle
				&& defenderRecipients != null
				&& defenderRecipients.Count > 1;
		}

		private static CrewAssignment ChooseFocusedRangedDefender(List<CrewAssignment> defenders)
		{
			if (defenders == null || defenders.Count == 0)
			{
				return CrewAssignment.EMPTY;
			}
			List<CrewAssignment> validDefenders = defenders
				.Where(item => item.IsValid && item.peepId.IsValid && item.GetPeep()?.components?.agent?.HasHealthPointsLeft == true)
				.ToList();
			if (validDefenders.Count == 0)
			{
				return CrewAssignment.EMPTY;
			}
			if (validDefenders.Count == 1)
			{
				return validDefenders[0];
			}
			int index = SharedRng.Next(validDefenders.Count);
			return validDefenders[Mathf.Clamp(index, 0, validDefenders.Count - 1)];
		}

		private static float GetFocusedRangedDamageShare(Fixnum totalDamage)
		{
			float damage = Mathf.Max(0f, (float)totalDamage);
			float normalized = Mathf.Clamp01((damage - 8f) / 24f);
			return Mathf.Lerp(0.60f, 0.85f, normalized);
		}

		private static Dictionary<EntityID, Fixnum> DistributeFocusedRangedDamageAcrossOccupants(List<CrewAssignment> occupants, EntityID focusedPeepId, Fixnum totalDamage, float focusShare)
		{
			var result = new Dictionary<EntityID, Fixnum>();
			if (occupants == null || occupants.Count == 0 || totalDamage.IsZero)
			{
				return result;
			}
			List<CrewAssignment> validOccupants = occupants
				.Where(item => item.peepId.IsValid)
				.ToList();
			if (validOccupants.Count == 0)
			{
				return result;
			}
			if (validOccupants.Count == 1 || !validOccupants.Any(item => item.peepId == focusedPeepId))
			{
				result[validOccupants[0].peepId] = totalDamage;
				return result;
			}
			focusShare = Mathf.Clamp(focusShare, 0f, 1f);
			int focusPercent = Mathf.Clamp(Mathf.RoundToInt(focusShare * 100f), 0, 100);
			Fixnum focusedDamage = totalDamage * (Fixnum)focusPercent / 100;
			Fixnum spreadDamage = totalDamage - focusedDamage;
			int nonFocusedCount = validOccupants.Count - 1;
			Fixnum evenShare = nonFocusedCount > 0 ? spreadDamage / nonFocusedCount : Fixnum.ZERO;
			Fixnum remainder = spreadDamage - evenShare * nonFocusedCount;
			foreach (CrewAssignment occupant in validOccupants)
			{
				if (occupant.peepId == focusedPeepId)
				{
					result[occupant.peepId] = focusedDamage + remainder;
				}
				else
				{
					result[occupant.peepId] = evenShare;
				}
			}
			return result;
		}

		private static Dictionary<EntityID, Fixnum> AllocateWeightedDamage(List<CrewAssignment> occupants, Dictionary<ulong, float> weights, EntityID preferredPeepId, Fixnum totalDamage)
		{
			var result = new Dictionary<EntityID, Fixnum>();
			if (occupants == null || occupants.Count == 0 || totalDamage.IsZero)
			{
				return result;
			}
			List<CrewAssignment> validOccupants = occupants
				.Where(item => item.IsValid && item.peepId.IsValid)
				.ToList();
			if (validOccupants.Count == 0)
			{
				return result;
			}
			if (validOccupants.Count == 1)
			{
				result[validOccupants[0].peepId] = totalDamage;
				return result;
			}
			float totalWeight = 0f;
			foreach (CrewAssignment occupant in validOccupants)
			{
				float weight = 1f;
				if (weights != null && weights.TryGetValue(occupant.peepId.id, out float storedWeight) && storedWeight > 0f)
				{
					weight = storedWeight;
				}
				totalWeight += weight;
			}
			if (totalWeight <= 0f)
			{
				return DistributeDamageAcrossOccupants(validOccupants, preferredPeepId, totalDamage);
			}

			var percents = new Dictionary<ulong, int>();
			var remainders = new List<KeyValuePair<ulong, float>>();
			int usedPercent = 0;
			foreach (CrewAssignment occupant in validOccupants)
			{
				float weight = 1f;
				if (weights != null && weights.TryGetValue(occupant.peepId.id, out float storedWeight) && storedWeight > 0f)
				{
					weight = storedWeight;
				}
				float exactPercent = weight / totalWeight * 100f;
				int wholePercent = Mathf.Clamp(Mathf.FloorToInt(exactPercent), 0, 100);
				percents[occupant.peepId.id] = wholePercent;
				usedPercent += wholePercent;
				remainders.Add(new KeyValuePair<ulong, float>(occupant.peepId.id, exactPercent - wholePercent));
			}
			foreach (KeyValuePair<ulong, float> remainder in remainders.OrderByDescending(item => item.Value).ThenBy(item => item.Key))
			{
				if (usedPercent >= 100)
				{
					break;
				}
				percents[remainder.Key]++;
				usedPercent++;
			}

			Fixnum allocatedTotal = Fixnum.ZERO;
			foreach (CrewAssignment occupant in validOccupants)
			{
				int percent = percents.TryGetValue(occupant.peepId.id, out int resolvedPercent) ? resolvedPercent : 0;
				Fixnum share = totalDamage * percent / 100;
				result[occupant.peepId] = share;
				allocatedTotal += share;
			}
			Fixnum remainderDamage = totalDamage - allocatedTotal;
			if (!remainderDamage.IsZero)
			{
				EntityID remainderTarget = preferredPeepId;
				if (!remainderTarget.IsValid || !result.ContainsKey(remainderTarget))
				{
					ulong weightedTargetId = percents.OrderByDescending(item => item.Value).ThenBy(item => item.Key).First().Key;
					remainderTarget = EntityID.FromID(weightedTargetId);
				}
				result[remainderTarget] += remainderDamage;
			}
			return result;
		}

		private static Dictionary<EntityID, AppliedDamageInfo> ApplyDistributedDamage(Entity attackerPeep, List<CrewAssignment> recipients, Dictionary<EntityID, Fixnum> damageMap)
		{
			var result = new Dictionary<EntityID, AppliedDamageInfo>();
			foreach (CrewAssignment recipient in recipients)
			{
				Entity recipientPeep = recipient.GetPeep();
				if (recipientPeep == null || !damageMap.TryGetValue(recipient.peepId, out Fixnum plannedDamage))
					continue;

				Fixnum damage = plannedDamage;
				bool wasHurt = ApplyDamageLikeVanilla(recipientPeep, ref damage);
				HandleDeathLikeVanilla(attackerPeep, recipientPeep);
				result[recipient.peepId] = new AppliedDamageInfo
				{
					damage = damage,
					wasHurt = wasHurt,
					isDead = !recipientPeep.components.agent.HasHealthPointsLeft
				};
			}
			return result;
		}

		private static CombatResults BuildCombatResult(CrewAssignment attacker, CrewAssignment target, WeaponConfig attackerWeapon, WeaponConfig defenderWeapon, Fixnum attackerDefended, Fixnum targetDefended, Dictionary<EntityID, AppliedDamageInfo> attackerApplied, Dictionary<EntityID, AppliedDamageInfo> defenderApplied, NodeID nodeId)
		{
			Entity attackerPeep = attacker.GetPeep();
			Entity targetPeep = target.GetPeep();
			AppliedDamageInfo attackerInfo = attackerApplied.TryGetValue(attacker.peepId, out AppliedDamageInfo attackerPrimary) ? attackerPrimary : default;
			AppliedDamageInfo targetInfo = defenderApplied.TryGetValue(target.peepId, out AppliedDamageInfo targetPrimary) ? targetPrimary : default;
			return new CombatResults
			{
				attacker = new CombatResults.Entry(attackerPeep, attackerWeapon, attackerInfo.damage, attackerInfo.wasHurt, attackerDefended),
				target = new CombatResults.Entry(targetPeep, defenderWeapon, targetInfo.damage, targetInfo.wasHurt, targetDefended),
				nodeId = nodeId
			};
		}

		private static void ApplyCombatAftermath(CombatResults result, CrewAssignment attacker, CrewAssignment target, WeaponConfig attackerWeapon, WeaponConfig defenderWeapon, Dictionary<EntityID, AppliedDamageInfo> attackerApplied, Dictionary<EntityID, AppliedDamageInfo> defenderApplied, Node node)
		{
			Entity attackerPeep = attacker.GetPeep();
			Entity targetPeep = target.GetPeep();
			PlayerID attackerPid = attackerPeep.data.agent.pid;
			PlayerID targetPid = targetPeep.data.agent.pid;

			var timesFought = new HashSet<ulong> { attacker.peepId.id, target.peepId.id };
			foreach (KeyValuePair<EntityID, AppliedDamageInfo> item in defenderApplied)
			{
				if ((item.Value.wasHurt || item.Key == target.peepId) && item.Key.IsValid)
					timesFought.Add(item.Key.id);
			}
			foreach (KeyValuePair<EntityID, AppliedDamageInfo> item2 in attackerApplied)
			{
				if ((item2.Value.wasHurt || item2.Key == attacker.peepId) && item2.Key.IsValid)
					timesFought.Add(item2.Key.id);
			}
			foreach (ulong peepId in timesFought)
			{
				Entity peep = EntityID.FromID(peepId).FindEntity();
				peep?.components?.agent?.IncrementStat(CrewStats.TimesFought, 1);
			}

			bool someoneDied = defenderApplied.Values.Any(info => info.isDead) || attackerApplied.Values.Any(info => info.isDead);
			node.heat.AddViolenceBuff(attackerPid, attackerPeep.Id, node, someoneDied);
			node.heat.AddViolenceBuff(targetPid, targetPeep.Id, node, someoneDied);

			if (result.target.wasHurt)
			{
				Label socialAction = result.target.IsDead ? SocialConstants.KILLING : SocialConstants.VIOLENCE;
				attackerPid.FindPlayer().social.PerformSocialActionOn(socialAction, targetPeep.Id, attackerPeep.Id, (ExtendHistoryInfo)ExtendLedger);
			}

			int defenderDeaths = defenderApplied.Values.Count(info => info.isDead);
			if (defenderDeaths > 0)
				attackerPeep.components.agent.IncrementStat(CrewStats.PeepsKilled, defenderDeaths);
			int attackerDeaths = attackerApplied.Values.Count(info => info.isDead);
			if (attackerDeaths > 0)
				targetPeep.components.agent.IncrementStat(CrewStats.PeepsKilled, attackerDeaths);

			if (targetPid.IsAIPlayer)
				targetPid.FindPlayer().ai?.social?.OnBeingAttacked(result);

			attackerPeep.components.agent.AddXP(XPSource.FromCombat);
			attacker.GetVehicle()?.components.mobile.ProcessAttack(attacker, defenderWeapon);
			target.GetVehicle()?.components.mobile.ProcessAttack(target, attackerWeapon);
			if (attackerPid.IsHumanPlayer || targetPid.IsHumanPlayer)
				PlayCombatVfxLikeVanilla(node, attacker, target, result.target.damage, result.attacker.damage);
			global::Game.Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.GangWarAction, attackerPid.FindPlayer().crew.GetCrewForPlayerPeep().peepId, attackerPid, targetPid));

			HistoryLedgerItem ExtendLedger(HistoryLedgerItem info)
			{
				info.actor = attackerPeep.Id;
				info.target = targetPeep.Id;
				info.method = attackerWeapon?.resid.String;
				info.node = result.nodeId;
				return info;
			}
		}

		private static bool ApplyDamageLikeVanilla(Entity target, ref Fixnum damage)
		{
			if (damage.IsZero)
				return false;
			PlayerInfo player = target.data.agent.pid.FindPlayer();
			if (player != null && player.IsInvincibleCheatEnabled)
				return false;
			damage = Fixnum.Min(damage, target.components.agent.CurrentHealth);
			if (player != null && player.IsHuman && global::Game.Game.ctx.tutorial.AreInjuriesSuppressed)
			{
				Fixnum minimumHealth = 75;
				Fixnum capped = Fixnum.Max(target.components.agent.CurrentHealth - minimumHealth, 0);
				damage = Fixnum.Min(damage, capped);
			}
			target.components.agent.IncrementHealth(-damage);
			target.components.agent.RememberInjuryAtThisTime();
			global::Game.Game.ctx.events.EnqueueOnce(SessionEventType.CrewMemberAttacked, player.PID, target.Id);
			return true;
		}

		private static void HandleDeathLikeVanilla(Entity attacker, Entity target)
		{
			if (attacker == null || target == null || target.components.agent.HasHealthPointsLeft)
				return;
			object deathSnapshot = MultiCrewVehicleHelper.CaptureVehicleOccupantDeathSnapshot(target, "grouped-combat");
			if (IsGroupedCombatTransactionActive())
			{
				EntityID vehicleId = TryFindCrewForPeep(target).VehicleID;
				VerificationLog("VehicleGroupCombat", $"death source={_groupedCombatTransactionSource} attacker={attacker.Id.id} target={target.Id.id} targetVehicle={vehicleId.id}");
				GameplayTweaksPlugin.VerificationLog("DeathAttribution", $"source=grouped-combat attacker={attacker.Id.id} target={target.Id.id} targetVehicle={vehicleId.id}");
			}
			GameplayTweaksPlugin.RecordDeathSource(target.Id, "grouped-combat");
			if (target.data.agent.pid.IsHumanPlayer)
				PassMoneyLikeVanilla(attacker, target);
			global::Game.Game.ctx.simman.peoplegen.MarkAsDead(target, global::Game.Game.ctx.clock.Now);
			MultiCrewVehicleHelper.ApplyVehicleOccupantDeathSnapshot(deathSnapshot);
		}

		private static void PassMoneyLikeVanilla(Entity attacker, Entity target)
		{
			Entity attackerVehicle = TryFindCrewForPeep(attacker).GetVehicle();
			Entity targetVehicle = TryFindCrewForPeep(target).GetVehicle();
			if (attackerVehicle == null || targetVehicle == null)
				return;
			PlayerFinances loserFinances = target.data.agent.pid.FindPlayer().finances;
			PlayerFinances winnerFinances = attacker.data.agent.pid.FindPlayer().finances;
			Money money = targetVehicle.components.modules.inventory.data.money;
			if (money.cash.IsZero)
				return;
			loserFinances.DoChangeMoney(targetVehicle, new Price(-money.cash), MoneyReason.CombatDefeat, attacker.Id);
			winnerFinances.DoChangeMoney(attackerVehicle, new Price(money.cash), MoneyReason.CombatVictory, target.Id);
		}

		private static void PlayCombatVfxLikeVanilla(Node node, CrewAssignment attacker, CrewAssignment target, Fixnum damageToTarget, Fixnum damageToAttacker)
		{
			WorldPos nodePos = node.pos;
			WorldPos attackerPos = attacker.GetVehicle()?.data.mobile.worldpos ?? WorldPos.Zero;
			WorldPos targetPos = target.GetVehicle()?.data.mobile.worldpos ?? WorldPos.Zero;
			MakeDamageFlyout(targetPos, damageToTarget);
			MakeDamageFlyout(attackerPos, damageToAttacker);
			global::Game.Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, nodePos, PlayerID.HumanPlayer, 1f);
		}

		private static void MakeDamageFlyout(WorldPos pos, Fixnum damage)
		{
			string text = TextUtil.ColorWrap(damage.IsZero ? Loc.Get("ui.fight.damage.miss") : Loc.Get("ui.fight.damage.total", "dealt", damage.ToString()), ColorConstants.TEXT_HEX_RED);
			global::Game.Game.ctx.hud.flyouts.MakeSimpleTextFlyout(pos, text, 3f);
		}

		private static void InvokeAttackCost(CombatManager combatManager, Entity peep)
		{
			try
			{
				CombatDoPayAttackCostMethod?.Invoke(combatManager, new object[] { peep });
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch attack cost: " + ex.Message);
			}
		}

		private static void DispatchCombatObservers(CombatResults result, string observerSource)
		{
			PactOpsCombatPatch.ProcessCombatResult(result, observerSource);
			RealCombatGrapevinePatch.ProcessCombatResult(result);
			BossMurderWarrantPatch.ProcessCombatResult(result);
			CombatObserverBridge.Publish(result, observerSource);
		}

		private static List<CombatResults> NormalizeCombatResultsForDisplay(List<CombatResults> results, bool isAttackerAI)
		{
			_ = isAttackerAI;
			if (results == null || results.Count == 0)
			{
				return results;
			}
			return results
				.Where(result => result != null && result.attacker.peep != null && result.target.peep != null)
				.ToList();
		}

		private static string BuildCombatDisplayGroupKey(CombatResults result, bool isAttackerAI)
		{
			CrewAssignment attackerCrew = TryFindCrewForPeep(result.attacker.peep);
			CrewAssignment targetCrew = TryFindCrewForPeep(result.target.peep);
			string attackerKey = attackerCrew.IsValid && attackerCrew.IsInVehicle && attackerCrew.VehicleID.IsValid
				? "vehicle:" + attackerCrew.VehicleID.id.ToString(CultureInfo.InvariantCulture)
				: "peep:" + result.attacker.peep.Id.id.ToString(CultureInfo.InvariantCulture);
			string targetKey = targetCrew.IsValid && targetCrew.IsInVehicle && targetCrew.VehicleID.IsValid
				? "vehicle:" + targetCrew.VehicleID.id.ToString(CultureInfo.InvariantCulture)
				: "peep:" + result.target.peep.Id.id.ToString(CultureInfo.InvariantCulture);
			return $"{result.nodeId}:{attackerKey}:{targetKey}:{isAttackerAI}";
		}

		private static CombatResults PickCombatDisplayRepresentative(List<CombatResults> bucket)
		{
			if (bucket == null || bucket.Count == 0)
			{
				return null;
			}
			CombatResults lethalTarget = bucket
				.Where(item => item != null && item.target.IsDead)
				.OrderByDescending(item => item.target.IsHumanCrew)
				.ThenByDescending(item => (float)(item.target.damage + item.attacker.damage))
				.FirstOrDefault();
			if (lethalTarget != null)
			{
				VerificationLog("VehicleGroupCombat", $"display-representative lethalPreferred=true attacker={lethalTarget.attacker.peep?.Id.id ?? 0UL} target={lethalTarget.target.peep?.Id.id ?? 0UL}");
				return lethalTarget;
			}
			CombatResults lethalAttacker = bucket
				.Where(item => item != null && item.attacker.IsDead)
				.OrderByDescending(item => item.attacker.IsHumanCrew)
				.ThenByDescending(item => (float)(item.target.damage + item.attacker.damage))
				.FirstOrDefault();
			if (lethalAttacker != null)
			{
				VerificationLog("VehicleGroupCombat", $"display-representative lethalPreferred=true attacker={lethalAttacker.attacker.peep?.Id.id ?? 0UL} target={lethalAttacker.target.peep?.Id.id ?? 0UL}");
				return lethalAttacker;
			}
			return bucket
				.OrderByDescending(item => item.target.IsHumanCrew || item.attacker.IsHumanCrew)
				.ThenByDescending(item => (float)(item.target.damage + item.attacker.damage))
				.FirstOrDefault();
		}

		private static void ShowCombatResults(List<CombatResults> results, bool immediate, bool isAttackerAI)
		{
			if (results == null || results.Count == 0)
				return;
			try
			{
				List<CombatResults> displayResults = NormalizeCombatResultsForDisplay(results, isAttackerAI);
				CombatManager combatManager = global::Game.Game.ctx?.simman?.combat;
				if (combatManager != null && CombatShowCombatResultsMethod != null)
				{
					CombatShowCombatResultsMethod.Invoke(combatManager, new object[] { displayResults, immediate, isAttackerAI });
					return;
				}

				CombatSummary summary = PhotoPopupUtils.GenerateCombatSummary(displayResults);
				if (summary.humanCrewDied.IsValid)
				{
					CombatManager.ShowCrewDied(summary);
				}
				if (immediate)
				{
					PhotoPopupUtils.ShowCombatSummary(summary, displayResults, isAttackerAI);
					return;
				}
				if (isAttackerAI)
				{
					global::Game.Game.ctx?.sfx?.PlayCombatHappened();
				}
				object tickers = global::Game.Game.ctx?.hud?.tickers;
				MethodInfo addTickerCombatResultsMethod = tickers?.GetType().GetMethod("AddTickerCombatResults", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(CombatSummary), typeof(List<CombatResults>) }, null);
				addTickerCombatResultsMethod?.Invoke(tickers, new object[] { summary, displayResults });
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleGroupCombatPatch.ShowCombatResults: " + ex.Message);
			}
		}
	}


	private static class PactOpsCombatPatch
	{
		private static readonly HashSet<string> _patchedSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private static readonly HashSet<string> _seenEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private static int _seenEventDay = -1;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
				if (type == null)
				{
					return;
				}
				HarmonyMethod harmonyMethod = new HarmonyMethod(typeof(PactOpsCombatPatch), "CombatPostfix", (Type[])null);
				foreach (MethodInfo item in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
				{
					if (!string.Equals(item.Name, "PerformCombat", StringComparison.Ordinal) && !string.Equals(item.Name, "PerformHumanCombat", StringComparison.Ordinal))
					{
						continue;
					}
					string text = item.ToString();
					if (_patchedSignatures.Contains(text))
					{
						continue;
					}
					harmony.Patch((MethodBase)item, (HarmonyMethod)null, harmonyMethod, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					_patchedSignatures.Add(text);
				}
				VerificationLog("GangOps.Combat", $"hooks={_patchedSignatures.Count}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GangOpsCombatPatch failed: " + ex.Message);
			}
		}

		private static void CombatPostfix(object __result, MethodBase __originalMethod)
		{
			try
			{
				ProcessCombatResult(__result as CombatResults, __originalMethod?.Name ?? "PerformCombat");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GangOps combat event parse failed: " + ex.Message);
			}
		}

		internal static void ProcessCombatResult(CombatResults result, string source)
		{
			if (result == null || !EnableAIAlliances.Value || result.attacker.peep == null || result.target.peep == null)
				return;
			int days = G.GetNow().days;
			if (_seenEventDay != days)
			{
				_seenEventDay = days;
				_seenEvents.Clear();
			}
			PlayerID pid = result.attacker.peep.data.agent.pid;
			PlayerID pid2 = result.target.peep.data.agent.pid;
			if (pid.id < 0 || pid2.id < 0 || pid.id == pid2.id)
				return;
			bool lethal = result.target.IsDead;
			string key = days.ToString(CultureInfo.InvariantCulture) + ":" + pid.id.ToString(CultureInfo.InvariantCulture) + ":" + pid2.id.ToString(CultureInfo.InvariantCulture) + ":" + result.attacker.peep.Id.id.ToString(CultureInfo.InvariantCulture) + ":" + result.target.peep.Id.id.ToString(CultureInfo.InvariantCulture) + ":" + lethal.ToString();
			if (!_seenEvents.Add(key))
				return;
			RegisterGangOpsHostileEvent(pid.id, pid2.id, lethal, (long)result.attacker.peep.Id.id);
			VerificationLog("GangOps.Combat", $"event method={source} attacker={pid.id} target={pid2.id} lethal={lethal}");
		}
	}


	private static class RealCombatGrapevinePatch
	{
		private static int _lastSeenDay = -1;

		private static readonly HashSet<string> _seenNonLethalPairs = new HashSet<string>(StringComparer.Ordinal);

		private static readonly HashSet<string> _seenLethalPairs = new HashSet<string>(StringComparer.Ordinal);

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
				if (combatType == null)
				{
					return;
				}
				MethodInfo performCombat = combatType.GetMethod("PerformCombat", BindingFlags.Instance | BindingFlags.Public, null, new Type[4]
				{
					typeof(CrewAssignment),
					typeof(CrewAssignment),
					typeof(WeaponConfig),
					typeof(WeaponConfig)
				}, null);
				if (performCombat == null)
				{
					Debug.LogWarning("[GameplayTweaks] RealCombatGrapevinePatch: PerformCombat signature not found.");
					return;
				}
				harmony.Patch((MethodBase)performCombat, (HarmonyMethod)null, new HarmonyMethod(typeof(RealCombatGrapevinePatch), "PerformCombatPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				VerificationLog("GrapevineCombat", "hooked PerformCombat(CrewAssignment,CrewAssignment,WeaponConfig,WeaponConfig)");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] RealCombatGrapevinePatch failed: " + ex.Message);
			}
		}

		private static void PerformCombatPostfix(CrewAssignment crew1, CrewAssignment crew2, WeaponConfig weapon1, WeaponConfig weapon2, CombatResults __result)
		{
			try
			{
				ProcessCombatResult(__result);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] RealCombatGrapevinePatch parse failed: " + ex.Message);
			}
		}

		internal static void ProcessCombatResult(CombatResults result)
		{
			if (result == null || result.attacker.peep == null || result.target.peep == null)
				return;
			PlayerID attackerPid = result.attacker.peep.data.agent.pid;
			PlayerID defenderPid = result.target.peep.data.agent.pid;
			if (attackerPid.id < 0 || defenderPid.id < 0 || attackerPid.id == defenderPid.id)
				return;
			int day = G.GetNow().days;
			EnsureDayCache(day);
			string pairKey = MakeGangPairKey(attackerPid.id, defenderPid.id);
			bool lethal = result.attacker.IsDead || result.target.IsDead;
			string attackerName = GetGangDisplayName(attackerPid.id);
			string defenderName = GetGangDisplayName(defenderPid.id);
			string fightVerb = DetermineFightVerb(result.attacker.weapon, result.target.weapon);
			string weapons = BuildWeaponVersusText(result.attacker.weapon, result.target.weapon);
			if (lethal)
			{
				if (!_seenLethalPairs.Add(pairKey))
					return;
				LogGrapevine(FormatCombatGrapevineText("DEATH", fightVerb, attackerName, defenderName, weapons, "left at least one dead."));
				return;
			}
			if (_seenLethalPairs.Contains(pairKey) || !_seenNonLethalPairs.Add(pairKey))
				return;
			string prefix = string.Equals(fightVerb, "Shootout", StringComparison.Ordinal) ? "SHOOTOUT" : "FIGHT";
			string nonLethalOutcome = DescribeNonLethalOutcome(result.attacker.wasHurt, result.target.wasHurt);
			LogGrapevine(FormatCombatGrapevineText(prefix, fightVerb, attackerName, defenderName, weapons, nonLethalOutcome));
		}

		private static void EnsureDayCache(int day)
		{
			if (_lastSeenDay != day)
			{
				_lastSeenDay = day;
				_seenNonLethalPairs.Clear();
				_seenLethalPairs.Clear();
			}
		}

		private static string MakeGangPairKey(int pid1, int pid2)
		{
			int low = Mathf.Min(pid1, pid2);
			int high = Mathf.Max(pid1, pid2);
			return low.ToString(CultureInfo.InvariantCulture) + ":" + high.ToString(CultureInfo.InvariantCulture);
		}

		private static string BuildWeaponVersusText(WeaponConfig weapon1, WeaponConfig weapon2)
		{
			return "(" + DescribeWeaponLabel(weapon1) + " vs " + DescribeWeaponLabel(weapon2) + ")";
		}

		private static string FormatCombatGrapevineText(string prefix, string fightVerb, string attackerName, string defenderName, string weapons, string outcomeText)
		{
			return prefix + ": " + fightVerb + " between " + attackerName + " and " + defenderName + " on the streets " + weapons + " " + outcomeText;
		}

		private static string DetermineFightVerb(WeaponConfig weapon1, WeaponConfig weapon2)
		{
			if (IsFirearm(weapon1) || IsFirearm(weapon2))
			{
				return "Shootout";
			}
			if (IsKnifeWeapon(weapon1) || IsKnifeWeapon(weapon2))
			{
				return "Knife fight";
			}
			return "Brawl";
		}

		private static bool IsFirearm(WeaponConfig weapon)
		{
			if (weapon == null)
			{
				return false;
			}
			if (weapon.firearm)
			{
				return true;
			}
			string weaponId = GetWeaponId(weapon);
			return weaponId.IndexOf("pistol", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("revolver", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("shotgun", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("rifle", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("thompson", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("tommy", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static bool IsKnifeWeapon(WeaponConfig weapon)
		{
			string weaponId = GetWeaponId(weapon);
			if (string.IsNullOrEmpty(weaponId))
			{
				return false;
			}
			return weaponId.IndexOf("knife", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("shiv", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("blade", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("dagger", StringComparison.OrdinalIgnoreCase) >= 0
				|| weaponId.IndexOf("machete", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string GetWeaponId(WeaponConfig weapon)
		{
			if (weapon == null)
			{
				return string.Empty;
			}
			try
			{
				return weapon.resid.String ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		private static string DescribeWeaponLabel(WeaponConfig weapon)
		{
			string text = GetWeaponId(weapon);
			if (string.IsNullOrEmpty(text))
			{
				return "Fists";
			}
			if (text.StartsWith("weapon-", StringComparison.OrdinalIgnoreCase))
			{
				text = text.Substring("weapon-".Length);
			}
			text = text.Replace('-', ' ');
			if (string.IsNullOrWhiteSpace(text))
			{
				return "Fists";
			}
			return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
		}

		private static string DescribeNonLethalOutcome(bool attackerWasHurt, bool defenderWasHurt)
		{
			if (attackerWasHurt && defenderWasHurt)
			{
				return "left several bruised and hospitalized.";
			}
			if (attackerWasHurt || defenderWasHurt)
			{
				return "left one side bruised and hospitalized.";
			}
			return "ended with no serious injuries.";
		}
	}


	private static class BossMurderWarrantPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type combatType = typeof(GameClock).Assembly.GetType("Game.Session.Sim.CombatManager");
				if (combatType == null)
				{
					return;
				}
				MethodInfo performCombat = combatType.GetMethod("PerformCombat", BindingFlags.Static | BindingFlags.Public);
				if (performCombat != null)
				{
					harmony.Patch((MethodBase)performCombat, (HarmonyMethod)null, new HarmonyMethod(typeof(BossMurderWarrantPatch), "PerformCombatPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					Debug.Log("[GameplayTweaks] Boss murder warrant patch enabled");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"[GameplayTweaks] BossMurderWarrantPatch failed: {ex}");
			}
		}

		private static void PerformCombatPostfix(object __result)
		{
			try
			{
				ProcessCombatResult(__result as CombatResults);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] BossMurderWarrantPatch runtime failed: {ex.Message}");
			}
		}

		internal static void ProcessCombatResult(CombatResults result)
		{
			if (result == null || !result.target.IsDead)
				return;
			Entity attackerPeep = result.attacker.peep;
			Entity targetPeep = result.target.peep;
			if (attackerPeep == null || targetPeep == null)
				return;
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer == null)
				return;
			if (attackerPeep.data?.agent == null)
				return;
			PlayerID attackerPid = attackerPeep.data.agent.pid;
			if (attackerPid.IsNotAnyPlayer)
				return;
			PlayerInfo attackerPlayer = PlayerIDExtensions.FindPlayer(attackerPid);
			if (attackerPlayer == null || attackerPlayer.crew == null || attackerPlayer.crew.IsCrewDefeated)
				return;
			if (!TryGetKilledBossOwner(targetPeep, out PlayerInfo victimGang))
				return;
			if (victimGang.PID.id == attackerPlayer.PID.id)
				return;
			AlliancePact pactForVictim = GetPactForPlayer(victimGang.PID);
			bool attackerIsHuman = attackerPid == humanPlayer.PID;
			bool hasWitnessEvidence = attackerIsHuman && HasWitnessEvidenceForAttacker(attackerPeep);
			VerificationLog("PactRetaliation", $"Witness required check result: {hasWitnessEvidence} attackerPeep={attackerPeep.Id.id} attackerPid={attackerPlayer.PID.id} attackerHuman={attackerIsHuman} victimGang={victimGang.PID.id} pact={(pactForVictim?.PactId ?? "none")}");
			if (hasWitnessEvidence || (pactForVictim != null && pactForVictim.IsActive))
			{
				RegisterGangOpsHostileEvent(attackerPlayer.PID.id, victimGang.PID.id, lethal: true, (long)attackerPeep.Id.id);
				HandleImmediatePactRetaliation(attackerPlayer, victimGang, attackerPeep);
			}
			if (!hasWitnessEvidence)
			{
				if (pactForVictim != null && pactForVictim.IsActive)
				{
					VerificationLog("PactRetaliation", $"Immediate retaliation triggered without witness escalation victimGang={victimGang.PID.id} attackerPid={attackerPlayer.PID.id} pact={pactForVictim.DisplayName}");
				}
				return;
			}
			if (!attackerIsHuman)
				return;
			if (!EnableCrewStats.Value)
				return;
			CrewModState state = GetOrCreateCrewState(attackerPeep.Id);
			if (state == null)
				return;
			state.HasWitness = true;
			state.WitnessThreatAttempted = false;
			state.WitnessThreatenedSuccessfully = false;
			state.WitnessCount = Math.Max(1, state.WitnessCount);
			RaiseLocalHeatFloor(attackerPeep.Id, 0.35f);
			string attackerName = attackerPeep.data?.person?.FullName ?? "Unknown";
			string victimName = victimGang?.social?.PlayerGroupName ?? "a rival boss";
			LogGrapevine($"LAW: Boss murder by {attackerName} ({victimName}) raised warrant risk.");
			Debug.Log($"[GameplayTweaks] Boss-murder witness evidence confirmed for {attackerName}");
		}

		private static bool HasWitnessEvidenceForAttacker(Entity attackerPeep)
		{
			if (attackerPeep == null || attackerPeep.Id.IsNotValid)
			{
				return false;
			}
			try
			{
				if (HasActiveImportantWitnessForCrew(attackerPeep.Id))
				{
					return true;
				}
				CrewModState existingState = GetCrewStateOrNull(attackerPeep.Id);
				if (existingState != null && (existingState.HasWitness || existingState.WitnessCount > 0))
				{
					return true;
				}
				if (!EnableCrewStats.Value)
				{
					return false;
				}
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				float witnessChance = 0.3f;
				bool reducedRisk = humanPlayer != null && (IsHumanBoss(attackerPeep, humanPlayer) || IsCaptainCrewMember(attackerPeep));
				if (reducedRisk)
				{
					witnessChance -= 0.1f;
				}
				witnessChance = Mathf.Clamp(witnessChance, 0.05f, 0.9f);
				if (SharedRng.NextDouble() > witnessChance)
				{
					return false;
				}
				CrewModState state = GetOrCreateCrewState(attackerPeep.Id);
				if (state == null)
				{
					return false;
				}
				state.HasWitness = true;
				state.WitnessThreatAttempted = false;
				state.WitnessThreatenedSuccessfully = false;
				state.WitnessCount = Math.Max(1, state.WitnessCount + 1);
				RaiseLocalHeatFloor(attackerPeep.Id, 0.35f);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryGetKilledBossOwner(Entity targetPeep, out PlayerInfo ownerGang)
		{
			ownerGang = null;
			if (targetPeep?.data?.agent == null)
			{
				VerificationLog("PactRetaliation", "Boss detect failed: target has no agent.");
				return false;
			}
			PlayerID? victimPid = targetPeep.data.agent.pid;
			if (!victimPid.HasValue || victimPid.Value.IsNotAnyPlayer)
			{
				VerificationLog("PactRetaliation", "Boss detect failed: victim has no valid player id.");
				return false;
			}
			PlayerInfo victimPlayer = PlayerIDExtensions.FindPlayer(victimPid.Value);
			if (victimPlayer == null || !victimPlayer.IsJustGang || victimPlayer.crew == null || victimPlayer.crew.IsCrewDefeated)
			{
				VerificationLog("PactRetaliation", "Boss detect failed: victim player missing or invalid gang.");
				return false;
			}
			if (victimPlayer.social != null && !victimPlayer.social.PlayerPeepId.IsNotValid && victimPlayer.social.PlayerPeepId == targetPeep.Id)
			{
				ownerGang = victimPlayer;
				VerificationLog("PactRetaliation", $"Boss detected via PlayerPeepId for gang {victimPlayer.PID.id}.");
				return true;
			}
			try
			{
				CrewAssignment boss = victimPlayer.crew.GetCrewForIndex(0);
				if (boss.IsValid && boss.peepId == targetPeep.Id)
				{
					ownerGang = victimPlayer;
					VerificationLog("PactRetaliation", $"Boss detected via crew index fallback for gang {victimPlayer.PID.id}.");
					return true;
				}
			}
			catch
			{
			}
			VerificationLog("PactRetaliation", $"Boss detect failed: peep {targetPeep.Id.id} did not match gang boss identity for gang {victimPlayer.PID.id}.");
			return false;
		}

		private static void PrimeImmediateRetaliationGangOps(PlayerInfo retaliator, PlayerInfo targetGang, Entity targetPeep, string reason)
		{
			if (!EnableAIAlliances.Value || retaliator == null || targetGang == null || retaliator.PID.id == targetGang.PID.id)
			{
				return;
			}
			GangOpsChannel gangOpsChannel = IsGangInActivePact(retaliator.PID.id) ? GangOpsChannel.Pact : GangOpsChannel.Independent;
			if (!IsGangOpsEnabled(gangOpsChannel))
			{
				return;
			}
			PactOpsSettings pactOpsSettings = EnsureGangOpsSettings(gangOpsChannel);
		float desiredHeat = Mathf.Max(GetWarHeatThresholdForCoordAttack(gangOpsChannel), pactOpsSettings.WarHeatKillGain);
			float currentHeat = GetWarHeat(gangOpsChannel, retaliator.PID.id, targetGang.PID.id);
			if (currentHeat < desiredHeat)
			{
				AddWarHeat(gangOpsChannel, retaliator.PID.id, targetGang.PID.id, desiredHeat - currentHeat, reason);
			}
			QueueRevengeIfEligible(gangOpsChannel, retaliator.PID.id, targetGang.PID.id, (long)(targetPeep?.Id.id ?? 0UL), G.GetNow().days, reason);
			VerificationLog($"GangOps.{GetGangOpsChannelTag(gangOpsChannel)}.Revenge", $"boss-retaliation-primed attacker={retaliator.PID.id} defender={targetGang.PID.id} heat={GetWarHeat(gangOpsChannel, retaliator.PID.id, targetGang.PID.id):0.0} reason={reason}");
		}

		private static void HandleImmediatePactRetaliation(PlayerInfo attackerPlayer, PlayerInfo victimGang, Entity attackerPeep)
		{
			if (attackerPlayer == null || victimGang == null || attackerPlayer.PID.id == victimGang.PID.id)
			{
				return;
			}
			if (ArePlayersProtectedByPactAlliance(victimGang, attackerPlayer))
			{
				VerificationLog("PactRetaliation", $"Immediate retaliation skipped alliance-protected victim={victimGang.PID.id} attacker={attackerPlayer.PID.id}");
				return;
			}
			AlliancePact pactForPlayer = GetPactForPlayer(victimGang.PID);
			if (pactForPlayer != null && pactForPlayer.IsActive)
			{
				List<int> pactMemberGangIds = GetPactMemberGangIds(pactForPlayer);
				bool leaderWasKilled = pactForPlayer.LeaderGangId == victimGang.PID.id;
				List<PlayerInfo> list = pactMemberGangIds
					.Select((int id) => G.FindPlayerById(id))
					.Where((PlayerInfo p) => p != null && p.crew != null && !p.crew.IsCrewDefeated && p.PID.id != attackerPlayer.PID.id && p.PID.id != victimGang.PID.id)
					.Distinct()
					.ToList();
				foreach (PlayerInfo item in list)
				{
					ActivateWarBetweenPlayers(item, attackerPlayer);
					PrimeImmediateRetaliationGangOps(item, attackerPlayer, attackerPeep, "boss-kill-immediate");
					TryDispatchRuntimeGangAttack(item, attackerPlayer, Mathf.Clamp(item.crew?.LivingCrewCount ?? 1, 1, 4), "immediate-pact-retaliation", attackerPeep.Id, out _);
					string buffId = leaderWasKilled ? "relbuff-pact-leader-killed" : "relbuff-pact-boss-killed";
					AddMutualRelationshipBuff(item, attackerPlayer, buffId, GetCrewPeepForPlayer(item), GetCrewPeepForPlayer(attackerPlayer, attackerPeep));
					bool forward = RelationshipHasBuff(item, attackerPlayer, buffId);
					bool reverse = RelationshipHasBuff(attackerPlayer, item, buffId);
					VerificationLog("PactRetaliation", $"Immediate buff {buffId} victim={victimGang.PID.id} member={item.PID.id} attacker={attackerPlayer.PID.id} attackerHuman={attackerPlayer.PID.IsHumanPlayer} forward={forward} reverse={reverse}");
				}
				ApplyPactBossDeathRelationshipPenalty(pactForPlayer, pactMemberGangIds, leaderWasKilled, victimGang.PID.id, new PlayerInfo[1] { attackerPlayer });
				if (leaderWasKilled)
				{
					ResetPactVotingStats(pactForPlayer);
				}
				string attackerName = attackerPlayer.PID.IsHumanPlayer ? "you" : GetGangDisplayName(attackerPlayer.PID.id);
				LogGrapevine($"WAR: {pactForPlayer.DisplayName} retaliates immediately against {attackerName} for the killing of {GetGangDisplayName(victimGang.PID.id)}.");
			}
			else
			{
				ActivateWarBetweenPlayers(victimGang, attackerPlayer);
				PrimeImmediateRetaliationGangOps(victimGang, attackerPlayer, attackerPeep, "boss-kill-immediate");
				TryDispatchRuntimeGangAttack(victimGang, attackerPlayer, Mathf.Clamp(victimGang.crew?.LivingCrewCount ?? 1, 1, 3), "immediate-gang-retaliation", attackerPeep.Id, out _);
				AddMutualRelationshipBuff(victimGang, attackerPlayer, "relbuff-gang-death", GetCrewPeepForPlayer(victimGang), GetCrewPeepForPlayer(attackerPlayer, attackerPeep));
				LogGrapevine($"WAR: {GetGangDisplayName(victimGang.PID.id)} is now at war after their boss was killed.");
			}
		}
	}
}
}
