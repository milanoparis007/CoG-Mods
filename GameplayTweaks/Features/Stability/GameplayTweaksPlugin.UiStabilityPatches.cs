using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using System.Reflection;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.Picks;
using Game.UI.Util;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class ConnectionsTabNullFixPatch
	{
		internal static int PatchedMethodCount { get; private set; }

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				PatchedMethodCount = 0;
				Type type = typeof(GameClock).Assembly.GetType("Game.UI.Session.ConnectionsTabSubview");
				if (type == null)
				{
					Debug.LogWarning("[GameplayTweaks] ConnectionsTabNullFixPatch: type not found");
					return;
				}
				string[] array = new string[4] { "CreateFamilyLinks", "RefreshAllCards", "RefreshSubview", "InitializeCard" };
				int num = 0;
				for (int i = 0; i < array.Length; i++)
				{
					string text = array[i];
					MethodInfo method = type.GetMethod(text, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method == null)
					{
						Debug.LogWarning("[GameplayTweaks] ConnectionsTabNullFixPatch missing method: " + text);
						continue;
					}
					HarmonyMethod postfix = null;
					HarmonyMethod prefix = null;
					if (text == "CreateFamilyLinks")
					{
						prefix = new HarmonyMethod(typeof(ConnectionsTabNullFixPatch), nameof(CreateFamilyLinksPrefix), (Type[])null);
						postfix = new HarmonyMethod(typeof(ConnectionsTabNullFixPatch), nameof(CreateFamilyLinksPostfix), (Type[])null);
					}
					else if (text == "InitializeCard")
					{
						prefix = new HarmonyMethod(typeof(ConnectionsTabNullFixPatch), nameof(InitializeCardPrefix), (Type[])null);
					}
					harmony.Patch((MethodBase)method, prefix, postfix, (HarmonyMethod)null, new HarmonyMethod(typeof(ConnectionsTabNullFixPatch), "NullRefFinalizer", (Type[])null), (HarmonyMethod)null);
					num++;
					GameplayTweaksPlugin.VerificationLog("ConnectionsTab", $"null-finalizer applied method={text}");
				}
				PatchedMethodCount = num;
				if (num == 0)
				{
					Debug.LogWarning("[GameplayTweaks] ConnectionsTabNullFixPatch: no methods patched.");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError("[GameplayTweaks] ConnectionsTabNullFixPatch failed: " + ex.Message);
			}
		}

		private static Exception NullRefFinalizer(Exception __exception)
		{
			if (__exception is NullReferenceException)
			{
				return null;
			}
			return __exception;
		}

		private static void CreateFamilyLinksPrefix(object __instance)
		{
			try
			{
				Entity entity = ResolveSubviewEntity(__instance);
				RelationshipList relationships = entity?.Id.IsValid == true
					? global::Game.Game.ctx?.simman?.rels?.GetListOrNull(entity.Id)
					: null;
				if (relationships?.data == null || relationships.data.Count == 0)
				{
					return;
				}

				int removed = relationships.data.RemoveAll(rel => !IsRenderableConnectionRelationship(rel, entity));
				if (removed > 0)
				{
					global::Game.Game.ctx?.events?.EnqueueOnce(Game.Session.SessionEventType.SomeEntityRelationshipChanged);
					GameplayTweaksPlugin.VerificationLog("ConnectionsTab", $"invalid-relationship-targets-pruned peep={entity.Id.id} removed={removed}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ConnectionsTab relationship prune failed: " + ex.Message);
			}
		}

		private static void CreateFamilyLinksPostfix(object __instance, IList __result)
		{
			if (__instance == null || __result == null)
			{
				return;
			}

			int removed = 0;
			for (int i = __result.Count - 1; i >= 0; i--)
			{
				object item = __result[i];
				if (item == null)
				{
					__result.RemoveAt(i);
					removed++;
					continue;
				}

				if (!IsRenderableConnectionCard(item))
				{
					__result.RemoveAt(i);
					removed++;
				}
			}
			if (removed > 0)
			{
				GameplayTweaksPlugin.VerificationLog("ConnectionsTab", $"invalid-card-targets-filtered count={removed}");
			}
			if (!ShouldRevealHumanCrewConnections(__instance))
			{
				return;
			}

			for (int i = 0; i < __result.Count; i++)
			{
				object item = __result[i];
				AccessTools.Field(item.GetType(), "isKnownByHuman")?.SetValue(item, true);
			}
		}

		private static bool InitializeCardPrefix(GameObject card, object data)
		{
			if (IsRenderableConnectionCard(data))
			{
				return true;
			}

			if (card != null)
			{
				card.SetActive(false);
			}
			GameplayTweaksPlugin.VerificationLog("ConnectionsTab", "invalid-card-render-suppressed");
			return false;
		}

		private static bool ShouldRevealHumanCrewConnections(object subviewInstance)
		{
			try
			{
				Entity entity = ResolveSubviewEntity(subviewInstance);
				if (entity == null || entity.data?.agent == null)
				{
					return false;
				}

				if (entity.data.agent.pid.IsHumanPlayer)
				{
					return true;
				}

				PlayerCrew humanCrew = G.GetHumanCrew();
				return humanCrew != null && humanCrew.GetCrewForPeep(entity.Id).IsValid;
			}
			catch
			{
				return false;
			}
		}

		private static Entity ResolveSubviewEntity(object subviewInstance)
		{
			object model = Traverse.Create(subviewInstance).Field("Model").GetValue();
			if (model == null)
			{
				return null;
			}

			FieldInfo fieldInfo = AccessTools.Field(model.GetType(), "entity");
			if (fieldInfo != null)
			{
				Entity entity = fieldInfo.GetValue(model) as Entity;
				if (entity != null)
				{
					return entity;
				}
			}

			PropertyInfo propertyInfo = AccessTools.Property(model.GetType(), "entity");
			return propertyInfo?.GetValue(model, null) as Entity;
		}

		private static bool IsRenderableConnectionCard(object cardData)
		{
			if (cardData == null)
			{
				return false;
			}

			Entity cardPeep = AccessTools.Field(cardData.GetType(), "cardPeep")?.GetValue(cardData) as Entity;
			return IsRenderablePerson(cardPeep);
		}

		private static bool IsRenderableConnectionRelationship(Relationship rel, Entity source)
		{
			if (rel == null || rel.to.IsNotValid)
			{
				return false;
			}

			Entity target = rel.to.FindEntity();
			if (!IsRenderablePerson(target))
			{
				return false;
			}

			return source == null || target.Id != source.Id;
		}

		private static bool IsRenderablePerson(Entity entity)
		{
			PersonData person = entity?.data?.person;
			if (person == null)
			{
				return false;
			}

			if (!entity.Id.IsValid)
			{
				return false;
			}

			string fullName = person.FullName;
			return !string.IsNullOrWhiteSpace(fullName)
				&& !string.Equals(fullName.Trim(), "Name", StringComparison.OrdinalIgnoreCase);
		}
	}

	internal static class SelectionManagerRightClickGuardPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Session.Board.SelectionManager");
				if (type == null)
				{
					Debug.LogWarning("[GameplayTweaks] SelectionManagerRightClickGuardPatch: type not found");
					return;
				}
				MethodInfo method = type.GetMethod("HandleClosePopupOrDeselect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(bool) }, null);
				if (method == null)
				{
					Debug.LogWarning("[GameplayTweaks] SelectionManagerRightClickGuardPatch: HandleClosePopupOrDeselect missing");
					return;
				}
				harmony.Patch((MethodBase)method, new HarmonyMethod(typeof(SelectionManagerRightClickGuardPatch), "HandleClosePopupOrDeselectPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				GameplayTweaksPlugin.VerificationLog("RightClickGuard", "selection manager patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SelectionManagerRightClickGuardPatch failed: " + ex.Message);
			}
		}

		private static bool HandleClosePopupOrDeselectPrefix(bool fromKeyboard)
		{
			if (fromKeyboard)
			{
				return true;
			}
			bool flag = false;
			if (GameplayTweaksPlugin.AnyCrewModMenuVisibleFromExternalUi())
			{
				GameplayTweaksPlugin.ForceCloseCrewMenusFromExternalUi();
				flag = true;
			}
			if (GameplayTweaksPlugin.HideGangOpsPopupFromExternalUi())
			{
				flag = true;
			}
			return !flag;
		}
	}

	internal static class TmpReplacementCharacterSanitizerPatch
	{
		private static readonly HashSet<MethodBase> PatchedSetters = new HashSet<MethodBase>();

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				int patched = 0;
				patched += PatchTextSetter(harmony, typeof(TMP_Text));
				patched += PatchTextSetter(harmony, typeof(TextMeshProUGUI));
				patched += PatchTextSetter(harmony, typeof(TextMeshPro));
				GameplayTweaksPlugin.VerificationLog("UISanitize", $"TMP replacement-character sanitizer patched={patched}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TmpReplacementCharacterSanitizerPatch failed: " + ex.Message);
			}
		}

		private static int PatchTextSetter(Harmony harmony, Type type)
		{
			MethodInfo setter = AccessTools.PropertySetter(type, "text");
			if (setter == null || !PatchedSetters.Add(setter))
			{
				return 0;
			}

			harmony.Patch(setter, prefix: new HarmonyMethod(typeof(TmpReplacementCharacterSanitizerPatch), nameof(TextSetterPrefix)));
			return 1;
		}

		private static void TextSetterPrefix(ref string value)
		{
			if (string.IsNullOrEmpty(value) || value.IndexOf('\uFFFD') < 0)
			{
				return;
			}

			value = value.Replace("\uFFFD", string.Empty);
		}
	}

	internal static class GangVisibilityRevealPatch
	{
		private static MethodInfo _markBuildingAsKnownMethod;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type meetingsType = typeof(GameClock).Assembly.GetType("Game.Session.Player.PlayerMeetings");
				MethodInfo revealAllUnits = meetingsType?.GetMethod("RevealAllUnitsOfPlayer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(PlayerID) }, null);
				if (revealAllUnits != null)
				{
					harmony.Patch(revealAllUnits, postfix: new HarmonyMethod(typeof(GangVisibilityRevealPatch), nameof(RevealAllUnitsOfPlayerPostfix)));
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GangVisibilityRevealPatch failed: " + ex.Message);
			}
		}

		private static void RevealAllUnitsOfPlayerPostfix(PlayerID pid)
		{
			try
			{
				if (!pid.IsValid || pid.IsHumanPlayer)
				{
					return;
				}

				PlayerInfo gang = pid.FindPlayer();
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				EntityID safehouseId = gang?.territory?.Safehouse ?? EntityID.INVALID;
				if (gang == null || humanPlayer?.meetings == null || !safehouseId.IsValid)
				{
					return;
				}
				if (!humanPlayer.meetings.IsPlayerMet(pid))
				{
					VerificationLog("Compat", $"gang-safehouse-reveal-skipped pid={pid.id} reason=not-met source=reveal-units");
					return;
				}

				Node headquartersNode = gang.territory?.GetHeadquartersNode(ignoreWarnings: true);
				if (headquartersNode != null)
				{
					humanPlayer.meetings.MarkNodeAsKnown(headquartersNode, expectedSeen: true, instant: true);
				}

				Entity safehouse = safehouseId.FindEntity();
				if (safehouse == null)
				{
					return;
				}

				if (_markBuildingAsKnownMethod == null || _markBuildingAsKnownMethod.DeclaringType != humanPlayer.meetings.GetType())
				{
					_markBuildingAsKnownMethod = humanPlayer.meetings.GetType().GetMethod("MarkBuildingAsKnown", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Entity), typeof(bool), typeof(bool) }, null);
				}

				_markBuildingAsKnownMethod?.Invoke(humanPlayer.meetings, new object[] { safehouse, true, true });
				VerificationLog("Compat", $"gang-safehouse-revealed pid={pid.id} safehouse={safehouse.Id.id} source=reveal-units");
			}
			catch
			{
			}
		}
	}

	internal static class SelectionFocusDeferredArrestPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type selectionType = typeof(GameClock).Assembly.GetType("Game.Session.Board.SelectionManager");
				Type defaultInputType = typeof(GameClock).Assembly.GetType("Game.Session.Input.DefaultInputMode");
				if (selectionType == null || defaultInputType == null)
				{
					Debug.LogWarning("[GameplayTweaks] SelectionFocusDeferredArrestPatch: type not found");
					return;
				}

				MethodInfo setFocus = selectionType.GetMethod("SetFocus", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[1] { typeof(Entity) }, null);
				MethodInfo processHover = defaultInputType.GetMethod("ProcessHoverOverEntity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2] { typeof(Vector2), typeof(Entity) }, null);
				if (setFocus == null || processHover == null)
				{
					Debug.LogWarning("[GameplayTweaks] SelectionFocusDeferredArrestPatch: required method missing");
					return;
				}

				harmony.Patch((MethodBase)setFocus, new HarmonyMethod(typeof(SelectionFocusDeferredArrestPatch), "SetFocusPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(typeof(SelectionFocusDeferredArrestPatch), "SetFocusFinalizer", (Type[])null), (HarmonyMethod)null);
				harmony.Patch((MethodBase)processHover, new HarmonyMethod(typeof(SelectionFocusDeferredArrestPatch), "ProcessHoverOverEntityPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				GameplayTweaksPlugin.VerificationLog("SelectionFocusGuard", "selection focus deferred-arrest patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SelectionFocusDeferredArrestPatch failed: " + ex.Message);
			}
		}

		private static bool ProcessHoverOverEntityPrefix(Entity e)
		{
			if (!GameplayTweaksPlugin.ShouldSuppressSelectionFocusForDeferredArrest(e, out EntityID vehicleId, out EntityID peepId, out string source))
			{
				return true;
			}

			GameplayTweaksPlugin.VerificationLog("SelectionFocusGuard", $"selection-hover-focus-skipped entity={e?.Id ?? EntityID.INVALID} peep={peepId.id} vehicle={vehicleId.id} source={source}");
			return false;
		}

		private static bool SetFocusPrefix(Entity e)
		{
			if (!GameplayTweaksPlugin.ShouldSuppressSelectionFocusForDeferredArrest(e, out EntityID vehicleId, out EntityID peepId, out string source))
			{
				return true;
			}

			GameplayTweaksPlugin.VerificationLog("SelectionFocusGuard", $"selection-focus-deferred-arrest-suppressed entity={e?.Id ?? EntityID.INVALID} peep={peepId.id} vehicle={vehicleId.id} source={source}");
			return false;
		}

		private static Exception SetFocusFinalizer(Exception __exception, Entity e)
		{
			if (__exception is NullReferenceException)
			{
				GameplayTweaksPlugin.VerificationLog("SelectionFocusGuard", $"selection-focus-null-guard entity={e?.Id ?? EntityID.INVALID}");
				return null;
			}

			return __exception;
		}
	}

	internal static class CrewPickAggroRefreshStabilityPatch
	{
		private static MethodInfo _getContainerMethod;
		private static MethodInfo _removePickMethod;
		private static readonly FieldInfo CrewPickPeepField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_peep");
		private static readonly FieldInfo CrewPickVehicleField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_vehicle");
		private static readonly FieldInfo CrewPickCrewField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_crew");

		private static int _lastRefreshFailureFrame = -1;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type pickManagerType = AccessTools.TypeByName("Game.UI.Session.Picks.PickManager");
				Type pickContainerType = AccessTools.TypeByName("Game.UI.Session.Picks.PickContainer");
				MethodInfo refreshMethod = pickManagerType?.GetMethod("RefreshAllPlayerCrewPicks", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(PlayerID) }, null);
				if (refreshMethod == null)
				{
					Debug.LogWarning("[GameplayTweaks] CrewPickAggroRefreshStabilityPatch: RefreshAllPlayerCrewPicks not found");
					return;
				}

				harmony.Patch(
					refreshMethod,
					finalizer: new HarmonyMethod(typeof(CrewPickAggroRefreshStabilityPatch), nameof(RefreshAllPlayerCrewPicksFinalizer)));
				MethodInfo addOrRefreshMethod = pickContainerType?.GetMethod("AddOrRefreshPick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(PickTarget), typeof(bool) }, null);
				if (addOrRefreshMethod != null)
				{
					harmony.Patch(
						addOrRefreshMethod,
						prefix: new HarmonyMethod(typeof(CrewPickAggroRefreshStabilityPatch), nameof(AddOrRefreshPickPrefix)));
				}
				MethodInfo resetMethod = AccessTools.Method(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "Reset");
				if (resetMethod != null)
				{
					harmony.Patch(
						resetMethod,
						postfix: new HarmonyMethod(typeof(CrewPickAggroRefreshStabilityPatch), nameof(CrewPickResetPostfix)));
				}
				bool delegatedScheduler = GameplayTweaksPlugin.IsAfterProhibitionUiAggroRefreshBridgeAvailableForCompat();
				GameplayTweaksPlugin.VerificationLog(
					"AggroUI",
					delegatedScheduler
						? "crew-pick refresh executor patch applied scheduler=AfterProhibitionUI fallback=GameplayTweaks"
						: "crew-pick refresh stability patch applied scheduler=GameplayTweaks");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CrewPickAggroRefreshStabilityPatch failed: " + ex.Message);
			}
		}

		private static Exception RefreshAllPlayerCrewPicksFinalizer(Exception __exception, object __instance, PlayerID pid)
		{
			try
			{
				if (__exception != null
					&& TryClearCopOrFedCrewPickTargets(__instance, pid, out int removedCount) && removedCount > 0)
				{
					GameplayTweaksPlugin.VerificationLog(
						"AggroUI",
						$"crew-pick-refresh-cleared pid={pid.id} removed={removedCount} exception={(__exception?.GetType().Name ?? "none")}");
				}
			}
			catch (Exception clearEx)
			{
				Debug.LogWarning("[GameplayTweaks] CrewPickAggroRefreshStabilityPatch clear failed: " + clearEx.Message);
			}

			if (__exception == null)
			{
				return null;
			}

			try
			{
				int frame = Time.frameCount;
				int removedCount = TryRepairCrewPickTargets(__instance, pid, out int refreshedCount);
				if (_lastRefreshFailureFrame != frame)
				{
					_lastRefreshFailureFrame = frame;
					GameplayTweaksPlugin.VerificationLog(
						"AggroUI",
						$"crew-pick-refresh-repaired pid={pid.id} exception={__exception.GetType().Name} removed={removedCount} refreshed={refreshedCount} frame={frame}");
				}
			}
			catch (Exception repairEx)
			{
				Debug.LogWarning("[GameplayTweaks] CrewPickAggroRefreshStabilityPatch repair failed: " + repairEx.Message);
			}

			return null;
		}

		private static bool AddOrRefreshPickPrefix(PickContainer __instance, PickTarget t, ref BasePick __result)
		{
			if (__instance == null || __instance.type != PickType.CrewPick)
			{
				return true;
			}

			Entity targetEntity = null;
			try
			{
				targetEntity = t.FindEntity();
			}
			catch
			{
			}

			bool invalidCrewTarget = t.IsNotValid
				|| targetEntity == null
				|| targetEntity.data?.mobile == null
				|| ShouldSuppressCrewPickTargetRefresh(targetEntity);
			if (!invalidCrewTarget)
			{
				return true;
			}

			try
			{
				RemoveCrewPickTargets(__instance, new List<PickTarget> { t });
			}
			catch
			{
			}

			__result = null;
			return false;
		}

		private static int TryRepairCrewPickTargets(object pickManager, PlayerID pid, out int refreshedCount)
		{
			refreshedCount = 0;
			if (pickManager == null)
			{
				return 0;
			}

			PlayerInfo player = pid.FindPlayer();
			HashSet<ulong> validTargetIds = BuildValidCrewPickTargetIds(player);
			HashSet<ulong> refreshTargetIds = BuildRefreshCrewPickTargetIds(player);
			PickContainer crewContainer = GetCrewPickContainer(pickManager);
			if (crewContainer?.picks == null)
			{
				return 0;
			}

			List<PickTarget> staleTargets = new List<PickTarget>();
			List<PickTarget> ownedTargets = new List<PickTarget>();
			foreach (KeyValuePair<PickTarget, BasePick> entry in crewContainer.picks.ToList())
			{
				PickTarget target = entry.Key;
				BasePick pick = entry.Value;
				Entity targetEntity = target.FindEntity();
				PlayerID pickPid = ResolveCrewPickPid(pick, targetEntity);
				bool belongsToRequestedPid = pickPid == pid
					|| (pickPid.IsValid && pickPid.id == pid.id)
					|| (targetEntity?.data?.mobile != null && targetEntity.data.mobile.pid == pid);
				if (belongsToRequestedPid)
				{
					ownedTargets.Add(target);
				}
				bool missingTargetEntity = targetEntity == null || target.IsNotValid;
				bool targetNoLongerTracked = belongsToRequestedPid
					&& (!target.eid.IsValid
						|| !validTargetIds.Contains(target.eid.id)
						|| HasStaleCrewPickBackingTarget(pick, validTargetIds));
				if (!missingTargetEntity && !targetNoLongerTracked)
				{
					continue;
				}

				if (!missingTargetEntity && !belongsToRequestedPid)
				{
					continue;
				}

				staleTargets.Add(target);
			}

			bool clearAllOwnedTargets = player == null || ShouldClearAllOwnedCopOrFedTargets(player, refreshTargetIds);
			List<PickTarget> targetsToRemove = clearAllOwnedTargets
				? ownedTargets.Concat(staleTargets).Distinct().ToList()
				: staleTargets;
			int removedCount = RemoveCrewPickTargets(crewContainer, targetsToRemove);
			if (player == null)
			{
				return removedCount;
			}
			if (clearAllOwnedTargets)
			{
				return removedCount;
			}

			foreach (ulong targetId in refreshTargetIds)
			{
				Entity entity = EntityID.FromID(targetId).FindEntity();
				if (entity == null)
				{
					continue;
				}

				try
				{
					crewContainer.AddOrRefreshPick(new PickTarget(entity.Id), resetExisting: true);
					refreshedCount++;
				}
				catch
				{
				}
			}

			return removedCount;
		}

		internal static bool TryClearCopOrFedCrewPickTargets(object pickManager, PlayerID pid, out int removedCount)
		{
			removedCount = 0;
			PlayerInfo player = pid.FindPlayer();
			HashSet<ulong> refreshTargetIds = BuildRefreshCrewPickTargetIds(player);
			if (player == null || !ShouldClearAllOwnedCopOrFedTargets(player, refreshTargetIds))
			{
				return false;
			}

			PickContainer crewContainer = GetCrewPickContainer(pickManager);
			if (crewContainer?.picks == null)
			{
				return true;
			}

			List<PickTarget> ownedTargets = new List<PickTarget>();
			foreach (KeyValuePair<PickTarget, BasePick> entry in crewContainer.picks.ToList())
			{
				PickTarget target = entry.Key;
				BasePick pick = entry.Value;
				Entity targetEntity = target.FindEntity();
				PlayerID pickPid = ResolveCrewPickPid(pick, targetEntity);
				bool belongsToRequestedPid = pickPid == pid
					|| (pickPid.IsValid && pickPid.id == pid.id)
					|| (targetEntity?.data?.mobile != null && targetEntity.data.mobile.pid == pid);
				if (!belongsToRequestedPid)
				{
					continue;
				}

				ownedTargets.Add(target);
			}

			removedCount = RemoveCrewPickTargets(crewContainer, ownedTargets.Distinct().ToList());
			return true;
		}

		internal static bool TryReconcileCrewPickTargets(object pickManager, PlayerID pid, out int removedCount, out int refreshedCount)
		{
			removedCount = 0;
			refreshedCount = 0;
			try
			{
				removedCount = TryRepairCrewPickTargets(pickManager, pid, out refreshedCount);
				return removedCount > 0 || refreshedCount > 0;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsCopOrFedPlayer(PlayerInfo player)
		{
			if (player == null)
			{
				return false;
			}

			try
			{
				PropertyInfo prop = player.GetType().GetProperty("IsCopOrFed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (prop?.GetValue(player) is bool propValue)
				{
					return propValue;
				}

				FieldInfo field = player.GetType().GetField("IsCopOrFed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field?.GetValue(player) is bool fieldValue)
				{
					return fieldValue;
				}
			}
			catch
			{
			}

			return false;
		}

		private static bool IsJustCopPlayer(PlayerInfo player)
		{
			if (player == null)
			{
				return false;
			}

			try
			{
				PropertyInfo prop = player.GetType().GetProperty("IsJustCop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (prop?.GetValue(player) is bool propValue)
				{
					return propValue;
				}

				FieldInfo field = player.GetType().GetField("IsJustCop", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field?.GetValue(player) is bool fieldValue)
				{
					return fieldValue;
				}
			}
			catch
			{
			}

			return false;
		}

		private static bool IsFederalPlayer(PlayerInfo player)
		{
			return IsCopOrFedPlayer(player) && !IsJustCopPlayer(player);
		}

		private static HashSet<ulong> BuildValidCrewPickTargetIds(PlayerInfo player)
		{
			HashSet<ulong> validTargetIds = new HashSet<ulong>();
			if (player?.crew == null)
			{
				return validTargetIds;
			}

			foreach (CrewAssignment assignment in player.crew.AllCrew)
			{
				if (!assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsNotDead || !player.crew.IsOnBoard(assignment.peepId) || GameplayTweaksPlugin.IsCrewCurrentlyJailed(assignment.peepId))
				{
					continue;
				}

				if (!assignment.IsInVehicle)
				{
					continue;
				}

				Entity vehicle = assignment.GetVehicle();
				EntityID vehicleId = vehicle?.Id ?? assignment.VehicleID;
				if (CanRefreshCrewPickVehicleTarget(vehicleId, vehicle))
				{
					validTargetIds.Add(vehicleId.id);
				}
			}

			if (IsCopOrFedPlayer(player))
			{
				return validTargetIds;
			}

			foreach (EntityID scavengeableCar in player.crew.AllScavengeableCars)
			{
				Entity scavengeableEntity = scavengeableCar.FindEntity();
				if (CanRefreshCrewPickVehicleTarget(scavengeableCar, scavengeableEntity))
				{
					validTargetIds.Add(scavengeableCar.id);
				}
			}

			foreach (EntityID unassignedVehicle in player.crew.AllUnassignedVehicles)
			{
				Entity unassignedEntity = unassignedVehicle.FindEntity();
				if (CanRefreshCrewPickVehicleTarget(unassignedVehicle, unassignedEntity))
				{
					validTargetIds.Add(unassignedVehicle.id);
				}
			}

			return validTargetIds;
		}

		private static HashSet<ulong> BuildRefreshCrewPickTargetIds(PlayerInfo player)
		{
			HashSet<ulong> refreshTargetIds = new HashSet<ulong>();
			if (player?.crew == null)
			{
				return refreshTargetIds;
			}

			foreach (CrewAssignment assignment in player.crew.AllCrew)
			{
				if (!assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsNotDead || !player.crew.IsOnBoard(assignment.peepId) || GameplayTweaksPlugin.IsCrewCurrentlyJailed(assignment.peepId))
				{
					continue;
				}

				if (assignment.IsInVehicle)
				{
					Entity vehicle = assignment.GetVehicle();
					EntityID vehicleId = vehicle?.Id ?? assignment.VehicleID;
					if (CanRefreshCrewPickVehicleTarget(vehicleId, vehicle))
					{
						refreshTargetIds.Add(vehicleId.id);
					}
				}
			}

			if (IsCopOrFedPlayer(player))
			{
				return refreshTargetIds;
			}

			foreach (EntityID scavengeableCar in player.crew.AllScavengeableCars)
			{
				Entity scavengeableEntity = scavengeableCar.FindEntity();
				if (CanRefreshCrewPickVehicleTarget(scavengeableCar, scavengeableEntity))
				{
					refreshTargetIds.Add(scavengeableCar.id);
				}
			}

			foreach (EntityID unassignedVehicle in player.crew.AllUnassignedVehicles)
			{
				Entity unassignedEntity = unassignedVehicle.FindEntity();
				if (CanRefreshCrewPickVehicleTarget(unassignedVehicle, unassignedEntity))
				{
					refreshTargetIds.Add(unassignedVehicle.id);
				}
			}

			return refreshTargetIds;
		}

		private static bool CanRefreshCrewPickVehicleTarget(EntityID vehicleId, Entity entity)
		{
			if (!vehicleId.IsValid || entity?.data?.mobile == null)
			{
				return false;
			}

			return !ShouldSuppressCrewPickTargetRefresh(entity);
		}

		private static bool ShouldSuppressCrewPickTargetRefresh(Entity entity)
		{
			if (entity == null || !entity.Id.IsValid)
			{
				return false;
			}

			try
			{
				if (IsUnmetAiCrewPickTarget(entity))
				{
					return true;
				}
				return MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(entity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state)
					&& !state.HasInspectableOccupants;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsUnmetAiCrewPickTarget(Entity entity)
		{
			try
			{
				PlayerID pid = entity?.data?.mobile?.pid ?? PlayerID.INVALID;
				if (!pid.IsValid || pid.IsHumanPlayer)
				{
					return false;
				}
				PlayerInfo human = G.GetHumanPlayer();
				return human?.meetings != null && !human.meetings.IsPlayerMet(pid);
			}
			catch
			{
				return false;
			}
		}

		private static bool ShouldClearAllOwnedCopOrFedTargets(PlayerInfo player, HashSet<ulong> refreshTargetIds)
		{
			if (player == null)
			{
				return true;
			}

			if (!IsCopOrFedPlayer(player) && !IsJustCopPlayer(player))
			{
				return false;
			}

			return refreshTargetIds == null || refreshTargetIds.Count <= 0;
		}

		private static PickContainer GetCrewPickContainer(object pickManager)
		{
			try
			{
				if (_getContainerMethod == null || _getContainerMethod.DeclaringType != pickManager.GetType())
				{
					_getContainerMethod = pickManager.GetType().GetMethod("GetContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(PickType) }, null);
				}

				return _getContainerMethod?.Invoke(pickManager, new object[] { PickType.CrewPick }) as PickContainer;
			}
			catch
			{
				return null;
			}
		}

		private static PlayerID ResolveCrewPickPid(BasePick pick, Entity targetEntity)
		{
			if (pick != null)
			{
				try
				{
					FieldInfo pidField = pick.GetType().GetField("_pid", BindingFlags.Instance | BindingFlags.NonPublic);
					if (pidField?.GetValue(pick) is PlayerID pid && pid.IsValid)
					{
						return pid;
					}
				}
				catch
				{
				}
			}

			try
			{
				if (pick != null)
				{
					if (CrewPickCrewField?.GetValue(pick) is CrewAssignment assignment && assignment.IsValid)
					{
						Entity vehicle = assignment.GetVehicle();
						if (vehicle?.data?.mobile != null)
						{
							return vehicle.data.mobile.pid;
						}

						Entity peep = assignment.GetPeep();
						if (peep?.data?.agent != null)
						{
							return peep.data.agent.pid;
						}
					}

					if (CrewPickVehicleField?.GetValue(pick) is Entity pickVehicle && pickVehicle.data?.mobile != null)
					{
						return pickVehicle.data.mobile.pid;
					}

					if (CrewPickPeepField?.GetValue(pick) is Entity pickPeep && pickPeep.data?.agent != null)
					{
						return pickPeep.data.agent.pid;
					}
				}
			}
			catch
			{
			}

			try
			{
				if (targetEntity?.data?.mobile != null)
				{
					return targetEntity.data.mobile.pid;
				}
			}
			catch
			{
			}

			return PlayerID.INVALID;
		}

		private static bool HasStaleCrewPickBackingTarget(BasePick pick, HashSet<ulong> validTargetIds)
		{
			if (pick == null || validTargetIds == null || validTargetIds.Count == 0)
			{
				return false;
			}

			try
			{
				List<EntityID> backingIds = new List<EntityID>();
				if (CrewPickCrewField?.GetValue(pick) is CrewAssignment assignment && assignment.IsValid)
				{
					if (assignment.IsInVehicle)
					{
						Entity assignmentVehicle = assignment.GetVehicle();
						EntityID vehicleId = assignmentVehicle?.Id ?? assignment.VehicleID;
						if (vehicleId.IsValid)
						{
							backingIds.Add(vehicleId);
						}
					}
					else if (assignment.peepId.IsValid)
					{
						backingIds.Add(assignment.peepId);
					}
				}

				if (backingIds.Count == 0 && CrewPickVehicleField?.GetValue(pick) is Entity vehicle && vehicle.Id.IsValid)
				{
					backingIds.Add(vehicle.Id);
				}

				if (backingIds.Count == 0 && CrewPickPeepField?.GetValue(pick) is Entity peep && peep.Id.IsValid)
				{
					backingIds.Add(peep.Id);
				}

				bool foundExpectedLiveTarget = false;
				foreach (EntityID backingId in backingIds.Distinct())
				{
					if (backingId.IsValid && validTargetIds.Contains(backingId.id))
					{
						foundExpectedLiveTarget = true;
						break;
					}
				}

				return backingIds.Count > 0 && !foundExpectedLiveTarget;
			}
			catch
			{
			}

			return false;
		}

		private static int RemoveCrewPickTargets(PickContainer container, List<PickTarget> staleTargets)
		{
			if (container == null || staleTargets == null || staleTargets.Count == 0)
			{
				return 0;
			}

			try
			{
				if (_removePickMethod == null || _removePickMethod.DeclaringType != container.GetType())
				{
					_removePickMethod = container.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(PickTarget) }, null);
				}

				if (_removePickMethod == null)
				{
					return 0;
				}

				int removedCount = 0;
				foreach (PickTarget target in staleTargets)
				{
					try
					{
						bool removed = false;
						if (_removePickMethod.Invoke(container, new object[] { target }) is bool invokeRemoved && invokeRemoved)
						{
							removed = true;
						}
						else if (container.picks != null && container.picks.TryGetValue(target, out BasePick fallbackPick) && container.picks.Remove(target))
						{
							if (fallbackPick != null)
							{
								ClearCrewPickVisuals(fallbackPick.go);
								try
								{
									container.pool?.Free(fallbackPick);
								}
								catch
								{
									try
									{
										fallbackPick.Reset();
									}
									catch
									{
									}

									try
									{
										if (fallbackPick.go != null)
										{
											fallbackPick.go.SetActive(false);
										}
									}
									catch
									{
									}
								}
							}
							removed = true;
						}

						if (removed)
						{
							removedCount++;
						}
					}
					catch
					{
						try
						{
							if (container.picks != null && container.picks.Remove(target))
							{
								removedCount++;
							}
						}
						catch
						{
						}
					}
				}

				return removedCount;
			}
			catch
			{
				return 0;
			}
		}

		internal static int ForceClearCrewPickTargets(object pickManager, params EntityID[] targetIds)
		{
			if (pickManager == null || targetIds == null || targetIds.Length == 0)
			{
				return 0;
			}

			try
			{
				PickContainer crewContainer = GetCrewPickContainer(pickManager);
				if (crewContainer?.gorect == null)
				{
					return 0;
				}

				HashSet<ulong> desiredTargetIds = new HashSet<ulong>(targetIds.Where(id => id.IsValid).Select(id => id.id));
				if (desiredTargetIds.Count == 0)
				{
					return 0;
				}

				int clearedCount = 0;
				HashSet<BasePick> processedPicks = new HashSet<BasePick>();
				if (crewContainer.picks != null)
				{
					foreach (KeyValuePair<PickTarget, BasePick> entry in crewContainer.picks.ToList())
					{
						BasePick pick = entry.Value;
						if (!DoesCrewPickMatchTargetIds(entry.Key, pick, desiredTargetIds))
						{
							continue;
						}

						if (pick != null)
						{
							processedPicks.Add(pick);
						}

						bool removed = false;
						try
						{
							if (_removePickMethod == null || _removePickMethod.DeclaringType != crewContainer.GetType())
							{
								_removePickMethod = crewContainer.GetType().GetMethod("Remove", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(PickTarget) }, null);
							}

							if (_removePickMethod?.Invoke(crewContainer, new object[] { entry.Key }) is bool invokeRemoved && invokeRemoved)
							{
								removed = true;
							}
						}
						catch
						{
						}

						if (!removed && crewContainer.picks.Remove(entry.Key))
						{
							ClearCrewPickVisuals(pick?.go);
							try
							{
								crewContainer.pool?.Free(pick);
							}
							catch
							{
								try
								{
									pick?.Reset();
								}
								catch
								{
								}

								try
								{
									if (pick?.go != null)
									{
										pick.go.SetActive(false);
									}
								}
								catch
								{
								}
							}

							removed = true;
						}

						if (removed)
						{
							clearedCount++;
						}
					}
				}

				try
				{
					PickContext[] contexts = crewContainer.gorect.GetComponentsInChildren<PickContext>(includeInactive: true);
					foreach (PickContext context in contexts)
					{
						BasePick pick = context?.pick;
						if (pick == null || processedPicks.Contains(pick))
						{
							continue;
						}

						if (!DoesCrewPickMatchTargetIds(default, pick, desiredTargetIds))
						{
							continue;
						}

						processedPicks.Add(pick);
						ClearCrewPickVisuals(pick.go);
						try
						{
							pick.Reset();
						}
						catch
						{
						}

						try
						{
							if (pick.go != null)
							{
								pick.go.SetActive(false);
							}
						}
						catch
						{
						}

						clearedCount++;
					}
				}
				catch
				{
				}

				return clearedCount;
			}
			catch
			{
				return 0;
			}
		}

		private static bool DoesCrewPickMatchTargetIds(PickTarget target, BasePick pick, HashSet<ulong> desiredTargetIds)
		{
			if (pick == null || desiredTargetIds == null || desiredTargetIds.Count == 0)
			{
				return false;
			}

			try
			{
				if (target.eid.IsValid && desiredTargetIds.Contains(target.eid.id))
				{
					return true;
				}
			}
			catch
			{
			}

			try
			{
				if (CrewPickVehicleField?.GetValue(pick) is Entity vehicle && vehicle.Id.IsValid && desiredTargetIds.Contains(vehicle.Id.id))
				{
					return true;
				}
			}
			catch
			{
			}

			try
			{
				if (CrewPickPeepField?.GetValue(pick) is Entity peep && peep.Id.IsValid && desiredTargetIds.Contains(peep.Id.id))
				{
					return true;
				}
			}
			catch
			{
			}

			try
			{
				if (CrewPickCrewField?.GetValue(pick) is CrewAssignment assignment)
				{
					if (assignment.peepId.IsValid && desiredTargetIds.Contains(assignment.peepId.id))
					{
						return true;
					}

					if (assignment.VehicleID.IsValid && desiredTargetIds.Contains(assignment.VehicleID.id))
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

		private static void CrewPickResetPostfix(BasePick __instance)
		{
			ClearCrewPickVisuals(__instance?.go);
		}

		private static void ClearCrewPickVisuals(GameObject pickGo)
		{
			if (pickGo == null)
			{
				return;
			}

			try
			{
				Image portraitImage = pickGo.transform.Find("Button/Portrait")?.GetComponent<Image>();
				if (portraitImage != null)
				{
					portraitImage.sprite = null;
				}
			}
			catch
			{
			}

			try
			{
				Text text = pickGo.transform.Find("Button/Text")?.GetComponent<Text>();
				if (text != null)
				{
					text.text = string.Empty;
				}
			}
			catch
			{
			}

			try
			{
				Transform warnBorder = pickGo.transform.Find("Warn Border");
				if (warnBorder != null)
				{
					warnBorder.gameObject.SetActive(false);
				}
			}
			catch
			{
			}

			try
			{
				Transform barPanel = pickGo.transform.Find("Bar Panel");
				if (barPanel != null)
				{
					barPanel.gameObject.SetActive(false);
				}
			}
			catch
			{
			}

			try
			{
				Transform stateIcon = pickGo.transform.Find("State Icon");
				if (stateIcon != null)
				{
					stateIcon.gameObject.SetActive(false);
				}
			}
			catch
			{
			}
		}
	}

	internal static class CrewSidebarJailBarsPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo refreshPanel = AccessTools.Method(typeof(CrewCardContext), "RefreshPanel");
				if (refreshPanel == null)
				{
					Debug.LogWarning("[GameplayTweaks] CrewSidebarJailBarsPatch: RefreshPanel not found");
					return;
				}

				harmony.Patch(refreshPanel, postfix: new HarmonyMethod(typeof(CrewSidebarJailBarsPatch), nameof(RefreshPanelPostfix)));
				VerificationLog("Jail", "crew sidebar jail-bars patch applied");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CrewSidebarJailBarsPatch failed: " + ex.Message);
			}
		}

		private static void RefreshPanelPostfix(CrewCardContext __instance)
		{
			try
			{
				if (__instance == null || __instance.data == null)
				{
					return;
				}

				CrewAssignment crew = __instance.data.crew;
				if (!crew.peepId.IsValid || !IsCrewCurrentlyJailed(crew.peepId))
				{
					return;
				}

				Transform bars = __instance.card?.transform.Find("Info/Panel/First/Bars");
				if (bars != null)
				{
					bars.gameObject.SetActive(true);
				}
			}
			catch
			{
			}
		}
	}

	internal static class PactColorUiPatch
	{
		private static readonly FieldInfo CrewPickPidField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_pid");
		private static readonly FieldInfo CrewPickPeepField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_peep");
		private static readonly FieldInfo CrewPickPlayerColorField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_playerColor");
		private static MethodInfo _getCrewPickContainerMethod;
		private static int _lastCrewPickColorLogDay = -1;
		private static int _lastTextColorLogDay = -1;
		private static int _lastFullCrewPickRefreshDeferredFrame = -1;
		private static bool _pendingFullCrewPickRefresh;
		private static int _pendingFullCrewPickRefreshEarliestFrame = -1;
		private static int _pendingFullCrewPickRefreshRemainingPasses;
		private static int _pendingFullCrewPickRefreshCursor;
		private static string _pendingFullCrewPickRefreshSource = string.Empty;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type mapDisplayType = typeof(GameClock).Assembly.GetType("Game.Session.Board.MapDisplayManager");
				MethodInfo getColorForPlayerText = mapDisplayType?.GetMethod("GetColorForPlayerText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(PlayerInfo), typeof(float) }, null);
				if (getColorForPlayerText != null)
				{
					harmony.Patch(getColorForPlayerText, null, new HarmonyMethod(typeof(PactColorUiPatch), nameof(GetColorForPlayerTextPostfix)));
				}

				MethodInfo crewPickRefresh = AccessTools.Method(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "RefreshContents");
				if (crewPickRefresh != null)
				{
					harmony.Patch(crewPickRefresh, null, new HarmonyMethod(typeof(PactColorUiPatch), nameof(CrewPickRefreshContentsPostfix)));
				}

				VerificationLog("PactColorUI", $"patch applied text={(getColorForPlayerText != null)} crewPick={(crewPickRefresh != null)}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PactColorUiPatch failed: " + ex.Message);
			}
		}

		internal static void RequestFullCrewPickRefresh(string sourceTag, int delayFrames = 0)
		{
			if (ShouldSkipFullCrewPickRefresh(sourceTag))
			{
				return;
			}

			int requestedPasses = GetRequestedFullCrewPickRefreshPasses(sourceTag);
			int requestedEarliestFrame = Time.frameCount + Mathf.Max(0, delayFrames);
			string normalizedSource = string.IsNullOrEmpty(sourceTag) ? "unknown" : sourceTag;
			if (!_pendingFullCrewPickRefresh || !string.Equals(_pendingFullCrewPickRefreshSource, normalizedSource, StringComparison.Ordinal))
			{
				_pendingFullCrewPickRefreshCursor = 0;
			}
			_pendingFullCrewPickRefresh = true;
			if (_pendingFullCrewPickRefreshEarliestFrame < 0)
			{
				_pendingFullCrewPickRefreshEarliestFrame = requestedEarliestFrame;
			}
			else
			{
				_pendingFullCrewPickRefreshEarliestFrame = Mathf.Min(_pendingFullCrewPickRefreshEarliestFrame, requestedEarliestFrame);
			}
			_pendingFullCrewPickRefreshRemainingPasses = Mathf.Max(_pendingFullCrewPickRefreshRemainingPasses, requestedPasses);
			_pendingFullCrewPickRefreshSource = normalizedSource;
		}

		private static bool ShouldSkipFullCrewPickRefresh(string sourceTag)
		{
			if (!string.Equals(sourceTag, "human-turn", StringComparison.Ordinal))
			{
				return false;
			}

			try
			{
				return SaveData?.Pacts == null || !SaveData.Pacts.Any(pact => pact != null && pact.IsActive);
			}
			catch
			{
				return false;
			}
		}

		private static int GetRequestedFullCrewPickRefreshPasses(string sourceTag)
		{
			switch (sourceTag)
			{
			case "load-v2":
			case "load-legacy":
				return 4;
			case "refresh-pact-cache":
				return 3;
			case "human-turn":
			case "vehicle-travel":
				return 1;
			default:
				return 2;
			}
		}

		internal static void FlushPendingFullCrewPickRefresh(string sourceTag)
		{
			if (!_pendingFullCrewPickRefresh)
			{
				return;
			}

			if (Time.frameCount < _pendingFullCrewPickRefreshEarliestFrame)
			{
				return;
			}

			if (ShouldDeferFullCrewPickRefresh(out string deferReason))
			{
				_pendingFullCrewPickRefreshEarliestFrame = Time.frameCount + 1;
				if (_lastFullCrewPickRefreshDeferredFrame != Time.frameCount)
				{
					_lastFullCrewPickRefreshDeferredFrame = Time.frameCount;
					VerificationLog("PactColorUI", $"full crew-pick refresh deferred source={_pendingFullCrewPickRefreshSource}->{sourceTag} reason={deferReason}");
				}
				return;
			}

			if (!TryResolveHudPickManager(out object pickManager))
			{
				return;
			}

			PickContainer crewContainer = GetCrewPickContainer(pickManager);
			if (crewContainer == null)
			{
				return;
			}

			int refreshed = 0;
			try
			{
				List<EntityID> refreshTargets = CollectVisibleCrewPickRefreshTargets(crewContainer);
				if (refreshTargets.Count == 0)
				{
					ClearPendingFullCrewPickRefresh();
					VerificationLog("PactColorUI", $"full crew-pick refresh skipped source={_pendingFullCrewPickRefreshSource}->{sourceTag} reason=no-live-targets");
					return;
				}

				int maxThisFlush = GetFullCrewPickRefreshMaxTargetsPerFlush(_pendingFullCrewPickRefreshSource);
				int startIndex = Mathf.Clamp(_pendingFullCrewPickRefreshCursor, 0, refreshTargets.Count);
				for (int index = startIndex; index < refreshTargets.Count && refreshed < maxThisFlush; index++)
				{
					try
					{
						EntityID targetId = refreshTargets[index];
						crewContainer.AddOrRefreshPick(new PickTarget(targetId), resetExisting: true);
						refreshed++;
					}
					catch
					{
					}
				}

				_pendingFullCrewPickRefreshCursor = startIndex + refreshed;
				if (_pendingFullCrewPickRefreshCursor < refreshTargets.Count)
				{
					_pendingFullCrewPickRefreshEarliestFrame = Time.frameCount + 1;
					VerificationLog("PactColorUI", $"full crew-pick refresh slice count={refreshed} cursor={_pendingFullCrewPickRefreshCursor}/{refreshTargets.Count} source={_pendingFullCrewPickRefreshSource}->{sourceTag}");
					return;
				}

				_pendingFullCrewPickRefreshCursor = 0;
				AdvancePendingFullCrewPickRefresh(refreshed);
				VerificationLog("PactColorUI", $"full crew-pick refresh count={refreshed} remaining={_pendingFullCrewPickRefreshRemainingPasses} source={_pendingFullCrewPickRefreshSource}->{sourceTag}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PactColorUiPatch full refresh failed: " + ex.Message);
			}
		}

		private static int GetFullCrewPickRefreshMaxTargetsPerFlush(string sourceTag)
		{
			if (IsLoadFullCrewPickRefreshSource(sourceTag))
			{
				return 12;
			}

			return 6;
		}

		private static bool ShouldDeferFullCrewPickRefresh(out string reason)
		{
			reason = null;
			if (Input.GetMouseButton(0))
			{
				reason = "mouse-held";
				return true;
			}

			if (InputFieldBlocker.ShouldBlockKeyboard)
			{
				reason = "keyboard-blocked";
				return true;
			}

			try
			{
				EventSystem current = EventSystem.current;
				GameObject currentSelectedGameObject = current?.currentSelectedGameObject;
				if (currentSelectedGameObject != null)
				{
					if (currentSelectedGameObject.GetComponent<InputField>() != null)
					{
						reason = "legacy-input-focused";
						return true;
					}

					if (currentSelectedGameObject.GetComponent<TMP_InputField>() != null)
					{
						reason = "tmp-input-focused";
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private static List<EntityID> CollectVisibleCrewPickRefreshTargets(PickContainer crewContainer)
		{
			List<EntityID> targets = new List<EntityID>();
			if (crewContainer?.picks == null || crewContainer.picks.Count == 0)
			{
				return targets;
			}

			HashSet<ulong> seenTargetIds = new HashSet<ulong>();
			foreach (KeyValuePair<PickTarget, BasePick> entry in crewContainer.picks.ToList())
			{
				EntityID targetId = entry.Key.eid;
				Entity targetEntity = entry.Key.FindEntity();
				if ((!targetId.IsValid || targetEntity == null) && entry.Value != null)
				{
					try
					{
						if (CrewPickPeepField?.GetValue(entry.Value) is Entity pickPeep && pickPeep?.Id.IsValid == true)
						{
							targetEntity = pickPeep;
							targetId = pickPeep.Id;
						}
					}
					catch
					{
					}
				}

				if (!targetId.IsValid || targetEntity?.data?.mobile == null || !seenTargetIds.Add(targetId.id))
				{
					continue;
				}

				if (ShouldSuppressVisibleCrewPickTargetRefresh(targetEntity))
				{
					continue;
				}

				targets.Add(targetId);
			}

			return targets;
		}

		private static void AdvancePendingFullCrewPickRefresh(int refreshedCount)
		{
			if (refreshedCount <= 0)
			{
				ClearPendingFullCrewPickRefresh();
				return;
			}

			if (IsLoadFullCrewPickRefreshSource(_pendingFullCrewPickRefreshSource))
			{
				_pendingFullCrewPickRefreshRemainingPasses = Math.Min(1, Math.Max(0, _pendingFullCrewPickRefreshRemainingPasses - 1));
				if (_pendingFullCrewPickRefreshRemainingPasses > 0)
				{
					_pendingFullCrewPickRefreshEarliestFrame = Time.frameCount + 20;
					return;
				}
			}

			ClearPendingFullCrewPickRefresh();
		}

		private static bool ShouldSuppressVisibleCrewPickTargetRefresh(Entity entity)
		{
			if (entity == null || !entity.Id.IsValid)
			{
				return false;
			}

			try
			{
				return MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(entity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state)
					&& !state.HasInspectableOccupants;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsLoadFullCrewPickRefreshSource(string sourceTag)
		{
			return string.Equals(sourceTag, "load-v2", StringComparison.Ordinal)
				|| string.Equals(sourceTag, "load-legacy", StringComparison.Ordinal);
		}

		private static void ClearPendingFullCrewPickRefresh()
		{
			_pendingFullCrewPickRefresh = false;
			_pendingFullCrewPickRefreshEarliestFrame = -1;
			_pendingFullCrewPickRefreshRemainingPasses = 0;
			_pendingFullCrewPickRefreshCursor = 0;
			_pendingFullCrewPickRefreshSource = string.Empty;
		}

		private static PickContainer GetCrewPickContainer(object pickManager)
		{
			try
			{
				if (_getCrewPickContainerMethod == null || _getCrewPickContainerMethod.DeclaringType != pickManager.GetType())
				{
					_getCrewPickContainerMethod = pickManager.GetType().GetMethod("GetContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(PickType) }, null);
				}

				return _getCrewPickContainerMethod?.Invoke(pickManager, new object[] { PickType.CrewPick }) as PickContainer;
			}
			catch
			{
				return null;
			}
		}

		private static void GetColorForPlayerTextPostfix(PlayerInfo player, float brightness, ref Color __result)
		{
			if (!TryGetPactSharedColor(player, out Color pactColor))
			{
				return;
			}

			__result = GetEffectivePlayerTextColor(player, brightness, __result);

			int days = G.GetNow().days;
			if (_lastTextColorLogDay != days)
			{
				_lastTextColorLogDay = days;
				VerificationLog("PactColorUI", $"text color bridged pid={player.PID.id} brightness={brightness:0.##}");
			}
		}

		private static void CrewPickRefreshContentsPostfix(BasePick __instance)
		{
			if (__instance?.go == null || CrewPickPidField == null)
			{
				return;
			}

			PlayerID pid;
			try
			{
				object value = CrewPickPidField.GetValue(__instance);
				if (!(value is PlayerID playerId) || playerId.IsNotValid)
				{
					return;
				}
				pid = playerId;
			}
			catch
			{
				return;
			}

			PlayerInfo player = pid.FindPlayer();
			bool hasPactColor = TryGetPactSharedColor(player, out Color pactColor);
			bool hasVisiblePeep = false;
			bool shouldKeepBasePlayerColor = pid.IsHumanPlayer && !hasPactColor;
			Color basePlayerColor = Color.white;

			try
			{
				if (CrewPickPlayerColorField?.GetValue(__instance) is Color currentColor)
				{
					basePlayerColor = currentColor;
				}
			}
			catch
			{
			}

			try
			{
				if (CrewPickPeepField?.GetValue(__instance) is Entity peep && peep != null)
				{
					hasVisiblePeep = true;
				}
			}
			catch
			{
			}

			try
			{
				Image portrait = __instance.go.transform.Find("Button/Portrait")?.GetComponent<Image>();
				if (portrait != null)
				{
					portrait.color = (!hasVisiblePeep && hasPactColor) ? pactColor : Color.white;
				}
			}
			catch
			{
			}

			if (shouldKeepBasePlayerColor)
			{
				try
				{
					Image overlay = __instance.go.transform.Find("Button/Color Overlay")?.GetComponent<Image>();
					if (overlay != null)
					{
						overlay.color = basePlayerColor;
					}
				}
				catch
				{
				}

				try
				{
					Image frameMask = __instance.go.transform.Find("Frame Gang/Mask")?.GetComponent<Image>();
					if (frameMask != null)
					{
						frameMask.color = new Color(basePlayerColor.r, basePlayerColor.g, basePlayerColor.b, 0.5f);
					}
				}
				catch
				{
				}

				return;
			}

			if (!hasPactColor)
			{
				return;
			}

			try
			{
				CrewPickPlayerColorField?.SetValue(__instance, pactColor);
			}
			catch
			{
			}

			try
			{
				Image overlay = __instance.go.transform.Find("Button/Color Overlay")?.GetComponent<Image>();
				if (overlay != null)
				{
					overlay.color = pactColor;
				}
			}
			catch
			{
			}

			try
			{
				Image frameMask = __instance.go.transform.Find("Frame Gang/Mask")?.GetComponent<Image>();
				if (frameMask != null)
				{
					frameMask.color = new Color(pactColor.r, pactColor.g, pactColor.b, 0.5f);
				}
			}
			catch
			{
			}

			int days = G.GetNow().days;
			if (_lastCrewPickColorLogDay != days)
			{
				_lastCrewPickColorLogDay = days;
				VerificationLog("PactColorUI", $"crew pick recolored pid={pid.id}");
			}
		}
	}

	internal static class ConvoNullFixPatch
	{
		private static int _insideTerritoryMissingNodeCount;

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				TryPatchInsideTerritoryRequirement(harmony);
				TryPatchDoesPassFinalizer(harmony, "Game.Session.Data.CheckHasSponsoredPoliticianInWard");
				TryPatchDoesPassFinalizer(harmony, "Game.Session.Data.CheckCrewRole");
				TryPatchDoesPassFinalizer(harmony, "Game.Session.Data.CheckCanBoostForGoon");
				TryPatchDoesPassFinalizer(harmony, "Game.Session.Data.CheckConvoQuery");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] ConvoNullFixPatch failed: {arg}");
			}
		}

		private static void TryPatchInsideTerritoryRequirement(Harmony harmony)
		{
			MethodInfo method = typeof(CheckIsConvoInsideTerritory).GetMethod("DoesPass", BindingFlags.Instance | BindingFlags.Public);
			if (method == null)
			{
				return;
			}
			harmony.Patch(
				(MethodBase)method,
				new HarmonyMethod(typeof(ConvoNullFixPatch), nameof(CheckIsConvoInsideTerritoryPrefix)),
				(HarmonyMethod)null,
				(HarmonyMethod)null,
				(HarmonyMethod)null,
				(HarmonyMethod)null);
			Debug.Log("[GameplayTweaks] ConvoNullFixPatch prefix applied to Game.Session.Data.CheckIsConvoInsideTerritory.DoesPass");
		}

		private static bool CheckIsConvoInsideTerritoryPrefix(CheckIsConvoInsideTerritory __instance, VisitState visit, ref bool __result)
		{
			try
			{
				PlayerID ownerPid = ResolveConvoTerritoryOwner(visit, out bool missingNode);
				bool flag = false;
				switch (__instance.player)
				{
				case CheckIsConvoInsideTerritory.Type.Human:
					flag = ownerPid.IsHumanPlayer;
					break;
				case CheckIsConvoInsideTerritory.Type.AI:
					flag = ownerPid.IsAIPlayer;
					break;
				case CheckIsConvoInsideTerritory.Type.Gang:
					flag = ownerPid.IsAIPlayer && ownerPid.FindPlayer()?.IsJustGang == true;
					break;
				case CheckIsConvoInsideTerritory.Type.Goon:
					flag = ownerPid.IsAIPlayer && ownerPid.FindPlayer()?.IsJustGoon == true;
					break;
				case CheckIsConvoInsideTerritory.Type.Any:
					flag = ownerPid.IsAnyPlayer;
					break;
				case CheckIsConvoInsideTerritory.Type.None:
					flag = ownerPid.IsNotValid;
					break;
				default:
					Debug.LogWarning("[GameplayTweaks] Unknown CheckIsConvoInsideTerritory player type: " + __instance.player);
					break;
				}

				__result = flag == __instance.expected;
				if (missingNode)
				{
					_insideTerritoryMissingNodeCount++;
					if (_insideTerritoryMissingNodeCount == 1 || _insideTerritoryMissingNodeCount % 25 == 0)
					{
						VerificationLog(
							"ConvoNullFix",
							$"inside-territory-missing-node player={__instance.player} expected={__instance.expected} result={__result} count={_insideTerritoryMissingNodeCount}");
					}
				}
				return false;
			}
			catch (Exception ex)
			{
				__result = false;
				VerificationLog("ConvoNullFix", $"inside-territory-safe-fallback exception={ex.GetType().Name} message={ex.Message}");
				return false;
			}
		}

		private static PlayerID ResolveConvoTerritoryOwner(VisitState visit, out bool missingNode)
		{
			missingNode = true;
			try
			{
				var node = visit?.GetBldgNode();
				if (node == null)
				{
					return PlayerID.INVALID;
				}
				missingNode = false;
				return node.owner.Get();
			}
			catch
			{
				return PlayerID.INVALID;
			}
		}

		private static void TryPatchDoesPassFinalizer(Harmony harmony, string typeName)
		{
			Type type = typeof(GameClock).Assembly.GetType(typeName);
			if (type == null)
			{
				return;
			}
			MethodInfo method = type.GetMethod("DoesPass", BindingFlags.Instance | BindingFlags.Public);
			if (method == null)
			{
				return;
			}
			MethodInfo finalizer = typeof(ConvoNullFixPatch).GetMethod("DoesPassFinalizer", BindingFlags.Static | BindingFlags.NonPublic);
			harmony.Patch((MethodBase)method, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(finalizer), (HarmonyMethod)null);
			Debug.Log("[GameplayTweaks] ConvoNullFixPatch finalizer applied to " + typeName + ".DoesPass");
		}

		private static Exception DoesPassFinalizer(Exception __exception, ref bool __result)
		{
			if (__exception != null)
			{
				__result = false;
				return null;
			}
			return null;
		}
	}

	internal static class GangTradeConvoStateFixPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo method = typeof(global::Game.UI.Session.Convo.ConversationController).GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public);
				if (method == null)
				{
					Debug.LogWarning("[GameplayTweaks] GangTradeConvoStateFixPatch: Initialize not found");
					return;
				}
				harmony.Patch(method, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(InitializePostfix)), null, null, null);
				MethodInfo processOnClick = typeof(ConvoCallbacks).GetMethod("ProcessOnClick", BindingFlags.Instance | BindingFlags.Public);
				if (processOnClick != null)
				{
					harmony.Patch(processOnClick, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ProcessOnClickPrefix)), null, null, null, null);
				}
				MethodInfo buyTruce = typeof(ConvoCallbacks).GetMethod("ExecuteGangConvoBuyTruce", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (buyTruce != null)
				{
					harmony.Patch(buyTruce, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGangConvoBuyTrucePostfix)), null, null, null);
				}
				MethodInfo initiativeAgree = typeof(ConvoCallbacks).GetMethod("ExecuteGangConvoInitiativeAgree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (initiativeAgree != null)
				{
					harmony.Patch(initiativeAgree, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGangConvoInitiativeAgreePostfix)), null, null, null);
				}
				MethodInfo expansionTreaty = typeof(ConvoCallbacks).GetMethod("ExecuteGangConvoBuyExpansionTreaty", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (expansionTreaty != null)
				{
					harmony.Patch(expansionTreaty, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGangConvoDealPostfix)), null, null, null);
				}
				MethodInfo jointWar = typeof(ConvoCallbacks).GetMethod("ExecuteGangConvoBuyJointWar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (jointWar != null)
				{
					harmony.Patch(jointWar, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGangConvoDealPostfix)), null, null, null);
				}
				MethodInfo outpostAgreement = typeof(ConvoCallbacks).GetMethod("ExecuteGangConvoBuyOutpostNoStealAgreement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (outpostAgreement != null)
				{
					harmony.Patch(outpostAgreement, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGangConvoDealPostfix)), null, null, null);
				}
				MethodInfo goonRewardStart = typeof(ConvoCallbacks).GetMethod("ExecuteGoonRewardStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (goonRewardStart != null)
				{
					harmony.Patch(goonRewardStart, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGoonRewardStartPostfix)), null, null, null);
				}
				MethodInfo goonRewardTake = typeof(ConvoCallbacks).GetMethod("ExecuteGoonRewardTake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (goonRewardTake != null)
				{
					harmony.Patch(goonRewardTake, null, new HarmonyMethod(typeof(GangTradeConvoStateFixPatch), nameof(ExecuteGoonRewardTakePostfix)), null, null, null);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GangTradeConvoStateFixPatch failed: " + ex.Message);
			}
		}

		private static void InitializePostfix()
		{
			EnsureGangTradeConvoStateFixes("convo-init");
		}

		private static bool ProcessOnClickPrefix(ConvoCallbacks __instance, ConvoButton button, ref OnClickResult __result)
		{
			if (!TryHandleGangRobberyConvoClick(__instance, button, ref __result))
			{
				return false;
			}
			if (!TryHandleGangMoneyInConvoClick(__instance, button, ref __result))
			{
				return false;
			}
			TryAwardDataDrivenGangDealStreetCred(__instance, button);
			if (!TryHandleGangWarMediationConvoClick(__instance, button, ref __result))
			{
				return false;
			}
			return TryHandleGangPactConvoClick(__instance, button, ref __result);
		}

		private static void ExecuteGangConvoBuyTrucePostfix(ConvoCallbacks __instance)
		{
			FinalizeConvoGangTruce(__instance, "buy-truce");
		}

		private static void ExecuteGangConvoInitiativeAgreePostfix(ConvoCallbacks __instance, ConvoButton button)
		{
			ConvoDataGangConvoInit data = button?.GetData<ConvoDataGangConvoInit>();
			if (data == null || data.topic != global::Game.Session.Player.AI.ConvoInitiative.Topic.TruceRequest)
			{
				return;
			}
			FinalizeConvoGangTruce(__instance, "initiative-truce");
		}

		private static void FinalizeConvoGangTruce(ConvoCallbacks callbacks, string sourceTag)
		{
			try
			{
				VisitState visit = ConvoCallbacksVisitProperty?.GetValue(callbacks, null) as VisitState;
				PlayerInfo humanPlayer = visit?.GetPlayer() ?? G.GetHumanPlayer();
				PlayerInfo gang = visit?.npc?.data?.agent?.pid.FindPlayer();
				if (humanPlayer == null || gang == null || gang.PID.IsHumanPlayer)
				{
					return;
				}
				FinalizeGangTruceBetweenPlayers(humanPlayer, gang, sourceTag);
				CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.03f, 0.05f, "convo-gang-truce");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Gang truce finalizer failed: " + ex.Message);
			}
		}

		private static void ExecuteGangConvoDealPostfix(ConvoCallbacks __instance)
		{
			try
			{
				VisitState visit = ConvoCallbacksVisitProperty?.GetValue(__instance, null) as VisitState;
				PlayerInfo humanPlayer = visit?.GetPlayer() ?? G.GetHumanPlayer();
				if (humanPlayer == null || !humanPlayer.PID.IsHumanPlayer)
				{
					return;
				}
				CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.03f, 0.05f, "convo-gang-deal");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ExecuteGangConvoDealPostfix failed: " + ex.Message);
			}
		}

		private static void ExecuteGoonRewardStartPostfix(ConvoCallbacks __instance)
		{
			try
			{
				VisitState visit = ConvoCallbacksVisitProperty?.GetValue(__instance, null) as VisitState;
				PlayerInfo humanPlayer = visit?.GetPlayer() ?? G.GetHumanPlayer();
				if (humanPlayer == null || !humanPlayer.PID.IsHumanPlayer)
				{
					return;
				}
				CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.03f, 0.04f, "goon-reward-start");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ExecuteGoonRewardStartPostfix failed: " + ex.Message);
			}
		}

		private static void ExecuteGoonRewardTakePostfix(ConvoCallbacks __instance)
		{
			try
			{
				VisitState visit = ConvoCallbacksVisitProperty?.GetValue(__instance, null) as VisitState;
				PlayerInfo humanPlayer = visit?.GetPlayer() ?? G.GetHumanPlayer();
				if (humanPlayer == null || !humanPlayer.PID.IsHumanPlayer)
				{
					return;
				}
				CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.04f, 0.06f, "goon-reward-take");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ExecuteGoonRewardTakePostfix failed: " + ex.Message);
			}
		}
	}

	private static UISettings _patchedGangTradeConvoUiSettings;

	private const string GangPactTopLevelStateId = "gang-toplevel";

	private const string GangPactStarterStateId = "gameplaytweaks-gang-pact";

	private const string GangPactSuccessStateId = "gameplaytweaks-gang-pact-success";

	private const string GangPactFailStateId = "gameplaytweaks-gang-pact-fail";

	private const string GangPactCallbackId = "GameplayTweaksFormPact";

	private const string GangMoneyInCallbackId = "GameplayTweaksGangMoneyIn";

	private const string GangRobberyCallbackId = "GameplayTweaksGangRobbery";

	private const string GangRobberySuccessStateId = "gameplaytweaks-gang-robbery-success";

	private const string GangRobberyFailStateId = "gameplaytweaks-gang-robbery-fail";

	private const string GangWarMediationCallbackId = "GameplayTweaksMediateWarOpen";

	private static int _gangMoneyInHandledFrame = -1;

	private static ulong _gangMoneyInHandledActorId;

	private static ulong _gangMoneyInHandledNpcId;

	private static int _gangMoneyInHandledDay = -1;

	private static readonly HashSet<string> _gangMoneyInHandledActorNpcDayKeys = new HashSet<string>(StringComparer.Ordinal);

	private static int _gangRobberyHandledFrame = -1;

	private static ulong _gangRobberyHandledActorId;

	private static ulong _gangRobberyHandledNpcId;

	private static int _gangRobberyHandledDay = -1;

	private static readonly HashSet<string> _gangRobberyHandledActorNpcDayKeys = new HashSet<string>(StringComparer.Ordinal);

	private static int _gangDealStreetCredHandledFrame = -1;

	private static string _gangDealStreetCredHandledKey;

	private static readonly Color ConversationDefaultPlayerPactColor = new Color(0.4f, 0.8f, 0.4f);

	private static readonly FieldInfo ConvoCallbacksControllerField = typeof(ConvoCallbacks).GetField("_ctrl", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly PropertyInfo ConvoCallbacksVisitProperty = typeof(ConvoCallbacks).GetProperty("Visit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly MethodInfo ConversationJumpToStateMethod = typeof(global::Game.UI.Session.Convo.ConversationController).GetMethod("JumpToConvoState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
	{
		typeof(Label),
		typeof(ConvoData)
	}, null);

	internal static void EnsureGangTradeConvoStateFixes(string source)
	{
		try
		{
			UISettings ui = global::Game.Game.serv?.globals?.ui;
			if (ui?.convos == null)
			{
				return;
			}
			if (ReferenceEquals(_patchedGangTradeConvoUiSettings, ui))
			{
				return;
			}

			LabelDictionary<ConvoStateDef> convos = ui.convos;
			Label gangTopLevel = new Label("gang-toplevel");
			int patchedStates = 0;
			int patchedButtons = 0;

			EnsureGangConvoCustomCallbacksRegistered();
			Label lowb2 = new Label("gang-trade-liquor-lowb2");
			if (!convos.ContainsKey(lowb2))
			{
				ConvoStateDef lowb;
				if (convos.TryGetValue(new Label("gang-trade-liquor-lowb"), out lowb) && lowb != null)
				{
					convos[lowb2] = lowb;
					patchedStates++;
				}
			}

			EnsureGangPactConversationOverrides(convos, ref patchedStates, ref patchedButtons);
			if (PatchGangMoneyInStarterButton(convos))
			{
				patchedStates++;
				patchedButtons++;
			}
			if (EnsureGangWarMediationTopLevelStarter(convos))
			{
				patchedStates++;
				patchedButtons++;
			}

			foreach (KeyValuePair<Label, ConvoStateDef> item in convos)
			{
				string text = item.Key.ToString();
				if (!ShouldPatchGangTradeConvoState(text) || item.Value?.buttons == null)
				{
					continue;
				}

				bool flag = false;
				if (string.Equals(text, "gang-money-in", StringComparison.OrdinalIgnoreCase) && PatchGangMoneyInButton(item.Value))
				{
					flag = true;
					patchedButtons++;
				}
				if (IsRobberyConvoStateId(text) && PatchGangRobberyButtons(item.Value, text))
				{
					flag = true;
					patchedButtons++;
				}
				foreach (ConvoButtonDef button in item.Value.buttons)
				{
					if (button == null)
					{
						continue;
					}

					if (button.grants != null && !HasExplicitConvoNext(button.next))
					{
						button.next = new NextStateDef
						{
							@goto = gangTopLevel
						};
						patchedButtons++;
						flag = true;
						continue;
					}

					if (string.Equals(button.onClick, "Goodbye", StringComparison.Ordinal) && !HasExplicitConvoNext(button.next))
					{
						button.next = new NextStateDef
						{
							@goto = gangTopLevel
						};
						patchedButtons++;
						flag = true;
					}
				}

				if (flag)
				{
					patchedStates++;
				}
			}

			_patchedGangTradeConvoUiSettings = ui;
			if (patchedButtons > 0)
			{
				Debug.Log($"[GameplayTweaks] Gang trade convo fix applied source={source} states={patchedStates} buttons={patchedButtons}");
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] EnsureGangTradeConvoStateFixes failed: " + ex.Message);
		}
	}

	private static bool PatchGangMoneyInButton(ConvoStateDef state)
	{
		if (state?.buttons == null)
		{
			return false;
		}
		foreach (ConvoButtonDef button in state.buttons)
		{
			if (!string.Equals(button?.text?.key, "convo.gangs.dirtycash1-price.yes", StringComparison.Ordinal))
			{
				continue;
			}
			bool changed = false;
			if (!string.Equals(button.onClick, GangMoneyInCallbackId, StringComparison.Ordinal))
			{
				button.onClick = GangMoneyInCallbackId;
				changed = true;
			}
			if (button.grants != null)
			{
				button.grants = null;
				changed = true;
			}
			return changed;
		}
		return false;
	}

	private static bool PatchGangRobberyButtons(ConvoStateDef state, string stateId)
	{
		if (state?.buttons == null)
		{
			return false;
		}
		bool changed = false;
		string topLevelStateId = GetRobberyTopLevelStateId(stateId);
		foreach (ConvoButtonDef button in state.buttons)
		{
			string textKey = button?.text?.key;
			if (string.IsNullOrWhiteSpace(textKey) || textKey.IndexOf("convo.gangs.robbery", StringComparison.OrdinalIgnoreCase) < 0 || textKey.IndexOf(".yes", StringComparison.OrdinalIgnoreCase) < 0)
			{
				continue;
			}
			if (!string.Equals(button.onClick, GangRobberyCallbackId, StringComparison.Ordinal))
			{
				button.onClick = GangRobberyCallbackId;
				changed = true;
			}
			if (button.grants != null)
			{
				button.grants = null;
				changed = true;
			}
			if (EnsureGangRobberyBossRequirement(button))
			{
				changed = true;
			}
			if (!HasExplicitConvoNext(button.next))
			{
				button.next = new NextStateDef
				{
					@goto = new Label(topLevelStateId)
				};
				changed = true;
			}
		}
		return changed;
	}

	private static bool EnsureGangRobberyBossRequirement(ConvoButtonDef button)
	{
		if (button == null)
		{
			return false;
		}
		if (button.visreqs == null)
		{
			button.visreqs = new ConvoButtonRequirementList();
		}
		foreach (IRequirement requirement in button.visreqs)
		{
			if (requirement is CheckCrewRank rankRequirement && rankRequirement.@is == CheckCrewRank.Rank.Boss)
			{
				return false;
			}
		}
		button.visreqs.Add(new CheckCrewRank
		{
			@is = CheckCrewRank.Rank.Boss
		});
		return true;
	}

	private static bool IsRobberyConvoStateId(string stateId)
	{
		if (string.IsNullOrWhiteSpace(stateId))
		{
			return false;
		}
		return stateId.StartsWith("gang-robbery-", StringComparison.OrdinalIgnoreCase)
			|| stateId.StartsWith("goon-robbery-", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetRobberyTopLevelStateId(string stateId)
	{
		return !string.IsNullOrWhiteSpace(stateId) && stateId.StartsWith("goon-", StringComparison.OrdinalIgnoreCase)
			? "goon-toplevel"
			: GangPactTopLevelStateId;
	}

	private static string GetRobberyTopLevelStateId(PlayerInfo target)
	{
		return target?.IsJustGoon == true ? "goon-toplevel" : GangPactTopLevelStateId;
	}

	private static bool CanStoreDirtyCash(Entity entity)
	{
		try
		{
			InventoryModule inventory = ModulesUtil.GetInventory(entity);
			return ((Module<InventoryModule, InventoryModuleConfig, InventoryModuleData>)(object)inventory)?.data != null;
		}
		catch
		{
			return false;
		}
	}

	private static Entity ResolveGangMoneyInDirtyCashTarget(VisitState visit, PlayerInfo humanPlayer)
	{
		if (visit?.crew.IsValid == true && humanPlayer?.crew != null)
		{
			CrewAssignment liveCrew = humanPlayer.crew.GetCrewForPeep(visit.crew.peepId);
			CrewAssignment actingCrew = liveCrew.IsValid ? liveCrew : visit.crew;
			if (actingCrew.IsInVehicle && actingCrew.VehicleID.IsValid)
			{
				Entity vehicle = actingCrew.VehicleID.FindEntity();
				if (CanStoreDirtyCash(vehicle))
				{
					return vehicle;
				}

				EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanPlayer.crew, actingCrew.VehicleID);
				Entity driverPeep = driverPeepId.IsValid ? driverPeepId.FindEntity() : null;
				if (CanStoreDirtyCash(driverPeep))
				{
					return driverPeep;
				}
			}

			Entity actingPeep = actingCrew.GetPeep();
			if (CanStoreDirtyCash(actingPeep))
			{
				return actingPeep;
			}
		}

		if (CanStoreDirtyCash(visit?.vehicle))
		{
			return visit.vehicle;
		}

		Entity safehouse = EntityIDExtensions.FindEntity(humanPlayer?.territory?.Safehouse ?? EntityID.INVALID);
		return CanStoreDirtyCash(safehouse) ? safehouse : null;
	}

	private static bool TryReserveGangMoneyInTransaction(int day, ulong actorId, ulong npcId, out string transactionKey)
	{
		transactionKey = $"{day}:{actorId}:{npcId}";
		if (_gangMoneyInHandledDay != day)
		{
			_gangMoneyInHandledDay = day;
			_gangMoneyInHandledActorNpcDayKeys.Clear();
		}

		return _gangMoneyInHandledActorNpcDayKeys.Add(transactionKey);
	}

	private static bool TryReserveGangRobberyTransaction(int day, ulong actorId, ulong npcId, out string transactionKey)
	{
		transactionKey = $"{day}:{actorId}:{npcId}";
		if (_gangRobberyHandledDay != day)
		{
			_gangRobberyHandledDay = day;
			_gangRobberyHandledActorNpcDayKeys.Clear();
		}

		return _gangRobberyHandledActorNpcDayKeys.Add(transactionKey);
	}

	private sealed class LiteralConvoBlurb : ConvoBlurb
	{
		internal string Text;

		public override string GetBlurb(ConversationModel _, ConvoButton __, string[] replacements)
		{
			return Text ?? string.Empty;
		}
	}

	private static void SetGangRobberyResultState(string stateId, string text, string topLevelStateId)
	{
		try
		{
			UISettings ui = global::Game.Game.serv?.globals?.ui;
			if (ui?.convos == null || string.IsNullOrEmpty(stateId))
			{
				return;
			}
			ui.convos[new Label(stateId)] = new ConvoStateDef
			{
				npcsays = new ConvoBlurbList
				{
					new LiteralConvoBlurb
					{
						Text = string.IsNullOrWhiteSpace(text) ? "Word gets around fast." : text
					}
				},
				buttons = new List<ConvoButtonDef>
				{
					new ConvoButtonDef
					{
						text = new ConvoButtonDef.ConvoSimpleText
						{
							dynkey = new ConvoBlurbList
							{
								new LiteralConvoBlurb
								{
									Text = "Continue"
								}
							},
							icon = "convo.icon-back"
						},
						next = new NextStateDef
						{
							@goto = new Label(string.IsNullOrWhiteSpace(topLevelStateId) ? GangPactTopLevelStateId : topLevelStateId)
						}
					}
				}
			};
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] SetGangRobberyResultState failed: " + ex.Message);
		}
	}

	private static bool PatchGangMoneyInStarterButton(LabelDictionary<ConvoStateDef> convos)
	{
		if (convos == null || !convos.TryGetValue(new Label("gang-toplevel"), out ConvoStateDef state) || state?.buttons == null)
		{
			return false;
		}
		foreach (ConvoButtonDef button in state.buttons)
		{
			if (!string.Equals(button?.text?.key, "convo.gangs.dirtycash1.button", StringComparison.Ordinal) && !TargetsState(button, "gang-money-in"))
			{
				continue;
			}
			bool changed = false;
			changed |= EnsureGangMoneyInStarterRequirement(ref button.visreqs);
			changed |= EnsureGangMoneyInStarterRequirement(ref button.reqs);
			return changed;
		}
		return false;
	}

	private static bool EnsureGangMoneyInStarterRequirement(ref ConvoButtonRequirementList requirements)
	{
		const string buffId = "relbuff-gangs-loot5-table-on-finish";
		if (requirements != null)
		{
			foreach (IRequirement requirement in requirements)
			{
				if (requirement is CheckOwnerRelbuffs ownerRelbuffs
					&& ownerRelbuffs.of != null
					&& ownerRelbuffs.of.Any((Label label) => string.Equals(label.ToString(), buffId, StringComparison.OrdinalIgnoreCase)))
				{
					if (ownerRelbuffs.has == CheckListOfItems.Type.None)
					{
						return false;
					}
					ownerRelbuffs.has = CheckListOfItems.Type.None;
					return true;
				}
			}
		}
		else
		{
			requirements = new ConvoButtonRequirementList();
		}
		requirements.Add(new CheckOwnerRelbuffs
		{
			has = CheckListOfItems.Type.None,
			of = new List<Label> { new Label(buffId) }
		});
		return true;
	}

	private static void EnsureGangConvoCustomCallbacksRegistered()
	{
		try
		{
			if (ConvoCallbacks.AllCallbackNames != null && !ConvoCallbacks.AllCallbackNames.Contains(GangWarMediationCallbackId))
			{
				ConvoCallbacks.AllCallbackNames.Add(GangWarMediationCallbackId);
			}
			if (ConvoCallbacks.AllCallbackNames != null && !ConvoCallbacks.AllCallbackNames.Contains(GangRobberyCallbackId))
			{
				ConvoCallbacks.AllCallbackNames.Add(GangRobberyCallbackId);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] EnsureGangConvoCustomCallbacksRegistered failed: " + ex.Message);
		}
	}

	private static bool EnsureGangWarMediationTopLevelStarter(LabelDictionary<ConvoStateDef> convos)
	{
		if (convos == null || !convos.TryGetValue(new Label(GangPactTopLevelStateId), out ConvoStateDef topLevelState) || topLevelState?.buttons == null)
		{
			return false;
		}
		if (topLevelState.buttons.Any((ConvoButtonDef button) => string.Equals(button?.onClick, GangWarMediationCallbackId, StringComparison.Ordinal)))
		{
			return false;
		}
		ConvoButtonDef mediationButton = new ConvoButtonDef
		{
			visreqs = new ConvoButtonRequirementList
			{
				new CheckBlockingCop
				{
					expected = false
				}
			},
			reqs = new ConvoButtonRequirementList
			{
				new CheckConvoActions
				{
					has = CheckConvoActions.CheckType.Enough
				}
			},
			text = new ConvoButtonDef.ConvoSimpleText
			{
				key = "convo.gangs.mediate-war.button",
				icon = "convo.icon-check"
			},
			onClick = GangWarMediationCallbackId,
			next = new NextStateDef
			{
				@goto = new Label(GangPactTopLevelStateId)
			}
		};
		int insertIndex = topLevelState.buttons.FindIndex((ConvoButtonDef button) => ButtonMatchesGangPactStarter(button));
		if (insertIndex < 0)
		{
			topLevelState.buttons.Add(mediationButton);
		}
		else
		{
			topLevelState.buttons.Insert(insertIndex, mediationButton);
		}
		return true;
	}

	private static void EnsureGangPactConversationOverrides(LabelDictionary<ConvoStateDef> convos, ref int patchedStates, ref int patchedButtons)
	{
		if (convos == null)
		{
			return;
		}
		if (EnsureGangPactTopLevelStarter(convos))
		{
			patchedStates++;
			patchedButtons++;
		}
		if (EnsureGangPactIntroState(convos))
		{
			patchedStates++;
			patchedButtons += 2;
		}
		if (EnsureGangPactResultState(convos, "gangwars-alliance-success", GangPactSuccessStateId))
		{
			patchedStates++;
			patchedButtons++;
		}
		if (EnsureGangPactResultState(convos, "gangwars-alliance-fail", GangPactFailStateId))
		{
			patchedStates++;
			patchedButtons++;
		}
	}

	private static bool EnsureGangPactTopLevelStarter(LabelDictionary<ConvoStateDef> convos)
	{
		if (!convos.TryGetValue(new Label(GangPactTopLevelStateId), out ConvoStateDef topLevelState) || topLevelState?.buttons == null)
		{
			return false;
		}
		ConvoButtonDef sourceButton = topLevelState.buttons.FirstOrDefault((ConvoButtonDef button) => ButtonMatchesGangPactStarter(button));
		if (sourceButton == null)
		{
			return false;
		}
		ConvoButtonDef patchedButton = CloneConvoButtonDef(sourceButton);
		patchedButton.visreqs = FilterGangPactStarterRequirements(sourceButton.visreqs);
		patchedButton.reqs = FilterGangPactStarterRequirements(sourceButton.reqs);
		patchedButton.next = new NextStateDef
		{
			@goto = new Label(GangPactStarterStateId)
		};
		for (int i = 0; i < topLevelState.buttons.Count; i++)
		{
			if (!ButtonMatchesGangPactStarter(topLevelState.buttons[i]))
			{
				continue;
			}
			topLevelState.buttons[i] = patchedButton;
			return true;
		}
		topLevelState.buttons.Add(patchedButton);
		return true;
	}

	private static bool EnsureGangPactIntroState(LabelDictionary<ConvoStateDef> convos)
	{
		if (!convos.TryGetValue(new Label("gangwars-alliance"), out ConvoStateDef sourceState) || sourceState == null)
		{
			return false;
		}
		ConvoStateDef patchedState = CloneConvoStateDef(sourceState);
		if (patchedState?.buttons == null || patchedState.buttons.Count == 0)
		{
			return false;
		}
		ConvoButtonDef acceptButton = CloneConvoButtonDef(patchedState.buttons[0]);
		acceptButton.onClick = GangPactCallbackId;
		acceptButton.grants = null;
		acceptButton.next = new NextStateDef
		{
			@goto = new Label(GangPactTopLevelStateId)
		};
		patchedState.buttons[0] = acceptButton;
		convos[new Label(GangPactStarterStateId)] = patchedState;
		return true;
	}

	private static bool EnsureGangPactResultState(LabelDictionary<ConvoStateDef> convos, string sourceStateId, string targetStateId)
	{
		if (!convos.TryGetValue(new Label(sourceStateId), out ConvoStateDef sourceState) || sourceState == null)
		{
			return false;
		}
		ConvoStateDef patchedState = CloneConvoStateDef(sourceState);
		if (patchedState?.buttons != null)
		{
			for (int i = 0; i < patchedState.buttons.Count; i++)
			{
				if (patchedState.buttons[i] == null)
				{
					continue;
				}
				ConvoButtonDef clonedButton = CloneConvoButtonDef(patchedState.buttons[i]);
				clonedButton.onClick = null;
				clonedButton.grants = null;
				clonedButton.next = new NextStateDef
				{
					@goto = new Label(GangPactTopLevelStateId)
				};
				patchedState.buttons[i] = clonedButton;
			}
		}
		convos[new Label(targetStateId)] = patchedState;
		return true;
	}

	private static bool ButtonMatchesGangPactStarter(ConvoButtonDef button)
	{
		if (button == null)
		{
			return false;
		}
		if (string.Equals(button?.text?.key, "convo.gangs.alliance.button", StringComparison.Ordinal))
		{
			return true;
		}
		return TargetsState(button, "gangwars-alliance") || TargetsState(button, GangPactStarterStateId);
	}

	private static bool TargetsState(ConvoButtonDef button, string stateId)
	{
		if (button?.next == null || string.IsNullOrEmpty(stateId))
		{
			return false;
		}
		if (button.next.@goto.IsSet && string.Equals(button.next.@goto.ToString(), stateId, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (button.next.then.IsSet && string.Equals(button.next.then.ToString(), stateId, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return button.next.@else.IsSet && string.Equals(button.next.@else.ToString(), stateId, StringComparison.OrdinalIgnoreCase);
	}

	private static ConvoButtonRequirementList FilterGangPactStarterRequirements(ConvoButtonRequirementList source)
	{
		if (source == null)
		{
			return null;
		}
		ConvoButtonRequirementList filtered = new ConvoButtonRequirementList();
		foreach (IRequirement requirement in source)
		{
			if (requirement is CheckOwnerRelationship)
			{
				continue;
			}
			if (requirement is CheckOwnerRelbuffs ownerRelbuffs && ownerRelbuffs.of != null && ownerRelbuffs.of.Any((Label label) => string.Equals(label.ToString(), "relbuff-gangwars-alliance", StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}
			filtered.Add(requirement);
		}
		return filtered;
	}

	private static bool TryHandleGangPactConvoClick(ConvoCallbacks callbacks, ConvoButton button, ref OnClickResult result)
	{
		string onClickId = (button != null && button.state.def != null) ? button.state.def.onClick : null;
		if (!string.Equals(onClickId, GangPactCallbackId, StringComparison.Ordinal))
		{
			return true;
		}
		bool accepted = false;
		TryRequestGangPactFromConversation(callbacks, out accepted);
		if (TryJumpConversationToState(callbacks, accepted ? GangPactSuccessStateId : GangPactFailStateId))
		{
			result = OnClickResult.PAUSE_CONVERSATION;
		}
		else
		{
			result = OnClickResult.CONTINUE;
		}
		return false;
	}

	private static bool TryHandleGangWarMediationConvoClick(ConvoCallbacks callbacks, ConvoButton button, ref OnClickResult result)
	{
		string onClickId = (button != null && button.state.def != null) ? button.state.def.onClick : null;
		if (!string.Equals(onClickId, GangWarMediationCallbackId, StringComparison.Ordinal))
		{
			return true;
		}
		try
		{
			VisitState visit = ConvoCallbacksVisitProperty?.GetValue(callbacks, null) as VisitState;
			PlayerInfo gang = visit?.npc?.data?.agent?.pid.FindPlayer();
			if (gang == null || gang.PID.IsHumanPlayer)
			{
				result = OnClickResult.CONTINUE;
				return false;
			}
			GangWarMediationPopup.Show(gang, visit);
			result = OnClickResult.PAUSE_CONVERSATION;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryHandleGangWarMediationConvoClick failed: " + ex.Message);
			result = OnClickResult.CONTINUE;
		}
		return false;
	}

	private static bool TryHandleGangRobberyConvoClick(ConvoCallbacks callbacks, ConvoButton button, ref OnClickResult result)
	{
		string onClickId = (button != null && button.state.def != null) ? button.state.def.onClick : null;
		if (!string.Equals(onClickId, GangRobberyCallbackId, StringComparison.Ordinal))
		{
			return true;
		}
		try
		{
			VisitState visit = ConvoCallbacksVisitProperty?.GetValue(callbacks, null) as VisitState;
			PlayerInfo humanPlayer = visit?.GetPlayer() ?? G.GetHumanPlayer();
			PlayerInfo targetGang = visit?.npc?.data?.agent?.pid.FindPlayer();
			if (visit == null || humanPlayer == null || targetGang == null || !humanPlayer.PID.IsHumanPlayer || targetGang.PID.IsHumanPlayer)
			{
				result = OnClickResult.CONTINUE;
				return false;
			}
			if (!IsGangRobberyBossConversationTarget(visit, targetGang))
			{
				VerificationLog("GangRobbery", $"blocked reason=non-boss-target target={targetGang.PID.id} human={humanPlayer.PID.id} npc={(visit.npc?.Id.id ?? 0UL)} boss={(GetCrewPeepForPlayer(targetGang).IsValid ? GetCrewPeepForPlayer(targetGang).id : 0UL)}");
				result = OnClickResult.CONTINUE;
				return false;
			}
			ulong crewId = visit.crew.peepId.IsValid ? visit.crew.peepId.id : 0UL;
			ulong actorId = visit.crew.IsInVehicle && visit.crew.VehicleID.IsValid ? visit.crew.VehicleID.id : crewId;
			ulong npcId = visit.npc?.Id.id ?? 0UL;
			int currentFrame = Time.frameCount;
			if (_gangRobberyHandledFrame == currentFrame && _gangRobberyHandledActorId == actorId && _gangRobberyHandledNpcId == npcId)
			{
				result = OnClickResult.CONTINUE;
				return false;
			}
			_gangRobberyHandledFrame = currentFrame;
			_gangRobberyHandledActorId = actorId;
			_gangRobberyHandledNpcId = npcId;
			int currentDay = G.GetNow().days;
			if (!TryReserveGangRobberyTransaction(currentDay, actorId, npcId, out string transactionKey))
			{
				VerificationLog("GangRobbery", $"deduped-day key={transactionKey} actor={actorId} crew={crewId} npc={npcId}");
				result = OnClickResult.CONTINUE;
				return false;
			}

			string textKey = button.state.def.text?.key ?? string.Empty;
			bool highValue = IsHighValueGangRobberyKey(textKey);
			float successChance = CalculateGangRobberySuccessChance(humanPlayer, targetGang, highValue);
			bool success = SharedRng.NextDouble() < successChance;
			int cash = success ? RollGangRobberyCash(textKey) : 0;
			string humanName = humanPlayer.social?.PlayerGroupName ?? "your outfit";
			string targetName = targetGang.social?.PlayerGroupName ?? ("Gang#" + targetGang.PID.id);
			string topLevelStateId = GetRobberyTopLevelStateId(targetGang);
			GangOpsChannel channel = ResolveGangOpsChannelForGang(targetGang.PID.id);
			float heatGain = success ? (highValue ? 8f : 5f) : (highValue ? 18f : 12f);

			visit.ConsumeConvoActionsHelper();
			AddDirectedRelationshipBuff(targetGang, humanPlayer, success ? "relbuff-gangs-robbery2-table-on-finish" : "relbuff-gangs-robbery1-table-on-finish", GetCrewPeepForPlayer(targetGang));
			if (!success)
			{
				AddDirectedRelationshipBuff(targetGang, humanPlayer, "relbuff-gangs-robbery1-buff", GetCrewPeepForPlayer(targetGang));
			}
			AddWarHeat(channel, targetGang.PID.id, humanPlayer.PID.id, heatGain, success ? "convo-robbery-success" : "convo-robbery-failed");
			if (success)
			{
				humanPlayer.finances.DoChangeMoneyOnSafehouse(new Price(cash), MoneyReason.Other);
				GrantStreetCredit(humanPlayer, highValue ? 2 : 1);
				CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit.crew.peepId, 0.04f, highValue ? 0.08f : 0.06f, "gang-robbery-success");
				string successText = FormatGangRobberySuccessText(humanName, targetName, cash, highValue);
				SetGangRobberyResultState(GangRobberySuccessStateId, successText, topLevelStateId);
				bool jumped = TryJumpConversationToState(callbacks, GangRobberySuccessStateId);
				LogGrapevine($"ROBBERY: {humanName} shook down {targetName} for ${cash}. {targetName} is angry, but held fire.");
				VerificationLog("GangRobbery", $"success target={targetGang.PID.id} human={humanPlayer.PID.id} cash={cash} chance={successChance:0.00} heat={heatGain:0.0} high={highValue}");
				result = jumped ? OnClickResult.PAUSE_CONVERSATION : OnClickResult.CONTINUE;
			}
			else
			{
				ActivateWarBetweenPlayers(targetGang, humanPlayer);
				EntityID preferredTarget = visit.crew.peepId.IsValid ? visit.crew.peepId : GetCrewPeepForPlayer(humanPlayer);
				int desiredCrew = Mathf.Clamp(highValue ? 2 : 1, 1, 3);
				bool dispatched = TryTriggerRobberyFailureRetaliation(channel, targetGang, humanPlayer, desiredCrew, "convo-robbery-failed", preferredTarget, out int dispatchedCount, out string retaliationAction);
				string failText = FormatGangRobberyFailText(humanName, targetName, dispatchedCount, highValue);
				SetGangRobberyResultState(GangRobberyFailStateId, failText, topLevelStateId);
				bool jumped = TryJumpConversationToState(callbacks, GangRobberyFailStateId);
				LogGrapevine($"ROBBERY: {humanName} tried to rob {targetName}, but the score went bad. {targetName} struck back.");
				VerificationLog("GangRobbery", $"failed target={targetGang.PID.id} human={humanPlayer.PID.id} chance={successChance:0.00} heat={heatGain:0.0} high={highValue} attack={dispatched} crews={dispatchedCount} action={retaliationAction}");
				result = jumped ? OnClickResult.PAUSE_CONVERSATION : OnClickResult.CONTINUE;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryHandleGangRobberyConvoClick failed: " + ex.Message);
			result = OnClickResult.CONTINUE;
		}
		return false;
	}

	private static bool IsGangRobberyBossConversationTarget(VisitState visit, PlayerInfo targetGang)
	{
		Entity npc = visit?.npc;
		if (npc?.components?.agent == null || targetGang == null)
		{
			return false;
		}
		try
		{
			if (npc.components.agent.IsBoss().pass)
			{
				return true;
			}
		}
		catch
		{
		}
		EntityID bossPeepId = GetCrewPeepForPlayer(targetGang);
		return bossPeepId.IsValid && npc.Id == bossPeepId;
	}

	private static bool IsHighValueGangRobberyKey(string textKey)
	{
		if (string.IsNullOrEmpty(textKey))
		{
			return false;
		}
		return textKey.IndexOf("robbery2", StringComparison.OrdinalIgnoreCase) >= 0
			|| textKey.IndexOf("2g", StringComparison.OrdinalIgnoreCase) >= 0
			|| textKey.IndexOf("high", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static int RollGangRobberyCash(string textKey)
	{
		bool highValue = IsHighValueGangRobberyKey(textKey);
		bool gangVersion = !string.IsNullOrEmpty(textKey) && textKey.IndexOf("g-price", StringComparison.OrdinalIgnoreCase) >= 0;
		int min = highValue ? (gangVersion ? 1200 : 850) : (gangVersion ? 750 : 450);
		int max = highValue ? (gangVersion ? 1900 : 1300) : (gangVersion ? 1150 : 750);
		return SharedRng.Next(min / 50, max / 50 + 1) * 50;
	}

	private static float CalculateGangRobberySuccessChance(PlayerInfo humanPlayer, PlayerInfo targetGang, bool highValue)
	{
		float chance = highValue ? 0.48f : 0.64f;
		int humanPower = CalculateGangPower(humanPlayer);
		int targetPower = CalculateGangPower(targetGang);
		chance += Mathf.Clamp((humanPower - targetPower) / 260f, -0.20f, 0.12f);
		if (HasGangRobberyBossTrait(targetGang, "trait-cautious", "trait-nervous", "trait-connected"))
		{
			chance -= 0.08f;
		}
		if (HasGangRobberyBossTrait(targetGang, "trait-aggressive", "trait-vindictive", "trait-bold"))
		{
			chance -= 0.06f;
		}
		if (HasGangRobberyBossTrait(humanPlayer, "trait-aggressive", "trait-bold", "trait-confident"))
		{
			chance += 0.05f;
		}
		return Mathf.Clamp(chance, 0.22f, 0.82f);
	}

	private static bool HasGangRobberyBossTrait(PlayerInfo gang, params string[] traitIds)
	{
		if (gang == null || traitIds == null || traitIds.Length == 0)
		{
			return false;
		}
		EntityID bossPeepId = GetCrewPeepForPlayer(gang);
		Entity boss = bossPeepId.IsValid ? bossPeepId.FindEntity() : null;
		if (boss == null)
		{
			return false;
		}
		for (int i = 0; i < traitIds.Length; i++)
		{
			if (HasTrait(boss, traitIds[i]))
			{
				return true;
			}
		}
		return false;
	}

	private static string FormatGangRobberySuccessText(string humanName, string targetName, int cash, bool highValue)
	{
		string[] options = highValue
			? new string[]
			{
				$"{targetName}'s runner gives up the roll after one look at the guns. {humanName} pockets ${cash}, but the insult will not stay quiet.",
				$"The ambush holds. {targetName} loses ${cash} in clean bills, and their people leave promising they will remember the faces.",
				$"A bag changes hands in the alley. {humanName} gets ${cash}; {targetName} gets a reason to load up later."
			}
			: new string[]
			{
				$"The shake-down works. {targetName} pays ${cash} to get their people out clean, but the room turns cold.",
				$"Nobody draws. {humanName} takes ${cash} and lets the runner walk, leaving {targetName} angry enough to talk.",
				$"The mark folds before the first shove. {humanName} collects ${cash}; {targetName} swallows it for now."
			};
		return options[SharedRng.Next(options.Length)];
	}

	private static string FormatGangRobberyFailText(string humanName, string targetName, int dispatchedCount, bool highValue)
	{
		string response = dispatchedCount > 0 ? $"{targetName} sends shooters before the talk is even over." : $"{targetName} starts a war and begins hunting for a clean shot.";
		string[] options = highValue
			? new string[]
			{
				$"The tip was bad. The bag is light, the lookout is made, and {targetName}'s muscle was waiting. {response}",
				$"{humanName} walks into a setup. The cash never appears, and {targetName} answers the robbery with guns.",
				$"The alley goes wrong fast: no payday, too many witnesses, and {targetName} ready to hit back. {response}"
			}
			: new string[]
			{
				$"The runner spots the play early and bolts straight to {targetName}. No cash changes hands. {response}",
				$"The shake-down misses. {targetName} hears about it before anyone gets paid, and they come looking for satisfaction.",
				$"{humanName} presses too hard and gets nothing. {targetName} takes it as an open challenge."
			};
		return options[SharedRng.Next(options.Length)];
	}

	private static void TryAwardDataDrivenGangDealStreetCred(ConvoCallbacks callbacks, ConvoButton button)
	{
		try
		{
			ConvoButtonDef def = (button != null && button.state.def != null) ? button.state.def : null;
			if (def == null || def.grants == null)
			{
				return;
			}

			string textKey = def.text?.key;
			if (!TryGetDataDrivenGangDealStreetCredSource(textKey, out string source))
			{
				return;
			}

			VisitState visit = ConvoCallbacksVisitProperty?.GetValue(callbacks, null) as VisitState;
			PlayerInfo humanPlayer = visit?.GetPlayer() ?? G.GetHumanPlayer();
			if (humanPlayer == null || !humanPlayer.PID.IsHumanPlayer)
			{
				return;
			}

			ulong crewId = visit?.crew.peepId.IsValid == true ? visit.crew.peepId.id : 0UL;
			ulong npcId = visit?.npc?.Id.id ?? 0UL;
			string handledKey = $"{crewId}:{npcId}:{textKey}";
			if (_gangDealStreetCredHandledFrame == Time.frameCount && string.Equals(_gangDealStreetCredHandledKey, handledKey, StringComparison.Ordinal))
			{
				return;
			}

			_gangDealStreetCredHandledFrame = Time.frameCount;
			_gangDealStreetCredHandledKey = handledKey;
			CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.03f, 0.06f, source);
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryAwardDataDrivenGangDealStreetCred failed: " + ex.Message);
		}
	}

	private static bool TryGetDataDrivenGangDealStreetCredSource(string textKey, out string source)
	{
		source = null;
		if (string.IsNullOrWhiteSpace(textKey))
		{
			return false;
		}

		string key = textKey.ToLowerInvariant();
		if (!key.StartsWith("convo.gangs.", StringComparison.Ordinal) || !key.Contains(".yes"))
		{
			return false;
		}

		if (key.Contains("robbery"))
		{
			source = "convo-pact-robbery";
			return true;
		}
		if (key.Contains("dirtycash"))
		{
			source = "convo-pact-laundering";
			return true;
		}
		if (key.Contains("drugs") || key.Contains("drug"))
		{
			source = "convo-pact-drugs";
			return true;
		}
		if (key.Contains("tradeimports") || key.Contains("liquor") || key.Contains("trade1") || key.Contains("trade2") || key.Contains("trade3"))
		{
			source = "convo-pact-liquor";
			return true;
		}
		if (key.Contains("loan"))
		{
			source = "convo-pact-loan";
			return true;
		}
		if (key.Contains("trade"))
		{
			source = "convo-pact-deal";
			return true;
		}

		return false;
	}

	private static bool TryHandleGangMoneyInConvoClick(ConvoCallbacks callbacks, ConvoButton button, ref OnClickResult result)
	{
		string onClickId = (button != null && button.state.def != null) ? button.state.def.onClick : null;
		if (!string.Equals(onClickId, GangMoneyInCallbackId, StringComparison.Ordinal))
		{
			return true;
		}
		const int cleanCost = 1100;
		const int dirtyQty = 1400;
		const string dirtyCashLabel = "dirty-cash";
		try
		{
			VisitState visit = ConvoCallbacksVisitProperty?.GetValue(callbacks, null) as VisitState;
			PlayerInfo humanPlayer = visit?.GetPlayer();
			if (visit == null || humanPlayer == null)
			{
				result = OnClickResult.CONTINUE;
				return false;
			}
			ulong crewId = visit.crew.peepId.IsValid ? visit.crew.peepId.id : 0UL;
			ulong actorId = visit.crew.IsInVehicle && visit.crew.VehicleID.IsValid
				? visit.crew.VehicleID.id
				: crewId;
			ulong npcId = visit.npc?.Id.id ?? 0UL;
			int currentFrame = Time.frameCount;
			if (_gangMoneyInHandledFrame == currentFrame && _gangMoneyInHandledActorId == actorId && _gangMoneyInHandledNpcId == npcId)
			{
				VerificationLog("GangMoneyIn", $"deduped frame={currentFrame} actor={actorId} crew={crewId} npc={npcId}");
				result = OnClickResult.CONTINUE;
				return false;
			}
			_gangMoneyInHandledFrame = currentFrame;
			_gangMoneyInHandledActorId = actorId;
			_gangMoneyInHandledNpcId = npcId;
			Price cleanDelta = new Price(-cleanCost);
			if (!humanPlayer.finances.CanChangeMoneyOnCrew(visit, cleanDelta))
			{
				VerificationLog("GangMoneyIn", $"blocked afford=false cleanCost={cleanCost} dirtyQty={dirtyQty}");
				result = OnClickResult.CONTINUE;
				return false;
			}
			Entity dirtyTarget = ResolveGangMoneyInDirtyCashTarget(visit, humanPlayer);
			if (dirtyTarget == null)
			{
				VerificationLog("GangMoneyIn", $"blocked reason=no-dirty-target actor={actorId} crew={crewId} npc={npcId}");
				result = OnClickResult.CONTINUE;
				return false;
			}
			int currentDay = G.GetNow().days;
			if (!TryReserveGangMoneyInTransaction(currentDay, actorId, npcId, out string transactionKey))
			{
				VerificationLog("GangMoneyIn", $"deduped-day key={transactionKey} actor={actorId} crew={crewId} npc={npcId}");
				result = OnClickResult.CONTINUE;
				return false;
			}
			int totalDirtyBefore = GetTotalDirtyCash();
			int targetDirtyBefore = ReadInventoryAmount(dirtyTarget, dirtyCashLabel);
			GrantContext grantContext = new GrantContext(visit);
			humanPlayer.finances.DoChangeMoneyOnCrew(visit, cleanDelta, MoneyReason.Other);
			AddDirtyCash(dirtyTarget, dirtyQty, "gang-money-in");
			visit.ConsumeConvoActionsHelper();
			new AddRelBuff
			{
				with = AbstractRelGrant.With.Player,
				id = new Label("relbuff-gangs-loot5-table-on-finish")
			}.Apply(grantContext);
			new AddHeatBuffAtNode
			{
				id = new Label("heatbuff-gangs-loot1-table-on-finish"),
				instant = true
			}.Apply(grantContext);
			GrantStreetCredit(humanPlayer, 1);
			CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit.crew.peepId, 0.04f, 0.06f, "gang-money-in");
			new AddXp
			{
				delta = 50
			}.Apply(grantContext);
			int totalDirtyAfter = GetTotalDirtyCash();
			int targetDirtyAfter = ReadInventoryAmount(dirtyTarget, dirtyCashLabel);
			VerificationLog("GangMoneyIn", $"applied cleanCost={cleanCost} dirtyQty={dirtyQty} actor={actorId} target={(dirtyTarget?.Id.id ?? 0UL)} targetBefore={targetDirtyBefore} targetAfter={targetDirtyAfter} totalBefore={totalDirtyBefore} totalAfter={totalDirtyAfter}");
			result = OnClickResult.CONTINUE;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryHandleGangMoneyInConvoClick failed: " + ex.Message);
			result = OnClickResult.CONTINUE;
		}
		return false;
	}

	private static bool TryRequestGangPactFromConversation(ConvoCallbacks callbacks, out bool accepted)
	{
		accepted = false;
		VisitState visit = ConvoCallbacksVisitProperty?.GetValue(callbacks, null) as VisitState;
		PlayerInfo humanPlayer = G.GetHumanPlayer();
		PlayerInfo gang = visit?.npc?.data?.agent?.pid.FindPlayer();
		if (humanPlayer == null || gang == null || gang.PID.IsHumanPlayer)
		{
			return false;
		}
		AlliancePact playerPact = SaveData.Pacts.FirstOrDefault((AlliancePact pact) => pact.ColorIndex == PLAYER_PACT_SLOT_INDEX);
		if (playerPact != null && (IsGangMemberOfConversationPact(playerPact, gang.PID.id) || !CanGangJoinPact(playerPact, gang.PID.id)))
		{
			return false;
		}
		if (GetPactForPlayer(gang.PID) != null)
		{
			return false;
		}
		float acceptance = CalculateConversationPactAcceptance(gang, humanPlayer);
		if (playerPact == null || !playerPact.PlayerColorConfirmed)
		{
			if (SharedRng.NextDouble() >= acceptance)
			{
				SaveData.PactInviteCooldowns[gang.PID.id] = G.GetNow().days;
				return true;
			}
			accepted = CrewRelationshipHandlerPatch.QueueConversationPlayerPactInvite(gang, "conversation");
			if (accepted)
			{
				visit?.ConsumeConvoActionsHelper();
				CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.05f, 0.06f, "convo-pact-accepted");
			}
			return accepted;
		}
		TryInviteGangToPlayerPactFromConversation(playerPact, gang, humanPlayer, acceptance, out accepted);
		if (accepted)
		{
			visit?.ConsumeConvoActionsHelper();
			CrewRelationshipHandlerPatch.TryAwardCrewStreetCreditProgress(humanPlayer, visit?.crew.peepId ?? EntityID.INVALID, 0.05f, 0.06f, "convo-pact-accepted");
			RefreshPactCache();
			LogGrapevine($"PACT: {gang.social?.PlayerGroupName ?? ("Gang#" + gang.PID.id)} joined {playerPact.DisplayName ?? "your pact"}.");
		}
		return true;
	}

	private static bool TryJumpConversationToState(ConvoCallbacks callbacks, string stateId)
	{
		if (callbacks == null || string.IsNullOrEmpty(stateId) || ConversationJumpToStateMethod == null)
		{
			return false;
		}
		object controller = ConvoCallbacksControllerField?.GetValue(callbacks);
		if (controller == null)
		{
			return false;
		}
		try
		{
			ConversationJumpToStateMethod.Invoke(controller, new object[2]
			{
				new Label(stateId),
				null
			});
			return true;
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[GameplayTweaks] TryJumpConversationToState failed: " + ex.Message);
			return false;
		}
	}

	private static bool IsGangMemberOfConversationPact(AlliancePact pact, int gangId)
	{
		if (pact == null || gangId < 0)
		{
			return false;
		}
		if (pact.LeaderGangId == gangId)
		{
			return true;
		}
		return pact.MemberIds != null && pact.MemberIds.Contains(gangId);
	}

	private static float CalculateConversationPactAcceptance(PlayerInfo aiGang, PlayerInfo humanPlayer)
	{
		float acceptance = 0.3f;
		int humanPower = CalculateGangPower(humanPlayer);
		int gangPower = CalculateGangPower(aiGang);
		acceptance = ((humanPower <= gangPower) ? (acceptance - Math.Min(0.2f, (float)(gangPower - humanPower) / 200f)) : (acceptance + Math.Min(0.3f, (float)(humanPower - gangPower) / 200f)));
		int livingCrewCount = aiGang.crew?.LivingCrewCount ?? 0;
		if (livingCrewCount > 10)
		{
			acceptance -= (float)(livingCrewCount - 10) * 0.01f;
		}
		if (livingCrewCount < 5)
		{
			acceptance += 0.2f;
		}
		if (GetPactForPlayer(aiGang.PID) != null)
		{
			acceptance = 0f;
		}
		return Mathf.Clamp01(acceptance);
	}

	private static bool TryInviteGangToPlayerPactFromConversation(AlliancePact playerPact, PlayerInfo gang, PlayerInfo humanPlayer, float acceptance, out bool accepted)
	{
		accepted = false;
		if (playerPact == null || gang == null || humanPlayer == null)
		{
			return false;
		}
		SimTime now = G.GetNow();
		if (SaveData.PactInviteCooldowns.TryGetValue(gang.PID.id, out int lastInviteDay))
		{
			int daysSinceInvite = (lastInviteDay > 0) ? (now.days - lastInviteDay) : 9999;
			if (daysSinceInvite < ModConstants.PACT_JOIN_COOLDOWN_DAYS)
			{
				return false;
			}
		}
		if (SharedRng.NextDouble() >= acceptance)
		{
			SaveData.PactInviteCooldowns[gang.PID.id] = now.days;
			return true;
		}
		if (playerPact.LeaderGangId < 0)
		{
			playerPact.LeaderGangId = gang.PID.id;
		}
		else if (gang.PID.id != playerPact.LeaderGangId && !playerPact.MemberIds.Contains(gang.PID.id))
		{
			playerPact.MemberIds.Add(gang.PID.id);
		}
		playerPact.IsPending = false;
		accepted = true;
		TerritoryColorPatch.RefreshAllTerritoryColors();
		return true;
	}

	private static ConvoStateDef CloneConvoStateDef(ConvoStateDef source)
	{
		if (source == null)
		{
			return null;
		}
		return new ConvoStateDef
		{
			npcsays = source.npcsays,
			customView = source.customView,
			dynamicButtons = source.dynamicButtons,
			dynamicTemplate = CloneConvoButtonDef(source.dynamicTemplate),
			buttons = source.buttons?.Select((ConvoButtonDef button) => CloneConvoButtonDef(button)).ToList()
		};
	}

	private static ConvoButtonDef CloneConvoButtonDef(ConvoButtonDef source)
	{
		if (source == null)
		{
			return null;
		}
		return new ConvoButtonDef
		{
			visreqs = CloneConvoButtonRequirementList(source.visreqs),
			reqs = CloneConvoButtonRequirementList(source.reqs),
			text = CloneConvoSimpleText(source.text),
			onShow = source.onShow,
			onClick = source.onClick,
			onPreshow = source.onPreshow,
			next = CloneNextStateDef(source.next),
			multiplier = source.multiplier,
			definition = source.definition,
			grants = CloneVisitGrantList(source.grants),
			quickType = source.quickType
		};
	}

	private static ConvoButtonDef.ConvoSimpleText CloneConvoSimpleText(ConvoButtonDef.ConvoSimpleText source)
	{
		if (source == null)
		{
			return null;
		}
		return new ConvoButtonDef.ConvoSimpleText
		{
			dynkey = source.dynkey,
			dyndis = source.dyndis,
			dynicon = source.dynicon,
			dynmo = source.dynmo,
			dynnpc = source.dynnpc,
			key = source.key,
			dis = source.dis,
			icon = source.icon,
			mo = source.mo,
			npc = source.npc,
			addmo = source.addmo
		};
	}

	private static NextStateDef CloneNextStateDef(NextStateDef source)
	{
		if (source == null)
		{
			return null;
		}
		return new NextStateDef
		{
			reqs = CloneConvoButtonRequirementList(source.reqs),
			then = source.then,
			@else = source.@else,
			@goto = source.@goto
		};
	}

	private static ConvoButtonRequirementList CloneConvoButtonRequirementList(ConvoButtonRequirementList source)
	{
		if (source == null)
		{
			return null;
		}
		ConvoButtonRequirementList clone = new ConvoButtonRequirementList();
		foreach (IRequirement requirement in source)
		{
			clone.Add(requirement);
		}
		return clone;
	}

	private static VisitGrantList CloneVisitGrantList(VisitGrantList source)
	{
		if (source == null)
		{
			return null;
		}
		VisitGrantList clone = new VisitGrantList();
		foreach (VisitGrant grant in source)
		{
			clone.Add(grant);
		}
		return clone;
	}

	private static bool HasExplicitConvoNext(NextStateDef next)
	{
		if (next == null)
		{
			return false;
		}
		if (next.@goto.IsSet || next.then.IsSet || next.@else.IsSet)
		{
			return true;
		}
		return false;
	}

	private static bool ShouldPatchGangTradeConvoState(string stateId)
	{
		if (string.IsNullOrEmpty(stateId))
		{
			return false;
		}
		if (string.Equals(stateId, "gang-money-in", StringComparison.OrdinalIgnoreCase) || string.Equals(stateId, "gang-trade2-cigs-low", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string[] array = new string[8] { "gang-robbery-", "goon-robbery-", "gang-trade-drugs-", "gang-trade2-drugs-", "gang-trade-liquor-", "gang-trade2-liquor-", "gang-trade3-liquor-", "gang-trade-loan-" };
		for (int i = 0; i < array.Length; i++)
		{
			if (stateId.StartsWith(array[i], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	internal static class CombatAdvisorNullFixPatch
	{
		private static readonly Dictionary<string, int> NullRefCountByMethod = new Dictionary<string, int>(StringComparer.Ordinal);

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				PatchTypeMethodsWithNullFinalizer(harmony, "Game.Session.Player.AI.CombatAdvisor", "MaybeAskForTruce", "UpdateRequestsAfterAggro");
				PatchTypeMethodsWithNullFinalizer(harmony, "Game.Session.Player.AI.PrecinctAdvisor", "FindCarToCollect", "ProduceRequests");
				PatchTypeMethodsWithNullFinalizer(harmony, "Game.Session.Sim.VictoryTracker", "OnHumanTurnStarted", "OnAfterEntityLoad", "RecomputeAllGoals");
				PatchTypeMethodsWithNullFinalizer(harmony, "Game.Session.Sim.VictoryAbstractWorthSubgoal", "RecomputeState");
				PatchTypeMethodsWithNullFinalizer(harmony, "Game.UI.Session.Ledger.LedgerReportGenerator", "MakeResourcesList", "GetNetWorthOfPlayer");
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] CombatAdvisorNullFixPatch failed: {arg}");
			}
		}

		private static void PatchTypeMethodsWithNullFinalizer(Harmony harmony, string typeName, params string[] methodNames)
		{
			Type type = typeof(GameClock).Assembly.GetType(typeName);
			if (type == null || methodNames == null || methodNames.Length == 0)
			{
				return;
			}

			for (int i = 0; i < methodNames.Length; i++)
			{
				PatchMethodWithNullFinalizer(harmony, type, methodNames[i]);
			}
		}

		private static void PatchMethodWithNullFinalizer(Harmony harmony, Type type, string methodName)
		{
			MethodInfo method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			if (method == null)
			{
				return;
			}
			MethodInfo finalizer = typeof(CombatAdvisorNullFixPatch).GetMethod("NullRefFinalizer", BindingFlags.Static | BindingFlags.NonPublic);
			harmony.Patch((MethodBase)method, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, new HarmonyMethod(finalizer), (HarmonyMethod)null);
			Debug.Log("[GameplayTweaks] CombatAdvisorNullFixPatch finalizer applied to " + methodName);
		}

		private static Exception NullRefFinalizer(Exception __exception, MethodBase __originalMethod)
		{
			if (__exception is NullReferenceException)
			{
				try
				{
					string methodName = $"{__originalMethod?.DeclaringType?.FullName ?? "unknown"}.{__originalMethod?.Name ?? "unknown"}";
					NullRefCountByMethod.TryGetValue(methodName, out int count);
					count++;
					NullRefCountByMethod[methodName] = count;
					if (count == 1 || count % 100 == 0)
					{
						VerificationLog(
							"LoadStability",
							$"nullref-swallowed method={methodName} message={__exception.Message} count={count}");
					}
				}
				catch
				{
				}

				return null;
			}
			return __exception;
		}
	}
}
}
