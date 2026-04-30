using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class GangPanelBuffDebugPatch
	{
		internal static int HookedMethodCount { get; private set; }

		private static int _lastLoggedDay = -1;

		private static readonly HashSet<string> _loggedGangKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				HookedMethodCount = 0;
				Type type = typeof(GameClock).Assembly.GetType("Game.UI.Session.GangInfoPanelUtil");
				if (type == null)
				{
					return;
				}
				int num = 0;
				MethodInfo[] methods = type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				foreach (MethodInfo methodInfo in methods)
				{
					if (!string.Equals(methodInfo.Name, "ShowGangPanel", StringComparison.Ordinal))
					{
						continue;
					}
					harmony.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(GangPanelBuffDebugPatch), "ShowGangPanelPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
					num++;
				}
				HookedMethodCount = num;
				if (num > 0)
				{
					GameplayTweaksPlugin.VerificationLog("GangBossBuffs", $"panel-hooked methods={num}");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GangPanelBuffDebugPatch failed: " + ex.Message);
			}
		}

		private static void ShowGangPanelPostfix(object[] __args, MethodBase __originalMethod)
		{
			try
			{
				int days = G.GetNow().days;
				if (_lastLoggedDay != days)
				{
					_lastLoggedDay = days;
					_loggedGangKeys.Clear();
				}
				int num = -1;
				if (__args != null)
				{
					for (int i = 0; i < __args.Length; i++)
					{
						if (TryResolveGangPid(__args[i], out num))
						{
							break;
						}
					}
				}
				if (num < 0)
				{
					return;
				}
				string text = num.ToString(CultureInfo.InvariantCulture) + ":" + (__originalMethod?.Name ?? "ShowGangPanel");
				if (!_loggedGangKeys.Add(text))
				{
					return;
				}
				GameplayTweaksPlugin.TryLogHumanGangRelationBuffSummary(num, "gang-panel-" + (__originalMethod?.Name ?? "ShowGangPanel"));
			}
			catch
			{
			}
		}

		private static bool TryResolveGangPid(object obj, out int gangPid)
		{
			gangPid = -1;
			if (obj == null)
			{
				return false;
			}
			if (obj is PlayerInfo playerInfo)
			{
				gangPid = playerInfo.PID.id;
				return gangPid >= 0;
			}
			if (obj is PlayerID playerID)
			{
				gangPid = playerID.id;
				return gangPid >= 0;
			}
			if (obj is Entity entity)
			{
				PlayerID? pid = entity.data?.agent?.pid;
				if (pid.HasValue)
				{
					gangPid = pid.Value.id;
					return gangPid >= 0;
				}
			}
			try
			{
				Type type = obj.GetType();
				PropertyInfo property = type.GetProperty("PID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetProperty("pid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (property != null)
				{
					object value = property.GetValue(obj);
					if (value is PlayerID playerID2)
					{
						gangPid = playerID2.id;
						return gangPid >= 0;
					}
				}
				FieldInfo field = type.GetField("PID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? type.GetField("pid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (field != null)
				{
					object value2 = field.GetValue(obj);
					if (value2 is PlayerID playerID3)
					{
						gangPid = playerID3.id;
						return gangPid >= 0;
					}
				}
			}
			catch
			{
			}
			return false;
		}
	}

	internal static class HostileMobileSelectionFixPatch
	{
		private static readonly HashSet<string> _loggedSelectionKeys = new HashSet<string>(StringComparer.Ordinal);

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				Type type = AccessTools.TypeByName("Game.UI.Session.PersonInfoDialog");
				MethodInfo[] array = type?.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					.Where((MethodInfo method) => method.Name == "Show")
					.Where((MethodInfo method) =>
					{
						ParameterInfo[] parameters = method.GetParameters();
						return parameters.Length >= 1
							&& parameters[0].ParameterType == typeof(Entity)
							&& (parameters.Length == 1 || parameters[1].ParameterType == typeof(Entity));
					})
					.ToArray();
				if (array == null || array.Length == 0)
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "person info redirect patch unavailable");
					return;
				}
				foreach (MethodInfo methodInfo in array)
				{
					ParameterInfo[] parameters = methodInfo.GetParameters();
					HarmonyMethod prefix = (parameters.Length == 1)
						? new HarmonyMethod(typeof(HostileMobileSelectionFixPatch), nameof(ShowPrefixSingle))
						: new HarmonyMethod(typeof(HostileMobileSelectionFixPatch), nameof(ShowPrefixWithSelect));
					harmony.Patch(methodInfo, prefix: prefix);
				}
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"person info redirect patch applied methods={array.Length}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HostileMobileSelectionFixPatch failed: " + ex.Message);
			}
		}

		private static bool ShowPrefixSingle(Entity __0)
		{
			try
			{
				return !TryRedirectPersonInfoToConvo(__0, null);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HostileMobileSelectionFixPatch prefix failed: " + ex);
				return true;
			}
		}

		private static bool ShowPrefixWithSelect(Entity __0, Entity __1)
		{
			try
			{
				return !TryRedirectPersonInfoToConvo(__0, __1);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HostileMobileSelectionFixPatch prefix failed: " + ex);
				return true;
			}
		}

		private static bool TryRedirectPersonInfoToConvo(Entity peep, Entity selectOnClose)
		{
			if (peep == null || !peep.Id.IsValid || peep.data?.agent == null || peep.data.agent.pid.IsHumanPlayer)
			{
				return false;
			}
			if (!TryResolveWorldSelectionSource(peep, selectOnClose, out Entity sourceMobile, out NodeID targetNodeId, out Entity resolvedPeep, out string sourceReason))
			{
				LogSkip(peep, "source", sourceReason);
				return false;
			}
			if (!TryFindMatchingHumanCrewAtNode(targetNodeId, out CrewAssignment matchingCrew, out int matchingCount, out string sourceTag))
			{
				LogSkip(peep, "crew", $"node={targetNodeId} matches={matchingCount} source={sourceTag}");
				return false;
			}
			Entity entity = resolvedPeep ?? peep;
			if (!MultiCrewVehicleHelper.TryStartCrewVisitWithPendingSelectionScope(entity, matchingCrew))
			{
				return false;
			}
			LogSelectionFix(sourceMobile, peep, entity, matchingCrew, targetNodeId, matchingCount, sourceTag);
			return true;
		}

		private static bool TryResolveWorldSelectionSource(Entity peep, Entity selectOnClose, out Entity sourceMobile, out NodeID targetNodeId, out Entity resolvedPeep, out string reason)
		{
			sourceMobile = null;
			targetNodeId = NodeID.INVALID;
			resolvedPeep = null;
			reason = "not-world-selection";
			if (selectOnClose != null)
			{
				return false;
			}
			if (global::Game.Game.serv?.ui?.TopPopupUnsafe != null)
			{
				reason = "popup-open";
				return false;
			}
			sourceMobile = global::Game.Game.ctx?.selection?.CurrentActive;
			if (sourceMobile == null)
			{
				reason = "active-missing";
				return false;
			}
			if (sourceMobile.components?.mobile == null)
			{
				reason = "active-not-mobile";
				return false;
			}
			PlayerID pid = sourceMobile.data?.mobile?.pid ?? PlayerID.INVALID;
			if (!pid.IsValid || pid.IsHumanPlayer)
			{
				reason = "active-owner-not-ai";
				return false;
			}
			PlayerInfo owner = pid.FindPlayer();
			Entity selectedPeep = null;
			if (owner?.crew != null)
			{
				selectedPeep = ResolveAiVehicleActionPeep(owner, sourceMobile.Id);
				if (selectedPeep == null)
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-skip reason=active-target-missing vehicle={sourceMobile.Id.id} clickedPeep={peep.Id.id}");
					reason = "active-target-missing";
					return false;
				}
				if (selectedPeep.Id != peep.Id)
				{
					CrewAssignment crewForPeep = owner.crew.GetCrewForPeep(peep.Id);
					if (!crewForPeep.IsValid || !crewForPeep.IsInVehicle || crewForPeep.VehicleID != sourceMobile.Id)
					{
						GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-skip reason=active-target-mismatch vehicle={sourceMobile.Id.id} clickedPeep={peep.Id.id} resolvedPeep={selectedPeep.Id.id}");
						reason = "active-target-mismatch";
						return false;
					}
					resolvedPeep = selectedPeep;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-normalized vehicle={sourceMobile.Id.id} clickedPeep={peep.Id.id} resolvedPeep={selectedPeep.Id.id}");
				}
			}
			if (sourceMobile.components?.mobile != null && sourceMobile.Id.IsValid)
			{
				_ = MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(sourceMobile, out targetNodeId, out _);
			}
			if (!targetNodeId.IsValid && selectedPeep != null && owner?.crew != null)
			{
				CrewAssignment selectedCrew = owner.crew.GetCrewForPeep(selectedPeep.Id);
				if (selectedCrew.IsValid && selectedCrew.IsInVehicle && selectedCrew.VehicleID.IsValid)
				{
					_ = MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(selectedCrew.VehicleID, out targetNodeId, out _);
				}
			}
			if (!targetNodeId.IsValid)
			{
				targetNodeId = peep.data?.agent?.nid ?? NodeID.INVALID;
			}
			if (!targetNodeId.IsValid)
			{
				if (owner?.crew == null)
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"hostile-mobile-police-skip reason=node-missing vehicle={sourceMobile.Id.id} clickedPeep={peep.Id.id} ownerPid={pid.id}");
				}
				reason = "node-missing";
				return false;
			}
			if (owner?.crew == null)
			{
				resolvedPeep = peep;
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"hostile-mobile-police-redirect vehicle={sourceMobile.Id.id} clickedPeep={peep.Id.id} resolvedPeep={resolvedPeep.Id.id} node={targetNodeId} ownerPid={pid.id}");
			}
			reason = "world-mobile-selection";
			return true;
		}

		private static Entity ResolveAiVehicleActionPeep(PlayerInfo owner, EntityID vehicleId)
		{
			return owner?.crew == null || !vehicleId.IsValid
				? null
				: MultiCrewVehicleHelper.ResolveVehicleActionPeep(owner.crew, vehicleId);
		}

		private static bool TryFindMatchingHumanCrewAtNode(NodeID targetNodeId, out CrewAssignment matchingCrew, out int matchingCount, out string sourceTag)
		{
			if (MultiCrewVehicleHelper.TryFindActualHumanCrewAtNode(
				targetNodeId,
				out matchingCrew,
				out matchingCount,
				out sourceTag))
			{
				return true;
			}

			matchingCrew = CrewAssignment.EMPTY;
			matchingCount = 0;
			sourceTag = "none";
			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null
				|| !MultiCrewVehicleHelper.TryGetHumanCrewUsableForScopePreviewAtNode(humanCrew, targetNodeId, out List<CrewAssignment> previewMatches, out string previewSource)
				|| previewMatches == null
				|| previewMatches.Count <= 0)
			{
				return false;
			}

			CrewAssignment selected = previewMatches.FirstOrDefault(match => match.peepId.IsValid);
			if (!selected.peepId.IsValid)
			{
				return false;
			}

			matchingCrew = selected;
			matchingCount = previewMatches.Count;
			sourceTag = "scope-preview-" + (string.IsNullOrWhiteSpace(previewSource) ? "final-goal" : previewSource);
			GameplayTweaksPlugin.VerificationLog(
				"HostileMobileSelect",
				$"preview-crew-match node={targetNodeId} crew={matchingCrew.peepId.id} matches={matchingCount} source={sourceTag}");
			return true;
		}

		private static void LogSelectionFix(Entity mobileEntity, Entity clickedPeep, Entity resolvedPeep, CrewAssignment crew, NodeID targetNodeId, int matchingCount, string sourceTag)
		{
			int day = G.GetNow().days;
			string mobileId = ((mobileEntity != null) ? mobileEntity.Id.id.ToString(CultureInfo.InvariantCulture) : "-1");
			string peepId = ((clickedPeep != null) ? clickedPeep.Id.id.ToString(CultureInfo.InvariantCulture) : "-1");
			string resolvedPeepId = ((resolvedPeep != null) ? resolvedPeep.Id.id.ToString(CultureInfo.InvariantCulture) : peepId);
			string nodeId = targetNodeId.IsValid ? targetNodeId.ToString() : "invalid";
			string key = $"{day}:{mobileId}:{peepId}:{resolvedPeepId}:{crew.peepId.id}";
			if (_loggedSelectionKeys.Add(key))
			{
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"redirect clickedPeep={peepId} resolvedPeep={resolvedPeepId} crew={crew.peepId.id} sourceMobile={mobileId} node={nodeId} matches={matchingCount} source={sourceTag}");
			}
		}

		private static void LogSkip(Entity peep, string kind, string detail)
		{
			int day = G.GetNow().days;
			string peepId = ((peep != null) ? peep.Id.id.ToString(CultureInfo.InvariantCulture) : "-1");
			string key = $"{day}:skip:{kind}:{peepId}:{detail}";
			if (_loggedSelectionKeys.Add(key))
			{
				if (string.Equals(kind, "crew", StringComparison.Ordinal))
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"skip reason=no-matching-human-crew peep={peepId} {detail}");
				}
				else
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"skip reason=not-world-selection peep={peepId} detail={detail}");
				}
			}
		}
	}

	internal static class KeyboardBlockerPatch
	{
		public static void ApplyPatch(Harmony harmony)
		{

			try
			{
				Type type = typeof(GameClock).Assembly.GetType("Game.Services.Input.KeyboardService");
				bool flag = false;
				if (type != null)
				{
					MethodInfo method = type.GetMethod("OnUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method != null)
					{
						harmony.Patch((MethodBase)method, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (OnUpdate)");
						flag = true;
					}
					MethodInfo method0 = type.GetMethod("DoInputUpdate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method0 != null)
					{
						harmony.Patch((MethodBase)method0, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (DoInputUpdate)");
						flag = true;
					}
					MethodInfo method1 = type.GetMethod("OnUpdateKeyboard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method1 != null)
					{
						harmony.Patch((MethodBase)method1, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (OnUpdateKeyboard)");
						flag = true;
					}
					MethodInfo method2 = type.GetMethod("ProcessKeys", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method2 != null)
					{
						harmony.Patch((MethodBase)method2, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (ProcessKeys)");
						flag = true;
					}
					MethodInfo method3 = type.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method3 != null)
					{
						harmony.Patch((MethodBase)method3, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (Update)");
						flag = true;
					}
				}
				Type type2 = typeof(GameClock).Assembly.GetType("Game.Services.Input.KeyboardHandler");
				if (type2 != null)
				{
					MethodInfo method4 = type2.GetMethod("ProcessEvent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (method4 != null)
					{
						harmony.Patch((MethodBase)method4, new HarmonyMethod(typeof(KeyboardBlockerPatch), "BlockKeyboardPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
						Debug.Log("[GameplayTweaks] Keyboard blocker patch applied (ProcessEvent)");
						flag = true;
					}
				}
				if (!flag)
				{
					Debug.LogWarning("[GameplayTweaks] Could not find any keyboard methods to patch!");
				}
			}
			catch (Exception arg)
			{
				Debug.LogError($"[GameplayTweaks] KeyboardBlockerPatch failed: {arg}");
			}
		}

		private static bool BlockKeyboardPrefix()
		{
			if (InputFieldBlocker.ShouldBlockKeyboard)
			{
				return false;
			}
			return true;
		}
	}
}
}
