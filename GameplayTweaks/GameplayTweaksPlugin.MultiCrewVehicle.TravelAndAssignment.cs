using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core;
using Game.Session.Actions;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Data;
using Game.Session;
using Game.Session.Player;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Input;
using Game.Services;
using Game.UI.Session.Combat;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.Popups;
using Game.UI.Util;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayTweaks
{
	internal static class DestroyVehiclePatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(PlayerCrew __instance, EntityID vehicleId, bool shutdown)
		{
			// Only block destroy when live onboard crew still remain in the vehicle.
			if (MultiCrewVehicleHelper.GetLiveVehicleCrewCount(__instance, vehicleId) > 0)
				return false;
			return true;
		}
	}

	internal static class CombatManagerHealingGuardPatch
	{
		[ThreadStatic]
		private static bool _isApplyingVehicleGroupHealing;
		internal const string DriverOnlyHealText = "Only the driver can heal from a vehicle.";

		[HarmonyPrefix]
		internal static bool Prefix(CombatManager __instance, PlayerID pid, ref CrewAssignment crew)
		{
			try
			{
				PlayerInfo player = pid.FindPlayer();
				if (player?.crew != null && crew.peepId.IsValid)
				{
					CrewAssignment latestAssignment = player.crew.GetCrewForPeep(crew.peepId);
					if (latestAssignment.IsValid)
					{
						crew = latestAssignment;
					}
				}
				if (!crew.IsValid || !crew.peepId.IsValid)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-skip pid={pid.id} reason=invalid-assignment peep={crew.peepId.id}");
					return false;
				}
				Entity peep = crew.GetPeep();
				if (peep?.components?.agent == null || !peep.components.agent.HasHealthPointsLeft)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-skip pid={pid.id} reason=missing-peep peep={crew.peepId.id}");
					return false;
				}
				if (!_isApplyingVehicleGroupHealing
					&& player?.IsHuman == true
					&& crew.IsInVehicle
					&& crew.VehicleID.IsValid
					&& !MultiCrewVehicleHelper.IsDriver(player.crew, crew))
				{
					MultiCrewVehicleHelper.ShowHudMessage(DriverOnlyHealText);
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-skip pid={pid.id} reason=driver-only peep={crew.peepId.id} vehicle={crew.VehicleID.id}");
					return false;
				}

				string executionLocationSource = "none";
				if (!_isApplyingVehicleGroupHealing
					&& player?.IsHuman == true
					&& crew.IsInVehicle
					&& crew.VehicleID.IsValid
					&& !TryIsAtVehicleHealingLocation(player, crew, out executionLocationSource))
				{
					MultiCrewVehicleHelper.ShowHudMessage(Loc.Get("ui.command.heal.mo.loc"));
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-skip pid={pid.id} reason=not-at-owned-location peep={crew.peepId.id} vehicle={crew.VehicleID.id}");
					return false;
				}

				if (!_isApplyingVehicleGroupHealing
					&& player?.IsHuman == true
					&& crew.IsInVehicle
					&& crew.VehicleID.IsValid
					&& __instance != null
					&& TryGetVehicleHealingTargets(player.crew, crew, out List<CrewAssignment> healingTargets))
				{
					_isApplyingVehicleGroupHealing = true;
					try
					{
						foreach (CrewAssignment target in healingTargets)
						{
							CrewAssignment latestTarget = player.crew.GetCrewForPeep(target.peepId);
							if (!latestTarget.IsValid)
							{
								continue;
							}

							__instance.PerformHealing(pid, latestTarget);
						}
					}
					finally
					{
						_isApplyingVehicleGroupHealing = false;
					}

					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"heal-vehicle-group pid={pid.id} vehicle={crew.VehicleID.id} crew={string.Join(",", healingTargets.Select(item => item.peepId.id.ToString()))} source={executionLocationSource}");
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CombatManagerHealingGuardPatch: " + ex.Message);
				return false;
			}
		}

		internal static bool TryGetVehicleHealingTargets(PlayerCrew crew, CrewAssignment actingCrew, out List<CrewAssignment> healingTargets)
		{
			healingTargets = new List<CrewAssignment>();
			if (crew == null || !actingCrew.IsInVehicle || !actingCrew.VehicleID.IsValid)
			{
				return false;
			}

			EntityID vehicleId = actingCrew.VehicleID;
			EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(crew, vehicleId);
			if (!driverPeepId.IsValid)
			{
				return false;
			}

			foreach (CrewAssignment occupant in MultiCrewVehicleHelper.GetAllCrewInVehicle(crew, vehicleId)
				.Where(item => item.IsValid && item.peepId.IsValid && item.IsNotDead && crew.IsOnBoard(item.peepId))
				.OrderByDescending(item => item.peepId == driverPeepId)
				.ThenBy(item => item.peepId.id))
			{
				Entity peep = occupant.GetPeep();
				if (peep?.components?.agent == null
					|| !peep.components.agent.HasHealthPointsLeft
					|| GameplayTweaksPlugin.IsCrewCurrentlyJailed(occupant.peepId)
					|| !peep.components.agent.IsWounded)
				{
					continue;
				}

				healingTargets.Add(occupant);
			}

			return healingTargets.Count > 0;
		}

		internal static bool TryBuildVehicleHealStatus(PlayerID pid, CrewAssignment crew, out CommandStatus status)
		{
			status = new CommandStatus(CommandType.Heal, Loc.Get("ui.command.heal.icon"));
			if (!pid.IsHumanPlayer || !crew.IsValid || !crew.IsInVehicle || !crew.VehicleID.IsValid)
			{
				return false;
			}

			PlayerInfo player = pid.FindPlayer();
			PlayerCrew humanCrew = player?.crew ?? G.GetHumanCrew();
			if (humanCrew == null)
			{
				return false;
			}

			CrewAssignment latestCrew = humanCrew.GetCrewForPeep(crew.peepId);
			if (latestCrew.IsValid)
			{
				crew = latestCrew;
			}

			if (!MultiCrewVehicleHelper.IsDriver(humanCrew, crew))
			{
				status = status.Set(CommandEnabledStatus.DisabledOther, DriverOnlyHealText);
				return true;
			}

			if (!TryGetVehicleHealingTargets(humanCrew, crew, out List<CrewAssignment> healingTargets))
			{
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-validate-hidden vehicle={crew.VehicleID.id} crew={crew.peepId.id} reason=no-wounded-occupants");
				return false;
			}

			if (!TryIsAtVehicleHealingLocation(player, crew, out string locationSource))
			{
				status = status.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.heal.mo.loc"));
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-validate-disabled vehicle={crew.VehicleID.id} crew={crew.peepId.id} reason=not-at-owned-location");
				return true;
			}

			Entity peep = crew.GetPeep();
			if (peep?.components?.agent == null || !peep.components.agent.HasActionsRemaining)
			{
				status = status.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.fail.points"));
				return true;
			}

			status = status.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.heal.mo.loc"));
			GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-validate-enabled vehicle={crew.VehicleID.id} crew={crew.peepId.id} targets={healingTargets.Count} source={locationSource}");
			return true;
		}

		internal static bool CanStartVehicleGroupHeal(PlayerID pid, EntityID peepId)
		{
			if (!pid.IsHumanPlayer || !peepId.IsValid)
			{
				return false;
			}

			PlayerInfo player = pid.FindPlayer();
			PlayerCrew humanCrew = player?.crew;
			if (humanCrew == null)
			{
				return false;
			}

			CrewAssignment crew = humanCrew.GetCrewForPeep(peepId);
			return crew.IsValid
				&& crew.IsInVehicle
				&& crew.VehicleID.IsValid
				&& MultiCrewVehicleHelper.IsDriver(humanCrew, crew)
				&& TryGetVehicleHealingTargets(humanCrew, crew, out _)
				&& TryIsAtVehicleHealingLocation(player, crew, out _);
		}

		private static bool TryIsAtVehicleHealingLocation(PlayerInfo player, CrewAssignment crew, out string source)
		{
			source = "none";
			if (player?.territory == null || !crew.IsInVehicle || !crew.VehicleID.IsValid)
			{
				return false;
			}

			foreach (NodeID nodeId in GetVehicleHealingLocationNodes(player).Where(nodeId => nodeId.IsValid).Distinct())
			{
				if (MultiCrewVehicleHelper.IsHumanVehicleAtOwnedBuildingAccessNode(crew.VehicleID, nodeId, out string accessSource))
				{
					source = $"{nodeId}:{accessSource}";
					return true;
				}
			}

			return false;
		}

		private static IEnumerable<NodeID> GetVehicleHealingLocationNodes(PlayerInfo player)
		{
			Node hqNode = player?.territory?.GetHeadquartersNode(false);
			if (hqNode?.id.IsValid == true)
			{
				yield return hqNode.id;
			}

			foreach (EntityID buildingId in MultiCrewVehicleHelper.GetControlledBuildingIds(player))
			{
				Entity building = buildingId.FindEntity();
				if (MultiCrewVehicleHelper.TryGetEntityBoardNodeId(building, out NodeID buildingNodeId) && buildingNodeId.IsValid)
				{
					yield return buildingNodeId;
				}
			}
		}
	}

	internal static class CommandButtonHealDriverPatch
	{
		[HarmonyPostfix]
		internal static void ValidatePostfix(PlayerID __0, CrewAssignment __1, ref CommandStatus __result)
		{
			try
			{
				if (!CombatManagerHealingGuardPatch.TryBuildVehicleHealStatus(__0, __1, out CommandStatus status))
				{
					return;
				}

				__result = status;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonHealDriverPatch.Validate: " + ex.Message);
			}
		}

		[HarmonyPrefix]
		internal static bool PostCommandAsHumanPrefix(CrewAssignment __0)
		{
			try
			{
				CrewAssignment crew = __0;
				if (!crew.IsValid || !crew.IsInVehicle || !crew.VehicleID.IsValid)
				{
					return true;
				}

				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null || MultiCrewVehicleHelper.IsDriver(humanCrew, crew))
				{
					return true;
				}

				MultiCrewVehicleHelper.ShowHudMessage(CombatManagerHealingGuardPatch.DriverOnlyHealText);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonHealDriverPatch.PostCommandAsHuman: " + ex.Message);
				return true;
			}
		}

		[HarmonyPrefix]
		internal static bool CanStartPrefix(CommandHeal __instance, ref Command.StartStatus __result)
		{
			try
			{
				if (__instance == null || !CombatManagerHealingGuardPatch.CanStartVehicleGroupHeal(__instance.pid, __instance.peepId))
				{
					return true;
				}

				__result = Command.StartStatus.OK;
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-canstart-driver-group peep={__instance.peepId.id}");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonHealDriverPatch.CanStart: " + ex.Message);
				return true;
			}
		}
	}

internal static class AssignCrewToVehiclePatch
{
	[HarmonyPrefix]
	internal static bool Prefix(PlayerCrew __instance, EntityID peepId, EntityID vehicleId)
		{
			// During map creation/setup there may be no human player; allow vanilla so assignment can complete.
			if (!__instance.PID.IsHumanPlayer)
				return true;
			if (G.GetHumanPlayer() == null)
				return true;

			try
			{
				if (MultiCrewVehicleHelper.IsCrewBlockedFromVehicleAssignment(peepId, out string custodyReason))
				{
					MultiCrewVehicleHelper.ShowHudMessage(custodyReason);
					return false;
				}

				int slots = MultiCrewVehicleHelper.GetVehicleCrewSlots(vehicleId);
				int currentCount = MultiCrewVehicleHelper.GetVehicleCrewCount(__instance, vehicleId);
				if (currentCount >= slots)
					return false;

				if (MultiCrewVehicleHelper.IsStartupFounderVehicleAssignment(__instance, peepId))
				{
					if (currentCount == 0)
						MultiCrewVehicleHelper.SetDriverForVehicle(__instance, vehicleId, peepId);
					return true;
				}

				// Exception: "Pick up stranded vehicle" flow allows assign when vehicle has 0 crew and is not at owned building.
				if (MultiCrewVehicleHelper.PickUpStrandedVehicleInProgress && currentCount == 0)
				{
					MultiCrewVehicleHelper.SetDriverForVehicle(__instance, vehicleId, peepId);
					return true;
				}

				// For the human player, only allow entering vehicles parked at owned buildings,
				// and seed the driver mapping when the first crew enters the vehicle.
				if (MultiCrewVehicleHelper.TryIsVehicleAtOwnedBuilding(vehicleId, out bool atOwned))
				{
					if (!atOwned)
					{
						MultiCrewVehicleHelper.ShowHudMessage("Crew can only enter vehicles parked at your own buildings.");
						return false;
					}
				}
				else
				{
					Debug.LogWarning("[GameplayTweaks] AssignCrewToVehicle: owned-building check unavailable; allowing assign.");
				}

				if (currentCount == 0)
				{
					MultiCrewVehicleHelper.SetDriverForVehicle(__instance, vehicleId, peepId);
				}
				return true;
			}
			catch (TypeLoadException ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AssignCrewToVehicle prefix failed (type load); allowing vanilla so setup can complete. {ex.GetType().Name}: {ex.Message}");
				return true;
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AssignCrewToVehicle prefix failed (type load); allowing vanilla so setup can complete. {ex.GetType().Name}: {ex.Message}");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AssignCrewToVehicle prefix failed; blocking assign. {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, EntityID peepId, EntityID vehicleId)
		{
			try
			{
				if (__instance == null || !peepId.IsValid || !vehicleId.IsValid)
					return;
				CrewAssignment crewForPeep = __instance.GetCrewForPeep(peepId);
				if (!crewForPeep.IsValid || !crewForPeep.IsInVehicle || crewForPeep.VehicleID != vehicleId)
					return;
				MultiCrewVehicleHelper.TrySyncVehicleOccupantsToVehicleNode(__instance, vehicleId);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] AssignCrewToVehicle postfix sync failed: " + ex.Message);
			}
		}
	}

internal static class AssignCrewToBuildingPatch
{
	[HarmonyPrefix]
	internal static bool Prefix(PlayerCrew __instance, EntityID peepId, EntityID buildingId)
	{
		if (__instance == null || !__instance.PID.IsHumanPlayer)
		{
			return true;
		}

		try
		{
			if (MultiCrewVehicleHelper.IsCrewBlockedFromVehicleAssignment(peepId, out string custodyReason))
			{
				MultiCrewVehicleHelper.ShowHudMessage(custodyReason);
				GameplayTweaksPlugin.VerificationLog("Compat", $"building-assign-blocked reason=custody building={buildingId.id} peep={peepId.id}");
				return false;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[GameplayTweaks] AssignCrewToBuilding prefix failed; blocking assign. {ex.GetType().Name}: {ex.Message}");
			return false;
		}

		return true;
	}
}

	internal static class AiHireNewCrewInVehiclePatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(PlayerCrew __instance, Node node, Entity peep, Entity introducer, bool isBoss, ref bool __state)
		{
			__state = false;
			if (CrewHireEligibilityPatch.ShouldBlockCrewHire(__instance, peep, isBoss, out string blockedReason))
			{
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"hire-method-blocked method=HireNewCrewInVehicle player={__instance?.PID.id ?? -1} peep={peep?.Id.id ?? 0UL} reason={blockedReason}");
				return false;
			}
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			if (!MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss))
			{
				return true;
			}

			MultiCrewVehicleHelper.PushAiHireNormalizationScope();
			__state = true;
			if (MultiCrewVehicleHelper.TryHandleAiVehicleHireIntercept(__instance, node, peep, introducer, isBoss))
			{
				MultiCrewVehicleHelper.PopAiHireNormalizationScope();
				__state = false;
				return false;
			}

			return true;
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, Node node, Entity peep, bool isBoss, bool __state)
		{
			if (!__state)
			{
				return;
			}
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			if (!MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss))
			{
				return;
			}

			MultiCrewVehicleHelper.TryNormalizeAiCrewAfterHire(player, peep.Id, node, allowVehicleCreateFallback: false, preserveCurrentVehicle: true, source: "ai-hire-generic");
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception, bool __state)
		{
			if (__state)
			{
				MultiCrewVehicleHelper.PopAiHireNormalizationScope();
			}
			return __exception;
		}
	}

	internal static class AiHireNewCrewMemberUnassignedPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(PlayerCrew __instance, Entity peep, ref bool __state)
		{
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			__state = MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss: false);
			if (CrewHireEligibilityPatch.ShouldBlockCrewHire(__instance, peep, isBoss: false, out string blockedReason))
			{
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"hire-method-blocked method=HireNewCrewMemberUnassigned player={__instance?.PID.id ?? -1} peep={peep?.Id.id ?? 0UL} reason={blockedReason}");
				__state = false;
				return false;
			}
			if (__state)
			{
				MultiCrewVehicleHelper.PushAiHireNormalizationScope();
			}
			return true;
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, Entity peep, bool __state)
		{
			if (!__state)
			{
				return;
			}
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			if (!MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss: false))
			{
				return;
			}

			MultiCrewVehicleHelper.TryNormalizeAiCrewAfterHire(player, peep.Id, player.territory?.GetHeadquartersNode(), allowVehicleCreateFallback: false, preserveCurrentVehicle: false, source: "ai-hire-unassigned");
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception, bool __state)
		{
			if (__state)
			{
				MultiCrewVehicleHelper.PopAiHireNormalizationScope();
			}
			return __exception;
		}
	}

	internal static class AiHireNewCrewInSpecificVehiclePatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(PlayerCrew __instance, Entity peep, ref bool __state)
		{
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			__state = MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss: false);
			if (CrewHireEligibilityPatch.ShouldBlockCrewHire(__instance, peep, isBoss: false, out string blockedReason))
			{
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"hire-method-blocked method=HireNewCrewInSpecificVehicle player={__instance?.PID.id ?? -1} peep={peep?.Id.id ?? 0UL} reason={blockedReason}");
				__state = false;
				return false;
			}
			if (__state)
			{
				MultiCrewVehicleHelper.PushAiHireNormalizationScope();
			}
			return true;
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, Node node, Entity peep, bool __state)
		{
			if (!__state)
			{
				return;
			}
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			if (!MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss: false))
			{
				return;
			}

			MultiCrewVehicleHelper.TryNormalizeAiCrewAfterHire(player, peep.Id, node, allowVehicleCreateFallback: false, preserveCurrentVehicle: true, source: "ai-hire-specific");
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception, bool __state)
		{
			if (__state)
			{
				MultiCrewVehicleHelper.PopAiHireNormalizationScope();
			}
			return __exception;
		}
	}

	internal static class AiAddToCrewUnassignedPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, Entity peep, bool isBoss)
		{
			PlayerInfo player = __instance != null ? __instance.PID.FindPlayer() : null;
			if (!MultiCrewVehicleHelper.ShouldRunAiHireNormalization(player, peep, isBoss)
				|| MultiCrewVehicleHelper.IsAiHireNormalizationScopeActive()
				|| MultiCrewVehicleHelper.IsAiHireNormalizationActive())
			{
				return;
			}

			MultiCrewVehicleHelper.TryNormalizeAiCrewAfterHire(player, peep.Id, player.territory?.GetHeadquartersNode(), allowVehicleCreateFallback: false, preserveCurrentVehicle: false, source: "ai-add-unassigned");
		}
	}

	internal static class PassengerMovementCapPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(CommandExecutor __instance)
		{
			try
			{
				if (__instance?.PID.IsHumanPlayer != true)
				{
					return;
				}
				MultiCrewVehicleHelper.PruneFreshStartupPendingVehicleTravel(__instance.PlayerInfo);
				MultiCrewVehicleHelper.ClearInactivePendingHumanVehicleTravel("stale-turn-start");
				MultiCrewVehicleHelper.FinalizeQueuedHumanVehicleArrivalsAtTurnStart(__instance.PlayerInfo);
				MultiCrewVehicleHelper.ClearVanillaQueuedCommandsForOwnedHumanRoutes(__instance);
			}
			catch (Exception ex)
			{
				Debug.LogError("[GameplayTweaks] PassengerMovementCapPatch prefix error: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		[HarmonyPostfix]
		internal static void Postfix(CommandExecutor __instance)
		{
			try
			{
				PlayerCrew crew = __instance.PlayerInfo?.crew;
				if (crew == null)
					return;
				if (__instance.PID.IsHumanPlayer)
				{
					HashSet<long> hashSet = new HashSet<long>();
					foreach (CrewAssignment assignment in crew.GetLiving())
					{
						MultiCrewVehicleHelper.ApplyPassengerMoveCap(crew, assignment);
						if (assignment.IsInVehicle && assignment.VehicleID.IsValid)
							hashSet.Add((long)assignment.VehicleID.id);
					}
					foreach (long item in hashSet)
					{
						EntityID vehicleId = EntityID.FromID((ulong)item);
						if (MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId) || MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId))
						{
							MultiCrewVehicleHelper.LogVehicleAuthority("turnstart-sync-skip", $"{vehicleId.id}:queued-resume", $"turnstart-sync-skip vehicle={vehicleId.id} reason=queued-resume", dedupe: false);
							continue;
						}
						MultiCrewVehicleHelper.TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId);
					}
					return;
				}

				MultiCrewVehicleHelper.TryApplyAiVehicleFillPolicy(__instance.PlayerInfo);
			}
			catch (Exception ex)
			{
				Debug.LogError("[GameplayTweaks] PassengerMovementCapPatch error: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}

	internal static class HumanVehicleTurnStartResumeAfterRefillPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(CommandExecutor __instance)
		{
			try
			{
				if (__instance?.PID.IsHumanPlayer != true)
				{
					return;
				}

				MultiCrewVehicleHelper.TryResumeQueuedHumanVehicleRoutes(__instance.PlayerInfo);
			}
			catch (Exception ex)
			{
				Debug.LogError("[GameplayTweaks] HumanVehicleTurnStartResumeAfterRefillPatch error: " + ex.GetType().Name + ": " + ex.Message);
			}
		}
	}

	internal static class HumanVehicleFlushQueuePatch
	{
		[HarmonyPostfix]
		internal static void Postfix(CommandExecutor __instance, EntityID peepId, bool cancelActive)
		{
			try
			{
				if (__instance?.PID.IsHumanPlayer != true || !peepId.IsValid)
				{
					return;
				}
				if (MultiCrewVehicleHelper.IsFlushQueueRouteClearSuppressed(peepId))
				{
					return;
				}
				if (MultiCrewVehicleHelper.TryConsumePreviewStopRouteClearSuppression(peepId, out string previewReason))
				{
					MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravelByPeep(peepId, out EntityID vehicleId, out NodeID goalNodeId);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"preview-stop-clear-blocked",
						$"{peepId.id}:{vehicleId.id}:{goalNodeId}:{previewReason}",
						$"preview-stop-clear-blocked peep={peepId.id} vehicle={vehicleId.id} finalGoal={goalNodeId} reason={previewReason}",
						dedupe: false);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"preview-stop-route-preserved",
						$"{peepId.id}:{vehicleId.id}:{goalNodeId}",
						$"preview-stop-route-preserved peep={peepId.id} vehicle={vehicleId.id} finalGoal={goalNodeId} source=flush-queue",
						dedupe: false);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"preview-repath-old-start-blocked",
						$"{peepId.id}:{vehicleId.id}:{goalNodeId}",
						$"preview-repath-old-start-blocked peep={peepId.id} vehicle={vehicleId.id} finalGoal={goalNodeId} source=flush-queue",
						dedupe: false);
					return;
				}
				MultiCrewVehicleHelper.ClearQueuedHumanVehicleTravelForPeep(__instance.PlayerInfo, peepId, "user-stop");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleFlushQueuePatch: " + ex.Message);
			}
		}
	}

	internal static class CarInputModeDriverPeepPatch
	{
		private static readonly FieldInfo CarField = AccessTools.Field(typeof(CarInputMode), "_car");
		private static readonly FieldInfo PeepField = AccessTools.Field(typeof(CarInputMode), "_peep");

		[HarmonyPostfix]
		internal static void ConstructorPostfix(CarInputMode __instance, Entity car)
		{
			RefreshDriverPeep(__instance, car, "ctor");
		}

		[HarmonyPrefix]
		internal static void UpdateLinePrefix(CarInputMode __instance)
		{
			Entity car = CarField?.GetValue(__instance) as Entity;
			RefreshDriverPeep(__instance, car, "update-line");
		}

		private static void RefreshDriverPeep(CarInputMode inputMode, Entity car, string source)
		{
			try
			{
				if (inputMode == null || car?.Id.IsValid != true || car.data?.mobile == null || !car.data.mobile.pid.IsHumanPlayer)
				{
					return;
				}

				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null)
				{
					return;
				}

				EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, car.Id);
				CrewAssignment driverAssignment = driverPeepId.IsValid ? humanCrew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
				Entity driverPeep = driverPeepId.IsValid ? driverPeepId.FindEntity() : null;
				if (!driverAssignment.IsValid
					|| !driverAssignment.IsInVehicle
					|| driverAssignment.VehicleID != car.Id
					|| !MultiCrewVehicleHelper.IsActiveVehicleOccupant(humanCrew, driverAssignment)
					|| driverPeep?.components?.agent == null)
				{
					return;
				}

				Entity currentPeep = PeepField?.GetValue(inputMode) as Entity;
				if (currentPeep == driverPeep)
				{
					return;
				}

				PeepField?.SetValue(inputMode, driverPeep);
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"car-input-driver-peep",
					$"{car.Id.id}:{currentPeep?.Id.id ?? 0UL}:{driverPeepId.id}:{source}",
					$"car-input-driver-peep vehicle={car.Id.id} priorPeep={currentPeep?.Id.id ?? 0UL} driver={driverPeepId.id} moves={driverPeep.components.agent.MovesRemaining} source={source}",
					dedupe: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CarInputModeDriverPeepPatch: " + ex.Message);
			}
		}
	}

	internal static class HumanVehicleCommandGotoPathPatch
	{
		private static readonly FieldInfo CommandGotoPathField = AccessTools.Field(typeof(CommandGoto), "_path");

		[HarmonyPrefix]
		internal static bool Prefix(CommandGoto __instance, ref Command.StartStatus __result)
		{
			try
			{
				if (MultiCrewVehicleHelper.TryGetOwnedQueuedHumanVehicleRoute(__instance, out EntityID staleVehicleId, out NodeID staleGoalNodeId))
				{
					CommandGotoPathField?.SetValue(__instance, null);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"route-stale-command-skip",
						$"{staleVehicleId.id}:{__instance.peepId.id}:{staleGoalNodeId}:set-path",
						$"route-stale-command-skip vehicle={staleVehicleId.id} peep={__instance.peepId.id} command=SetAndValidatePath finalGoal={staleGoalNodeId}",
						dedupe: false);
					__result = Command.StartStatus.Failed;
					return false;
				}
				if (__instance == null || !MultiCrewVehicleHelper.TryGetHumanVehicleCommandContext(__instance, out _, out PlayerCrew crew, out Entity peep, out CrewAssignment assignment, out Entity vehicle))
				{
					return true;
				}
				if (MultiCrewVehicleHelper.TryGetHumanVehicleTravelConflict(assignment.VehicleID, __instance, out int activeCommandKey, out int blockedCommandKey, out bool sameOwner))
				{
					bool interrupted = MultiCrewVehicleHelper.IsDriver(crew, assignment)
						&& MultiCrewVehicleHelper.TryInterruptActiveHumanVehicleTravelForImmediateMove(crew, vehicle, assignment.VehicleID, __instance, "command-path");
					if (interrupted)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-path-interrupt",
							$"{assignment.VehicleID.id}:{activeCommandKey}:{blockedCommandKey}",
							$"command-path-interrupt-active vehicle={assignment.VehicleID.id} activeCommand={activeCommandKey} replacementCommand={blockedCommandKey} sameOwner={sameOwner}",
							dedupe: false);
					}
					if (!interrupted)
					{
						CommandGotoPathField?.SetValue(__instance, null);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-path-blocked",
							$"{assignment.VehicleID.id}:{activeCommandKey}:{blockedCommandKey}",
							$"command-path-blocked-active vehicle={assignment.VehicleID.id} activeCommand={activeCommandKey} blockedCommand={blockedCommandKey} sameOwner={sameOwner}",
							dedupe: false);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-path-discarded",
							$"{assignment.VehicleID.id}:{activeCommandKey}:{blockedCommandKey}:active",
							$"command-path-discarded-active vehicle={assignment.VehicleID.id} activeCommand={activeCommandKey} blockedCommand={blockedCommandKey} moves={peep.components.agent.MovesRemaining} actions={peep.components.agent.ActionsRemaining} reason=active-travel-unreached",
							dedupe: false);
						__result = Command.StartStatus.Failed;
						return false;
					}
				}
				int movesRemaining = __instance.freeDrive ? int.MaxValue : peep.components.agent.MovesRemaining;
				if (movesRemaining <= 0)
				{
					CommandGotoPathField?.SetValue(__instance, null);
					__result = Command.StartStatus.SkipThisTurn;
					return false;
				}
				if (!MultiCrewVehicleHelper.TryBuildHumanVehicleCommandPath(__instance, out PathData path, out Node startNode, out string source))
				{
					return true;
				}
				CommandGotoPathField?.SetValue(__instance, path);
				Node goalNode = __instance.goalID.FindNode();
				NodeID startNodeId = startNode?.id ?? NodeID.INVALID;
				NodeID goalNodeId = goalNode?.id ?? NodeID.INVALID;
				string costText = path != null ? path.cost.ToString() : "invalid";
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"command-path",
					$"{assignment.VehicleID.id}:{startNodeId}:{goalNodeId}:{costText}",
					$"command-path-start vehicle={assignment.VehicleID.id} peep={__instance.peepId.id} startNode={startNodeId} goalNode={goalNodeId} source={source} cost={costText} movesBefore={movesRemaining} actionsBefore={peep.components.agent.ActionsRemaining}");
				__result = (path == null || path.world.Count <= 1) ? Command.StartStatus.Failed : Command.StartStatus.OK;
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleCommandGotoPathPatch: " + ex.Message);
				return true;
			}
		}
	}

	internal static class HumanVehiclePathSourcePatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(PlayerID pid, Entity agent, WorldPos target, Action<Pathfinding.Result> callback)
		{
			try
			{
				Node agentNode = null;
				try
				{
					agentNode = agent?.components?.agent?.GetNode();
				}
				catch
				{
					agentNode = null;
				}
				if (agent == null || agent.data?.agent == null || agent.components?.agent == null || agentNode == null || callback == null)
				{
					if (pid.IsAIPlayer || pid.IsHumanPlayer)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"path-source-blocked",
							$"{pid.id}:{agent?.Id.id ?? 0UL}:{(agentNode == null ? "node-missing" : "invalid")}",
							$"path-source-blocked pid={pid.id} agent={agent?.Id.id ?? 0UL} reason=node-missing",
							dedupe: true);
					}
					return false;
				}
				if (!pid.IsHumanPlayer)
				{
					return true;
				}
				PlayerCrew crew = pid.FindPlayer()?.crew;
				if (crew == null)
				{
					return true;
				}
				CrewAssignment assignment = crew.GetCrewForPeep(agent.Id);
				if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					return true;
				}
				Node vehicleNode = null;
				string source = "none";
				Node peepNode = agentNode;
				NodeID peepNodeId = peepNode?.id ?? NodeID.INVALID;
				if (MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(assignment.VehicleID)
					&& MultiCrewVehicleHelper.TryGetQueuedArrivalCommittedNode(assignment.VehicleID, out vehicleNode)
					&& vehicleNode != null)
				{
					source = "queued-arrival";
				}
				else if (!MultiCrewVehicleHelper.TryGetResolvedHumanVehiclePathStartNode(assignment.VehicleID, peepNodeId, out vehicleNode, out source) || vehicleNode == null)
				{
					return true;
				}
				if (peepNodeId != vehicleNode.id)
				{
					string category = string.Equals(source, "recent-finalize", StringComparison.Ordinal)
						|| string.Equals(source, "queued-arrival", StringComparison.Ordinal)
						|| string.Equals(source, "peep-live", StringComparison.Ordinal)
						? "path-redirect-committed"
						: "path";
					MultiCrewVehicleHelper.LogVehicleAuthority(category, $"{assignment.VehicleID.id}:{peepNodeId}:{vehicleNode.id}:{source}", $"path-redirect peep={agent.Id.id} vehicle={assignment.VehicleID.id} peepNode={peepNodeId} vehicleNode={vehicleNode.id} source={source}");
				}
				RoadPathContext ctx = new RoadPathContext(global::Game.Game.ctx.board);
				global::Game.Game.ctx.board.GetGridPath(pid, agent.Id, vehicleNode.pos, target, ctx, callback);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehiclePathSourcePatch: " + ex.Message);
				return true;
			}
		}
	}

	internal static class HumanVehicleTravelEndSyncPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(ActionNavigate __instance)
		{
			Entity vehicle = null;
			NodeID finalNodeId = NodeID.INVALID;
			Node startNode = null;
			bool finalizedTravelState = false;
			try
			{
				vehicle = __instance?.Agent;
				if (vehicle?.data?.mobile == null || !vehicle.data.mobile.pid.IsHumanPlayer)
				{
					return;
				}
				bool hasTravelOwner = MultiCrewVehicleHelper.TryGetHumanVehicleTravelOwner(vehicle.Id, out _);
				bool hasRecoverableTravelState = MultiCrewVehicleHelper.TryGetRecoverableHumanVehicleTravelState(vehicle.Id, out string recoverableSource);
				bool hasQueuedGoalOnly = false;
				if (!hasTravelOwner && !hasRecoverableTravelState
					&& MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(vehicle.Id, out _, out NodeID pendingExpectedNodeId, out NodeID pendingGoalNodeId)
					&& !pendingExpectedNodeId.IsValid
					&& pendingGoalNodeId.IsValid)
				{
					hasQueuedGoalOnly = true;
				}
				if (!hasTravelOwner && !hasRecoverableTravelState)
				{
					if (hasQueuedGoalOnly)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority("travel-end-ignored", $"{vehicle.Id.id}:queued-remainder", $"travel-end-queued-remainder-ignored vehicle={vehicle.Id.id}", dedupe: false);
					}
					else
					{
						MultiCrewVehicleHelper.LogVehicleAuthority("travel-end-ignored", $"{vehicle.Id.id}:postfinalize", $"travel-end-postfinalize-ignored vehicle={vehicle.Id.id}", dedupe: false);
					}
					return;
				}
				if (!hasTravelOwner && hasRecoverableTravelState)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority("travel-end-owner-recovered", $"{vehicle.Id.id}:{recoverableSource}", $"travel-end-owner-recovered vehicle={vehicle.Id.id} source={recoverableSource}", dedupe: false);
				}
				if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravelStartNode(vehicle.Id, out NodeID startNodeId) && startNodeId.IsValid)
				{
					startNode = startNodeId.FindNode();
				}
				PlayerCrew crew = vehicle.data.mobile.pid.FindPlayer()?.crew;
				if (crew == null || MultiCrewVehicleHelper.GetVehicleCrewCount(crew, vehicle.Id) <= 0)
				{
					return;
				}
				MultiCrewVehicleHelper.TryFinalizeHumanVehicleTravelStop(crew, vehicle, out finalNodeId);
				finalizedTravelState = true;

				if (finalNodeId.IsValid)
				{
					NodeID displayFinalNodeId = finalNodeId;
					MultiCrewVehicleHelper.TryGetHumanVehicleTravelDisplayFinalNodeId(vehicle.Id, finalNodeId, out displayFinalNodeId, out _);
					MultiCrewVehicleHelper.TryRefreshCrewHudStateAfterTravel(vehicle.Id, displayFinalNodeId);
					GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(startNode, displayFinalNodeId.FindNode(), "travel-finalize", vehicle.Id);
				}
				else
				{
					GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(startNode, null, "travel-finalize", vehicle.Id);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleTravelEndSyncPatch: " + ex.Message);
			}
			finally
			{
				if (vehicle != null && !finalizedTravelState)
				{
					MultiCrewVehicleHelper.CompleteHumanVehicleTravelSegment(vehicle.Id, finalNodeId, out _);
				}
			}
		}
	}

	internal static class HumanVehicleQueuedTurnStartPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(CommandGoto __instance)
		{
			try
			{
				if (__instance == null || !__instance.pid.IsHumanPlayer)
				{
					return true;
				}
				PlayerCrew crew = __instance.pid.FindPlayer()?.crew;
				if (crew == null)
				{
					return true;
				}

				EntityID vehicleId = EntityID.INVALID;
				NodeID goalNodeId = NodeID.INVALID;
				CrewAssignment assignment = crew.GetCrewForPeep(__instance.peepId);
				if (assignment.IsValid && assignment.IsInVehicle && assignment.VehicleID.IsValid)
				{
					vehicleId = assignment.VehicleID;
				}
				else if (!MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravelByPeep(__instance.peepId, out vehicleId, out goalNodeId))
				{
					return true;
				}

				if (!vehicleId.IsValid || !MultiCrewVehicleHelper.HasPendingHumanVehicleTravel(vehicleId))
				{
					return true;
				}

				MultiCrewVehicleHelper.LogVehicleAuthority(
					"route-vanilla-skip",
					$"{vehicleId.id}:{__instance.peepId.id}:{goalNodeId}",
					$"route-vanilla-turnstart-skip vehicle={vehicleId.id} peep={__instance.peepId.id} finalGoal={goalNodeId}",
					dedupe: false);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleQueuedTurnStartPatch: " + ex.Message);
				return true;
			}
		}
	}

	internal static class HumanVehicleQueuedCanActivatePatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(CommandGoto __instance, ref bool __result)
		{
			try
			{
				if (MultiCrewVehicleHelper.TryGetOwnedQueuedHumanVehicleRoute(__instance, out EntityID vehicleId, out NodeID goalNodeId))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"route-stale-command-skip",
						$"{vehicleId.id}:{__instance.peepId.id}:{goalNodeId}:can-activate",
						$"route-stale-command-skip vehicle={vehicleId.id} peep={__instance.peepId.id} command=CanActivateAfterDequeue finalGoal={goalNodeId}",
						dedupe: false);
					__result = false;
					return false;
				}
				if (MultiCrewVehicleHelper.TryShouldSkipAiQueuedVehicleCommand(__instance, out EntityID aiVehicleId, out string aiReason))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ai-route-stale-command-skip",
						$"{aiVehicleId.id}:{__instance.peepId.id}:{aiReason}",
						$"ai-route-stale-command-skip vehicle={aiVehicleId.id} peep={__instance.peepId.id} command=CanActivateAfterDequeue reason={aiReason}",
						dedupe: false);
					__result = false;
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleQueuedCanActivatePatch: " + ex.Message);
				return true;
			}
		}
	}

	internal static class CommandGotoDriverGuardPatch
	{
		// #region agent log
		private static readonly bool EnableCommandGotoDriverGuardDebugLog =
			string.Equals(Environment.GetEnvironmentVariable("COG_COMMAND_GOTO_DEBUG"), "1", StringComparison.Ordinal);

		private static void DebugLog(string hypothesisId, string message, object data = null)
		{
			if (!EnableCommandGotoDriverGuardDebugLog)
			{
				return;
			}
			try
			{
				string path = Path.Combine(UnityEngine.Application.persistentDataPath, "debug-469e6d.log");
				long ts = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
				var obj = new Dictionary<string, object> { ["sessionId"] = "469e6d", ["hypothesisId"] = hypothesisId, ["message"] = message, ["timestamp"] = ts };
				if (data != null) obj["data"] = data;
				string line = "{\"sessionId\":\"469e6d\",\"hypothesisId\":\"" + hypothesisId + "\",\"message\":\"" + (message ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"timestamp\":" + ts + "}" + Environment.NewLine;
				File.AppendAllText(path, line);
			}
			catch { }
		}
		// #endregion

		[HarmonyPrefix]
		internal static bool Prefix(CommandGoto __instance, ref Command.StartStatus __result)
		{
			// #region agent log
			DebugLog("H2", "prefix_entered");
			// #endregion
			try
			{
				if (!__instance.pid.IsHumanPlayer)
				{
					if (!__instance.pid.IsAIPlayer)
					{
						DebugLog("H3", "allow_not_human");
						return true;
					}
					PlayerInfo aiPlayer = __instance.pid.FindPlayer();
					PlayerCrew aiCrew = aiPlayer?.crew;
					if (aiCrew == null)
					{
						DebugLog("H3", "allow_ai_no_crew");
						return true;
					}
					CrewAssignment aiAssignment = aiCrew.GetCrewForPeep(__instance.peepId);
					if (!aiAssignment.IsInVehicle)
					{
						DebugLog("H3", "allow_ai_not_in_vehicle");
						return true;
					}
					if (MultiCrewVehicleHelper.IsDriver(aiCrew, aiAssignment))
					{
						DebugLog("H3", "allow_ai_driver");
						return true;
					}
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ai-passenger-move-block",
						$"{aiAssignment.VehicleID.id}:{aiAssignment.peepId.id}:can-start",
						$"ai-passenger-move-block vehicle={aiAssignment.VehicleID.id} peep={aiAssignment.peepId.id} command=CanStart",
						dedupe: false);
					__result = Command.StartStatus.Failed;
					return false;
				}

				PlayerInfo player = __instance.pid.FindPlayer();
				PlayerCrew crew = player?.crew;
				if (crew == null)
				{
					DebugLog("H3", "allow_no_crew");
					return true;
				}

				if (!MultiCrewVehicleHelper.TryNormalizeHumanVehicleCommandDriver(__instance, crew, out _, out CrewAssignment assignment, out _))
				{
					DebugLog("H3", "allow_missing_command_peep");
					return true;
				}
				if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
				{
					DebugLog("H3", "allow_not_in_vehicle");
					return true;
				}

				if (MultiCrewVehicleHelper.IsDriver(crew, assignment))
				{
					DebugLog("H3", "allow_is_driver");
					return true;
				}

				DebugLog("H3", "block_non_driver");
				try { MultiCrewVehicleHelper.ShowHudMessage("Only the driver can move the car."); } catch (TypeLoadException) { } catch (ReflectionTypeLoadException) { }
				__result = Command.StartStatus.Failed;
				return false;
			}
			catch (Exception ex)
			{
				// #region agent log
				DebugLog("H1", "catch_entered", new Dictionary<string, object> { ["exType"] = ex.GetType().Name });
				// #endregion
				Debug.LogError("[GameplayTweaks] CommandGotoDriverGuardPatch error: " + ex.Message);
				// H1: avoid ShowHudMessage in catch so we don't trigger a second TypeLoadException
				// MultiCrewVehicleHelper.ShowHudMessage("Only the driver can move the car.");
				__result = Command.StartStatus.Failed;
				// #region agent log
				DebugLog("H1", "catch_before_return");
				// #endregion
				return false;
			}
		}

		[HarmonyPrefix]
		internal static bool StartDrivingCarPrefix(CommandGoto __instance, ref bool __result)
		{
			try
			{
				if (MultiCrewVehicleHelper.TryGetOwnedQueuedHumanVehicleRoute(__instance, out EntityID staleVehicleId, out NodeID staleGoalNodeId))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"route-stale-command-skip",
						$"{staleVehicleId.id}:{__instance.peepId.id}:{staleGoalNodeId}:start-driving",
						$"route-stale-command-skip vehicle={staleVehicleId.id} peep={__instance.peepId.id} command=StartDrivingCar finalGoal={staleGoalNodeId}",
						dedupe: false);
					__result = false;
					return false;
				}
				var pathField = AccessTools.Field(typeof(CommandGoto), "_path");
				if (!__instance.pid.IsHumanPlayer)
				{
					if (MultiCrewVehicleHelper.TryShouldSkipAiQueuedVehicleCommand(__instance, out EntityID staleAiVehicleId, out string staleAiReason))
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"ai-route-stale-command-skip",
							$"{staleAiVehicleId.id}:{__instance.peepId.id}:{staleAiReason}:start-driving",
							$"ai-route-stale-command-skip vehicle={staleAiVehicleId.id} peep={__instance.peepId.id} command=StartDrivingCar reason={staleAiReason}",
							dedupe: false);
						pathField?.SetValue(__instance, null);
						__result = false;
						return false;
					}
					if (MultiCrewVehicleHelper.TryGetAiVehicleCommandContext(__instance, out _, out _, out _, out CrewAssignment aiAssignment, out _))
					{
						PlayerCrew aiCrew = aiAssignment.GetPeep()?.data?.agent?.pid.FindPlayer()?.crew;
						if (aiCrew != null && !MultiCrewVehicleHelper.IsDriver(aiCrew, aiAssignment))
						{
							MultiCrewVehicleHelper.LogVehicleAuthority(
								"ai-passenger-move-block",
								$"{aiAssignment.VehicleID.id}:{aiAssignment.peepId.id}:start-driving",
								$"ai-passenger-move-block vehicle={aiAssignment.VehicleID.id} peep={aiAssignment.peepId.id} command=StartDrivingCar",
								dedupe: false);
							pathField?.SetValue(__instance, null);
							__result = false;
							return false;
						}
						PathData aiPath = pathField?.GetValue(__instance) as PathData;
						Node aiStartNode = null;
						string aiStartSource = "none";
						MultiCrewVehicleHelper.TryGetBestAiVehicleStartNode(aiAssignment.VehicleID, out aiStartNode, out aiStartSource);
						Node aiReachedNode = null;
						if (aiPath != null && aiPath.world.Count > 1)
						{
							WorldPos aiReachedPos = aiPath.world[aiPath.world.Count - 1];
							aiReachedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(aiReachedPos, 5f);
						}
						if (aiStartNode != null && aiReachedNode != null && MultiCrewVehicleHelper.ShouldBlockAiImmediateReverse(aiAssignment.VehicleID, aiStartNode.id, aiReachedNode.id))
						{
							pathField?.SetValue(__instance, null);
							__result = false;
							return false;
						}
						if (aiStartNode != null && aiReachedNode != null)
						{
							MultiCrewVehicleHelper.RecordAiVehicleIssuedMove(aiAssignment.VehicleID, aiStartNode.id, aiReachedNode.id);
							MultiCrewVehicleHelper.TrySyncVehicleOccupantsToVehicleNode(aiCrew, aiAssignment.VehicleID, "ai-move-start");
						}
					}
					return true;
				}
				if (!MultiCrewVehicleHelper.TryGetHumanVehicleCommandContext(__instance, out PlayerInfo player, out PlayerCrew crew, out Entity peep, out CrewAssignment assignment, out Entity vehicle))
					return true;
				if (!MultiCrewVehicleHelper.IsDriver(crew, assignment))
				{
					__result = false;
					return false;
				}
				if (MultiCrewVehicleHelper.TryGetHumanVehicleTravelConflict(assignment.VehicleID, __instance, out int activeCommandKey, out int blockedCommandKey, out bool sameOwner))
				{
					bool interrupted = MultiCrewVehicleHelper.TryInterruptActiveHumanVehicleTravelForImmediateMove(crew, vehicle, assignment.VehicleID, __instance, "start-driving");
					if (interrupted)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"start-driving-interrupt",
							$"{assignment.VehicleID.id}:{activeCommandKey}:{blockedCommandKey}",
							$"start-driving-interrupt-active vehicle={assignment.VehicleID.id} activeCommand={activeCommandKey} replacementCommand={blockedCommandKey} sameOwner={sameOwner}",
							dedupe: false);
					}
					if (!interrupted)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"start-driving-blocked",
							$"{assignment.VehicleID.id}:{activeCommandKey}:{blockedCommandKey}",
						$"start-driving-blocked-active vehicle={assignment.VehicleID.id} activeCommand={activeCommandKey} blockedCommand={blockedCommandKey} sameOwner={sameOwner}",
							dedupe: false);
						__result = false;
						return false;
					}
				}
				PathData path = pathField?.GetValue(__instance) as PathData;
				if (path == null || path.world.Count <= 1)
				{
					MultiCrewVehicleHelper.ClearPendingHumanVehicleTravel(assignment.VehicleID, "empty-path");
					__result = false;
					return false;
				}
				Node startNode = null;
				string startSource = "none";
				NodeID peepNodeId = peep.components?.agent?.GetNode()?.id ?? peep.data?.agent?.nid ?? NodeID.INVALID;
				if (!MultiCrewVehicleHelper.TryGetResolvedHumanVehiclePathStartNode(assignment.VehicleID, peepNodeId, out startNode, out startSource, allowQueuedResumeRepair: true) || startNode == null)
				{
					startNode = path.nodes != null && path.nodes.Count > 0 ? path.nodes[0].node : null;
					startSource = startNode != null ? "path-origin-fallback" : "none";
				}
				WorldPos reachedPos = path.world[path.world.Count - 1];
				Node reachedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(reachedPos, 5f);
				if (reachedNode == null || reachedPos.IsZero)
				{
					MultiCrewVehicleHelper.ClearPendingHumanVehicleTravel(assignment.VehicleID, "invalid-reached-node");
					__result = false;
					return false;
				}
				bool instant = !player.IsHuman;
				foreach (PathNode node2 in path.nodes)
				{
					if (player.IsHuman && !node2.node.known.Get(PlayerID.HumanPlayer))
					{
						peep.components.agent.IncrementStat(CrewStats.NodeScouted, 1);
					}
					player.meetings.MarkNodeAsKnown(node2.node, expectedSeen: true, instant);
				}
				NodeID startNodeId = startNode?.id ?? NodeID.INVALID;
				MultiCrewVehicleHelper.MarkHumanVehicleTravelActive(assignment.VehicleID, assignment.peepId, MultiCrewVehicleHelper.GetCommandOwnerKey(__instance), startNodeId, reachedNode.id, __instance.goalID);
				MultiCrewVehicleHelper.MarkQueuedFinalGoalKnownForScopePreview(player, peep, assignment.VehicleID, startNode, reachedNode, __instance.goalID, "travel-start-preview-goal-known");
				if (reachedNode.id.IsValid)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"travel-logical-commit-deferred",
						$"{assignment.VehicleID.id}:{reachedNode.id}:{__instance.goalID}",
						$"travel-logical-commit-deferred vehicle={assignment.VehicleID.id} expectedNode={reachedNode.id} goalNode={__instance.goalID} reason=await-physical-arrival",
						dedupe: false);
				}
				NodeID previewFinalNodeId = reachedNode.id;
				MultiCrewVehicleHelper.TryGetHumanVehicleTravelDisplayFinalNodeId(assignment.VehicleID, reachedNode.id, out previewFinalNodeId, out string previewFinalSource);
				MultiCrewVehicleHelper.TryRefreshCrewHudStateAfterTravel(assignment.VehicleID, previewFinalNodeId);
				if (startNodeId.IsValid && __instance.goalID.IsValid && startNodeId != __instance.goalID)
				{
					MultiCrewVehicleHelper.HideCornerInfoIfShowingNode(startNodeId);
				}
				Node previewFinalNode = previewFinalNodeId.FindNode();
				if (startNode != null && previewFinalNode != null && startNodeId.IsValid && previewFinalNode.id.IsValid && startNodeId != previewFinalNode.id)
				{
					GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(startNode, previewFinalNode, "travel-start-preview", assignment.VehicleID);
				}
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"pre-drive",
					$"{assignment.VehicleID.id}:{assignment.peepId.id}:{startNodeId}:{reachedNode.id}:{__instance.goalID}",
					$"pre-drive-agent-skip vehicle={assignment.VehicleID.id} peep={assignment.peepId.id} startNode={startNodeId} reachedNode={reachedNode.id} goalNode={__instance.goalID} previewNode={previewFinalNodeId} previewSource={previewFinalSource}");
				global::Game.Game.ctx.transit.DriveOnPath(player.PID, vehicle, path);
				peep.components.agent.AddXP(XPSource.FromDriving);
				vehicle.components.mobile.UpdateHealthFrom(assignment, VehicleHealthSource.FromDriving);
				__result = reachedNode != __instance.goalID.FindNode();
				return false;
			}
			catch (Exception ex)
			{
				if (__instance != null && MultiCrewVehicleHelper.TryGetHumanVehicleCommandContext(__instance, out _, out _, out _, out CrewAssignment assignment, out _))
				{
					MultiCrewVehicleHelper.ClearPendingHumanVehicleTravel(assignment.VehicleID, "start-driving-error");
				}
				Debug.LogWarning("[GameplayTweaks] StartDrivingCarPrefix override failed: " + ex.Message);
				__result = false;
				return false;
			}
		}

	}

	/// <summary>
	/// After RefreshPanel: for CrewMuscle cards with crew in vehicle, show "Passengers:" then one line of initials (e.g. TL, HR, TW) in Extras above "In vehicle".
	/// Also shows Scout button for passengers (1 MP to reveal one adjacent node).
	/// </summary>
}
