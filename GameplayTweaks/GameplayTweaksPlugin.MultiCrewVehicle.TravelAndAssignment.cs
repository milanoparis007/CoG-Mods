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
using SomaSim.Util;
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
				CrewAssignment healActor = crew;
				bool isHumanVehicleHeal = !_isApplyingVehicleGroupHealing
					&& player?.IsHuman == true
					&& crew.IsInVehicle
					&& crew.VehicleID.IsValid;
				if (isHumanVehicleHeal
					&& !MultiCrewVehicleHelper.IsDriver(player.crew, crew))
				{
					if (!TryResolveVehicleHealDriver(player.crew, crew, out healActor))
					{
						MultiCrewVehicleHelper.ShowHudMessage(DriverOnlyHealText);
						GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-skip pid={pid.id} reason=driver-only peep={crew.peepId.id} vehicle={crew.VehicleID.id}");
						return false;
					}
				}

				string executionLocationSource = "none";
				if (isHumanVehicleHeal
					&& !TryIsAtVehicleHealingLocation(player, healActor, out executionLocationSource))
				{
					MultiCrewVehicleHelper.ShowHudMessage(Loc.Get("ui.command.heal.mo.loc"));
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-skip pid={pid.id} reason=not-at-owned-location peep={crew.peepId.id} driver={healActor.peepId.id} vehicle={crew.VehicleID.id}");
					return false;
				}

				if (isHumanVehicleHeal
					&& __instance != null
					&& TryGetVehicleHealingTargets(player.crew, healActor, out List<CrewAssignment> healingTargets))
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
						$"heal-vehicle-group pid={pid.id} vehicle={crew.VehicleID.id} requester={crew.peepId.id} driver={healActor.peepId.id} crew={string.Join(",", healingTargets.Select(item => item.peepId.id.ToString()))} source={executionLocationSource}");
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

			CrewAssignment driverCrew = crew;
			if (!MultiCrewVehicleHelper.IsDriver(humanCrew, crew)
				&& !TryResolveVehicleHealDriver(humanCrew, crew, out driverCrew))
			{
				status = status.Set(CommandEnabledStatus.DisabledOther, DriverOnlyHealText);
				return true;
			}

			if (!TryGetVehicleHealingTargets(humanCrew, crew, out List<CrewAssignment> healingTargets))
			{
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-validate-hidden vehicle={crew.VehicleID.id} crew={crew.peepId.id} reason=no-wounded-occupants");
				return false;
			}

			if (!TryIsAtVehicleHealingLocation(player, driverCrew, out string locationSource))
			{
				status = status.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.heal.mo.loc"));
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-validate-disabled vehicle={crew.VehicleID.id} crew={crew.peepId.id} reason=not-at-owned-location");
				return true;
			}

			Entity peep = driverCrew.GetPeep();
			if (peep?.components?.agent == null || !peep.components.agent.HasActionsRemaining)
			{
				status = status.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.fail.points"));
				return true;
			}

			status = status.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.heal.mo.loc"));
			GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-validate-enabled vehicle={crew.VehicleID.id} crew={crew.peepId.id} driver={driverCrew.peepId.id} targets={healingTargets.Count} source={locationSource}");
			return true;
		}

		internal static bool TryResolveVehicleHealDriver(PlayerCrew crew, CrewAssignment occupant, out CrewAssignment driverCrew)
		{
			driverCrew = CrewAssignment.EMPTY;
			if (crew == null || !occupant.IsValid || !occupant.IsInVehicle || !occupant.VehicleID.IsValid)
			{
				return false;
			}

			EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(crew, occupant.VehicleID);
			if (!driverPeepId.IsValid)
			{
				return false;
			}

			driverCrew = crew.GetCrewForPeep(driverPeepId);
			return driverCrew.IsValid
				&& driverCrew.IsInVehicle
				&& driverCrew.VehicleID == occupant.VehicleID
				&& MultiCrewVehicleHelper.IsActiveVehicleOccupant(crew, driverCrew);
		}

		internal static bool CanStartVehicleGroupHeal(PlayerID pid, EntityID peepId)
		{
			return TryGetVehicleGroupHealActor(pid, peepId, out _, out _);
		}

		internal static bool TryGetVehicleGroupHealActor(PlayerID pid, EntityID peepId, out CrewAssignment requesterCrew, out CrewAssignment driverCrew)
		{
			requesterCrew = CrewAssignment.EMPTY;
			driverCrew = CrewAssignment.EMPTY;
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

			requesterCrew = humanCrew.GetCrewForPeep(peepId);
			if (!requesterCrew.IsValid || !requesterCrew.IsInVehicle || !requesterCrew.VehicleID.IsValid)
			{
				return false;
			}

			driverCrew = requesterCrew;
			if (!MultiCrewVehicleHelper.IsDriver(humanCrew, requesterCrew)
				&& !TryResolveVehicleHealDriver(humanCrew, requesterCrew, out driverCrew))
			{
				return false;
			}

			return driverCrew.IsValid
				&& driverCrew.IsInVehicle
				&& driverCrew.VehicleID == requesterCrew.VehicleID
				&& TryGetVehicleHealingTargets(humanCrew, driverCrew, out _)
				&& TryIsAtVehicleHealingLocation(player, driverCrew, out _);
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
				if (MultiCrewVehicleHelper.IsHumanVehicleRouteSimAccessNode(crew.VehicleID, nodeId, "heal", out string routeSimSource))
				{
					if (!TryCanReachVehicleHealingNodeThisTurn(player, crew, nodeId, out string reachReason, out int movesRemaining, out Fixnum routeCost, out NodeID lastNodeId))
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"route-sim-heal-blocked",
							$"{crew.VehicleID.id}:{crew.peepId.id}:{nodeId}:{routeSimSource}:{reachReason}",
							$"route-sim-heal-blocked vehicle={crew.VehicleID.id} crew={crew.peepId.id} node={nodeId} source={routeSimSource} reason={reachReason} moves={movesRemaining} cost={routeCost} lastNode={lastNodeId}",
							dedupe: true);
						continue;
					}

					source = $"{nodeId}:{routeSimSource}";
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"route-sim-heal",
						$"{crew.VehicleID.id}:{crew.peepId.id}:{nodeId}:{routeSimSource}",
						$"route-sim-heal vehicle={crew.VehicleID.id} crew={crew.peepId.id} node={nodeId} source={routeSimSource} reason=reachable moves={movesRemaining} cost={routeCost}",
						dedupe: true);
					return true;
				}
			}

			return false;
		}

		private static bool TryCanReachVehicleHealingNodeThisTurn(PlayerInfo player, CrewAssignment crew, NodeID nodeId, out string reason, out int movesRemaining, out Fixnum routeCost, out NodeID lastNodeId)
		{
			reason = "none";
			movesRemaining = 0;
			routeCost = Fixnum.ZERO;
			lastNodeId = NodeID.INVALID;
			try
			{
				if (player == null || !crew.IsValid || !crew.peepId.IsValid || !nodeId.IsValid)
				{
					reason = "missing-context";
					return false;
				}

				Entity peep = crew.GetPeep();
				Node targetNode = nodeId.FindNode();
				if (peep?.components?.agent == null || targetNode == null)
				{
					reason = "missing-route-context";
					return false;
				}

				movesRemaining = peep.components.agent.MovesRemaining;
				if (movesRemaining <= 0)
				{
					reason = "no-moves";
					return false;
				}

				PathData path = CommandGoto.MakePath(player.PID, peep, targetNode, new Fixnum(movesRemaining));
				if (path == null || path.world == null || path.world.Count <= 1 || path.nodes == null || path.nodes.Count == 0)
				{
					reason = $"no-path-within-moves";
					return false;
				}

				routeCost = path.cost;
				lastNodeId = path.nodes.LastOrDefault()?.node?.id ?? NodeID.INVALID;
				if (lastNodeId == nodeId && path.cost <= movesRemaining)
				{
					reason = "reachable";
					return true;
				}

				reason = "insufficient-moves";
				return false;
			}
			catch (Exception ex)
			{
				reason = "route-check-failed-" + ex.GetType().Name;
				return false;
			}
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
		[HarmonyPrefix]
		internal static bool ValidatePrefix(PlayerID __0, CrewAssignment __1, ref CommandStatus __result)
		{
			try
			{
				if (IsSafeVanillaHealValidationContext(__0, __1, out string reason))
				{
					return true;
				}

				__result = new CommandStatus(CommandType.Heal, Loc.Get("ui.command.heal.icon"));
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"heal-validate-stale-crew-blocked",
					$"{__0.id}:{__1.peepId.id}:{reason}",
					$"heal-validate-stale-crew-blocked pid={__0.id} crew={__1.peepId.id} reason={reason}",
					dedupe: true);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonHealDriverPatch.ValidatePrefix: " + ex.Message);
				return true;
			}
		}

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

		private static bool IsSafeVanillaHealValidationContext(PlayerID pid, CrewAssignment crew, out string reason)
		{
			reason = "none";
			if (!pid.IsHumanPlayer)
			{
				return true;
			}

			if (!crew.IsValid || !crew.peepId.IsValid)
			{
				reason = "invalid-crew";
				return false;
			}

			PlayerInfo player = pid.FindPlayer();
			if (player?.territory == null)
			{
				reason = "missing-player-territory";
				return false;
			}

			Entity peep = crew.GetPeep();
			if (peep?.components?.agent == null)
			{
				reason = "missing-agent";
				return false;
			}

			Node node = peep.components.agent.GetNode();
			if (node == null || !node.id.IsValid)
			{
				reason = "missing-node";
				return false;
			}

			return true;
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

				if (CombatManagerHealingGuardPatch.TryResolveVehicleHealDriver(humanCrew, crew, out CrewAssignment driverCrew)
					&& CombatManagerHealingGuardPatch.TryBuildVehicleHealStatus(PlayerID.HumanPlayer, crew, out CommandStatus status)
					&& status.IsEnabled)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-post-redirect vehicle={crew.VehicleID.id} requester={crew.peepId.id} driver={driverCrew.peepId.id}");
					HumanCommandValidator.PostCommandHelper(new CommandHeal(PlayerID.HumanPlayer, driverCrew.peepId));
					return false;
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
				if (__instance == null
					|| !CombatManagerHealingGuardPatch.TryGetVehicleGroupHealActor(__instance.pid, __instance.peepId, out CrewAssignment requesterCrew, out CrewAssignment driverCrew))
				{
					return true;
				}

				if (__instance.peepId != driverCrew.peepId)
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"heal-canstart-retarget vehicle={requesterCrew.VehicleID.id} requester={requesterCrew.peepId.id} driver={driverCrew.peepId.id}");
					__instance.peepId = driverCrew.peepId;
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
				HumanVehicleCommandQueueDriverPatch.NormalizeQueuedVehicleDriverCommands(__instance, "turn-start");
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

	internal static class HumanVehicleCommandQueueDriverPatch
	{
		[HarmonyPrefix]
		internal static void HandleAddingCommandPrefix(CommandExecutor __instance, Command cmd)
		{
			try
			{
				RetargetVehicleDriverCommand(__instance?.PlayerInfo, cmd, "add-command");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleCommandQueueDriverPatch.HandleAddingCommand: " + ex.Message);
			}
		}

		internal static void NormalizeQueuedVehicleDriverCommands(CommandExecutor executor, string source)
		{
			try
			{
				PlayerInfo player = executor?.PlayerInfo;
				PlayerCrew crew = player?.crew;
				if (executor?.PID.IsHumanPlayer != true || crew == null || executor.tuples == null)
				{
					return;
				}

				for (int i = executor.tuples.Count - 1; i >= 0; i--)
				{
					CommandExecutor.CommandTuple tuple = executor.tuples[i];
					if (tuple == null || tuple.markedForRemoval)
					{
						continue;
					}

					EntityID originalTuplePeepId = tuple.peepId;
					EntityID driverPeepId = EntityID.INVALID;
					bool changed = false;
					if (tuple.active != null && RetargetVehicleDriverCommand(player, tuple.active, source, out driverPeepId))
					{
						changed = true;
					}
					if (tuple.queue != null)
					{
						foreach (Command queuedCommand in tuple.queue)
						{
							if (RetargetVehicleDriverCommand(player, queuedCommand, source, out EntityID commandDriverPeepId))
							{
								changed = true;
								driverPeepId = commandDriverPeepId;
							}
						}
					}

					if (!changed || !driverPeepId.IsValid || originalTuplePeepId == driverPeepId)
					{
						continue;
					}

					CommandExecutor.CommandTuple driverTuple = executor.tuples
						.FirstOrDefault(item => item != null && item != tuple && !item.markedForRemoval && item.peepId == driverPeepId);
					if (driverTuple == null)
					{
						tuple.peepId = driverPeepId;
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-queue-driver-tuple-retarget",
							$"{originalTuplePeepId.id}:{driverPeepId.id}:{source}",
							$"command-queue-driver-tuple-retarget fromPeep={originalTuplePeepId.id} toPeep={driverPeepId.id} source={source}",
							dedupe: false);
						continue;
					}

					if (tuple.active != null)
					{
						if (driverTuple.active == null)
						{
							driverTuple.active = tuple.active;
						}
						else
						{
							driverTuple.queue.Insert(0, tuple.active);
						}
						tuple.active = null;
					}
					if (tuple.queue != null && tuple.queue.Count > 0)
					{
						driverTuple.queue.AddRange(tuple.queue);
						tuple.queue.Clear();
					}
					tuple.markedForRemoval = true;
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"command-queue-driver-tuple-merged",
						$"{originalTuplePeepId.id}:{driverPeepId.id}:{source}",
						$"command-queue-driver-tuple-merged fromPeep={originalTuplePeepId.id} toPeep={driverPeepId.id} source={source}",
						dedupe: false);
				}

				executor.tuples.RemoveAll(tuple => tuple == null || tuple.markedForRemoval);
				executor.tuples.Sort((left, right) => left.peepId.index - right.peepId.index);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleCommandQueueDriverPatch.NormalizeQueuedVehicleDriverCommands: " + ex.Message);
			}
		}

		private static bool RetargetVehicleDriverCommand(PlayerInfo player, Command command, string source)
		{
			return RetargetVehicleDriverCommand(player, command, source, out _);
		}

		private static bool RetargetVehicleDriverCommand(PlayerInfo player, Command command, string source, out EntityID driverPeepId)
		{
			driverPeepId = EntityID.INVALID;
			if (player?.PID.IsHumanPlayer != true || player.crew == null || command == null || !ShouldRouteThroughVehicleDriver(command))
			{
				return false;
			}
			if (!command.pid.IsHumanPlayer || !command.peepId.IsValid)
			{
				return false;
			}

			CrewAssignment assignment = player.crew.GetCrewForPeep(command.peepId);
			if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return false;
			}
			if (MultiCrewVehicleHelper.IsDriver(player.crew, assignment))
			{
				driverPeepId = assignment.peepId;
				return false;
			}

			driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(player.crew, assignment.VehicleID);
			CrewAssignment driverAssignment = driverPeepId.IsValid ? player.crew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
			if (!driverAssignment.IsValid
				|| !driverAssignment.IsInVehicle
				|| driverAssignment.VehicleID != assignment.VehicleID
				|| !MultiCrewVehicleHelper.IsActiveVehicleOccupant(player.crew, driverAssignment))
			{
				return false;
			}

			EntityID originalPeepId = command.peepId;
			command.peepId = driverPeepId;
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"command-queue-driver-retarget",
				$"{assignment.VehicleID.id}:{originalPeepId.id}:{driverPeepId.id}:{command.GetType().Name}:{source}",
				$"command-queue-driver-retarget vehicle={assignment.VehicleID.id} command={command.GetType().Name} fromPeep={originalPeepId.id} toPeep={driverPeepId.id} source={source}",
				dedupe: false);
			return true;
		}

		private static bool ShouldRouteThroughVehicleDriver(Command command)
		{
			return command is CommandGoto || command is CommandAutomationStep;
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

				MultiCrewVehicleHelper.TryResumeQueuedHumanVehicleRoutes(__instance.PlayerInfo, EntityID.INVALID, "turn-start-immediate");
				MultiCrewVehicleHelper.QueueDeferredQueuedHumanVehicleRouteResume(__instance.PlayerInfo, "turn-start-fallback");
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

	internal static class SelectionManagerStaleEntityPatch
	{
		private static int _lastLoggedFrame = -9999;

		[HarmonyFinalizer]
		internal static Exception SetActiveFinalizer(SelectionManager __instance, Entity e, Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}
			if (!(__exception is NullReferenceException))
			{
				return __exception;
			}

			int frame = Time.frameCount;
			if (frame - _lastLoggedFrame >= 180)
			{
				_lastLoggedFrame = frame;
				ulong targetId = 0UL;
				ulong currentId = 0UL;
				try
				{
					targetId = e?.Id.id ?? 0UL;
					currentId = __instance?.CurrentActive?.Id.id ?? 0UL;
				}
				catch
				{
				}
				GameplayTweaksPlugin.VerificationLog("Selection", $"set-active-null-suppressed target={targetId} current={currentId}");
			}
			return null;
		}
	}

	internal static class CarInputModeDriverPeepPatch
	{
		private static readonly FieldInfo CarField = AccessTools.Field(typeof(CarInputMode), "_car");
		private static readonly FieldInfo PeepField = AccessTools.Field(typeof(CarInputMode), "_peep");
		private static readonly FieldInfo TargetNodeField = AccessTools.Field(typeof(CarInputMode), "_targetNode");
		private static int _lastStaleUpdateLineLogFrame = -9999;

		[HarmonyPostfix]
		internal static void ConstructorPostfix(CarInputMode __instance, Entity car)
		{
			RefreshDriverPeep(__instance, car, "ctor");
		}

		[HarmonyPrefix]
		internal static bool UpdateLinePrefix(CarInputMode __instance)
		{
			Entity car = CarField?.GetValue(__instance) as Entity;
			RefreshDriverPeep(__instance, car, "update-line");
			Entity peep = PeepField?.GetValue(__instance) as Entity;
			Node targetNode = TargetNodeField?.GetValue(__instance) as Node;
			if (targetNode == null)
			{
				return false;
			}
			if (car?.data?.mobile == null || peep?.components?.agent == null || global::Game.Game.ctx?.transit == null)
			{
				LogStaleUpdateLineSuppressed(car, peep, targetNode, "missing-car-peep-or-transit");
				return false;
			}
			return true;
		}

		[HarmonyFinalizer]
		internal static Exception UpdateLineFinalizer(CarInputMode __instance, Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}
			if (!(__exception is NullReferenceException))
			{
				return __exception;
			}
			Entity car = CarField?.GetValue(__instance) as Entity;
			Entity peep = PeepField?.GetValue(__instance) as Entity;
			Node targetNode = TargetNodeField?.GetValue(__instance) as Node;
			LogStaleUpdateLineSuppressed(car, peep, targetNode, "finalizer-nullref");
			return null;
		}

		private static void LogStaleUpdateLineSuppressed(Entity car, Entity peep, Node targetNode, string reason)
		{
			int frame = Time.frameCount;
			if (frame - _lastStaleUpdateLineLogFrame < 180)
			{
				return;
			}
			_lastStaleUpdateLineLogFrame = frame;
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"car-input-update-line-null-suppressed",
				$"{car?.Id.id ?? 0UL}:{peep?.Id.id ?? 0UL}:{targetNode?.id.index ?? -1}:{reason}",
				$"car-input-update-line-null-suppressed vehicle={car?.Id.id ?? 0UL} peep={peep?.Id.id ?? 0UL} targetNode={targetNode?.id.ToString() ?? "none"} reason={reason}",
				dedupe: false);
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
		private static readonly Dictionary<CommandGoto, int> HumanVehiclePathBuiltFrameByCommand = new Dictionary<CommandGoto, int>();
		private static readonly bool EnableRoutineCommandPathLog =
			string.Equals(Environment.GetEnvironmentVariable("COG_VERBOSE_VEHICLE_AUTHORITY"), "1", StringComparison.Ordinal);

		internal static void ClearCachedPathFrame(CommandGoto command)
		{
			if (command != null)
			{
				HumanVehiclePathBuiltFrameByCommand.Remove(command);
			}
		}

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
				bool interruptedActiveRoute = false;
				if (MultiCrewVehicleHelper.TryGetHumanVehicleTravelConflict(assignment.VehicleID, __instance, out int activeCommandKey, out int blockedCommandKey, out bool sameOwner))
				{
					if (MultiCrewVehicleHelper.ShouldDiscardDuplicateHumanVehicleRouteCommand(assignment.VehicleID, __instance, out NodeID duplicateExpectedNodeId, out NodeID duplicateGoalNodeId, out NodeID duplicateCommandGoalId))
					{
						CommandGotoPathField?.SetValue(__instance, null);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-path-duplicate-discarded",
							$"{assignment.VehicleID.id}:{duplicateExpectedNodeId}:{duplicateGoalNodeId}:{duplicateCommandGoalId}",
							$"command-path-duplicate-discarded vehicle={assignment.VehicleID.id} expectedNode={duplicateExpectedNodeId} finalGoal={duplicateGoalNodeId} commandGoal={duplicateCommandGoalId} reason=already-routed-await-arrival",
							dedupe: true);
						__result = Command.StartStatus.Failed;
						return false;
					}
					if (MultiCrewVehicleHelper.ShouldDeferHumanVehicleRouteCommandUntilArrival(assignment.VehicleID, __instance, out NodeID expectedNodeId, out NodeID commandGoalId))
					{
						CommandGotoPathField?.SetValue(__instance, null);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-path-deferred",
							$"{assignment.VehicleID.id}:{expectedNodeId}:{commandGoalId}",
							$"command-path-deferred vehicle={assignment.VehicleID.id} expectedNode={expectedNodeId} commandGoal={commandGoalId} reason=await-active-arrival",
							dedupe: true);
						__result = Command.StartStatus.SkipThisTurn;
						return false;
					}
					bool interrupted = MultiCrewVehicleHelper.IsDriver(crew, assignment)
						&& MultiCrewVehicleHelper.TryInterruptActiveHumanVehicleTravelForImmediateMove(crew, vehicle, assignment.VehicleID, __instance, "command-path");
					interruptedActiveRoute = interrupted;
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
				PathData existingPath = CommandGotoPathField?.GetValue(__instance) as PathData;
				if (existingPath != null
					&& existingPath.world != null
					&& existingPath.world.Count > 1
					&& HumanVehiclePathBuiltFrameByCommand.TryGetValue(__instance, out int builtFrame)
					&& builtFrame == Time.frameCount)
				{
					__result = Command.StartStatus.OK;
					return false;
				}
				if (!MultiCrewVehicleHelper.TryBuildHumanVehicleCommandPath(__instance, out PathData path, out Node startNode, out string source))
				{
					if (interruptedActiveRoute)
					{
						CommandGotoPathField?.SetValue(__instance, null);
						HumanVehiclePathBuiltFrameByCommand.Remove(__instance);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"command-path-interrupt-build-failed",
							$"{assignment.VehicleID.id}:{__instance.peepId.id}:{__instance.goalID}",
							$"command-path-interrupt-build-failed vehicle={assignment.VehicleID.id} peep={__instance.peepId.id} commandGoal={__instance.goalID} reason=no-resolved-path-after-interrupt",
							dedupe: false);
						__result = Command.StartStatus.SkipThisTurn;
						return false;
					}
					return true;
				}
				Node goalNode = __instance.goalID.FindNode();
				NodeID startNodeId = startNode?.id ?? NodeID.INVALID;
				NodeID goalNodeId = goalNode?.id ?? NodeID.INVALID;
				if (HumanVehicleTravelEndSyncPatch.ShouldDelayCommandForDeliveryPumpGuard(assignment.VehicleID, startNodeId, goalNodeId, out NodeID guardTargetNodeId, out string guardReason))
				{
					CommandGotoPathField?.SetValue(__instance, null);
					HumanVehiclePathBuiltFrameByCommand.Remove(__instance);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"delivery-route-command-deferred",
						$"{assignment.VehicleID.id}:{guardTargetNodeId}:{startNodeId}:{goalNodeId}:set-path",
						$"delivery-route-command-deferred vehicle={assignment.VehicleID.id} peep={__instance.peepId.id} guardTarget={guardTargetNodeId} startNode={startNodeId} goalNode={goalNodeId} reason={guardReason}",
						dedupe: true);
					__result = Command.StartStatus.SkipThisTurn;
					return false;
				}
				CommandGotoPathField?.SetValue(__instance, path);
				if (path != null && path.world != null && path.world.Count > 1)
				{
					HumanVehiclePathBuiltFrameByCommand[__instance] = Time.frameCount;
				}
				else
				{
					HumanVehiclePathBuiltFrameByCommand.Remove(__instance);
				}
				if (EnableRoutineCommandPathLog)
				{
					string costText = path != null ? path.cost.ToString() : "invalid";
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"command-path",
						$"{assignment.VehicleID.id}:{startNodeId}:{goalNodeId}:{costText}",
						$"command-path-start vehicle={assignment.VehicleID.id} peep={__instance.peepId.id} startNode={startNodeId} goalNode={goalNodeId} source={source} cost={costText} movesBefore={movesRemaining} actionsBefore={peep.components.agent.ActionsRemaining}");
				}
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
		private static readonly MethodInfo CommandExecutorProcessCommandQueueMethod = AccessTools.Method(typeof(CommandExecutor), "ProcessCommandQueue");
		private static readonly MethodInfo AutomationExecutorRunAutomationStepMethod = AccessTools.Method(typeof(AutomationExecutor), "RunAutomationStep");
		private static readonly Dictionary<long, int> DeliveryPumpSegmentStartFrameByVehicleId = new Dictionary<long, int>();
		private static readonly Dictionary<long, NodeID> DeliveryPumpSegmentTargetByVehicleId = new Dictionary<long, NodeID>();
		private static readonly Dictionary<long, DeliveryManualRouteStop> ManualDeliveryStopByVehicleId = new Dictionary<long, DeliveryManualRouteStop>();

		private sealed class DeliveryManualRouteStop
		{
			public NodeID StopNodeId;
			public NodeID AutomationTargetNodeId;
			public AutomationID SequenceId;
			public int NextStep;
			public int Frame;
		}

		[HarmonyPostfix]
		internal static void Postfix(ActionNavigate __instance)
		{
			Entity vehicle = null;
			NodeID finalNodeId = NodeID.INVALID;
			Node startNode = null;
			bool finalizedTravelState = false;
			bool routeQueued = false;
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
				MultiCrewVehicleHelper.TryFinalizeHumanVehicleTravelStop(crew, vehicle, out finalNodeId, out routeQueued);
				finalizedTravelState = true;
				TryRefreshAfterHumanVehicleTravelFinalize(vehicle, startNode, finalNodeId);
				TryContinueDeliveryAutomationAfterTravel(vehicle, crew, finalNodeId, routeQueued);
				TryResumeRouteAfterDeferredTurnStartArrival(vehicle, finalNodeId, routeQueued);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleTravelEndSyncPatch failed " + FormatTravelEndSyncContext(vehicle, finalNodeId, routeQueued, finalizedTravelState) + " ex=" + ex.GetType().Name + ": " + ex.Message);
			}
			finally
			{
				if (vehicle != null && !finalizedTravelState)
				{
					MultiCrewVehicleHelper.CompleteHumanVehicleTravelSegment(vehicle.Id, finalNodeId, out _);
				}
			}
		}

		private static void TryRefreshAfterHumanVehicleTravelFinalize(Entity vehicle, Node startNode, NodeID finalNodeId)
		{
			try
			{
				if (vehicle?.Id.IsValid != true)
				{
					return;
				}

				if (finalNodeId.IsValid)
				{
					NodeID displayFinalNodeId = finalNodeId;
					MultiCrewVehicleHelper.TryGetHumanVehicleTravelDisplayFinalNodeId(vehicle.Id, finalNodeId, out displayFinalNodeId, out _);
					MultiCrewVehicleHelper.TryRefreshCrewHudStateAfterTravel(vehicle.Id, displayFinalNodeId);
					GameplayTweaksPlugin.QueueDeferredSelectedVehicleUiRefresh(startNode, finalNodeId.FindNode(), "travel-finalize", vehicle.Id);
				}
				else
				{
					GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(startNode, null, "travel-finalize", vehicle.Id);
				}
			}
			catch (Exception ex)
			{
				ulong vehicleId = vehicle?.Id.id ?? 0UL;
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"travel-finalize-ui-refresh-failed",
					$"{vehicleId}:{startNode?.id ?? NodeID.INVALID}:{finalNodeId}:{ex.GetType().Name}",
					$"travel-finalize-ui-refresh-failed vehicle={vehicleId} startNode={startNode?.id ?? NodeID.INVALID} finalNode={finalNodeId} ex={ex.GetType().Name}: {ex.Message}",
					dedupe: false);
			}
		}

		private static void TryResumeRouteAfterDeferredTurnStartArrival(Entity vehicle, NodeID finalNodeId, bool routeQueued)
		{
			try
			{
				if (vehicle?.Id.IsValid != true || !finalNodeId.IsValid)
				{
					return;
				}

				bool hasQueuedResume = routeQueued || MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicle.Id);
				if (!hasQueuedResume)
				{
					return;
				}
				bool consumedDeferredArrival = MultiCrewVehicleHelper.TryConsumeTurnStartDeferredHumanVehicleArrival(vehicle.Id, "travel-end");
				if (!consumedDeferredArrival && !routeQueued)
				{
					return;
				}

				PlayerInfo player = vehicle.data?.mobile?.pid.FindPlayer();
				if (player?.PID.IsHumanPlayer != true)
				{
					return;
				}
				PlayerID currentPlayer = PlayerID.INVALID;
				if (global::Game.Game.ctx?.clock != null)
				{
					currentPlayer = global::Game.Game.ctx.clock.CurrentPlayer;
				}
				if (!currentPlayer.IsHumanPlayer)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"route-resume-after-deferred-arrival-skipped",
						$"{vehicle.Id.id}:{finalNodeId}:{currentPlayer}",
						$"route-resume-after-deferred-arrival-skipped vehicle={vehicle.Id.id} finalNode={finalNodeId} currentPlayer={currentPlayer} reason=not-human-turn",
						dedupe: false);
					return;
				}

				MultiCrewVehicleHelper.LogVehicleAuthority(
					consumedDeferredArrival ? "route-resume-after-deferred-arrival" : "route-resume-after-segment-arrival",
					$"{vehicle.Id.id}:{finalNodeId}:{consumedDeferredArrival}",
					consumedDeferredArrival
						? $"route-resume-after-deferred-arrival vehicle={vehicle.Id.id} finalNode={finalNodeId} reason=turnstart-arrival-deferred"
						: $"route-resume-after-segment-arrival vehicle={vehicle.Id.id} finalNode={finalNodeId} reason=same-turn-queued-destination",
					dedupe: false);
				MultiCrewVehicleHelper.TryResumeQueuedHumanVehicleRoutes(player, vehicle.Id, consumedDeferredArrival ? "deferred-arrival" : "segment-arrival");
			}
			catch (Exception ex)
			{
				ulong vehicleId = vehicle?.Id.id ?? 0UL;
				Debug.LogWarning("[GameplayTweaks] TryResumeRouteAfterDeferredTurnStartArrival failed vehicle=" + vehicleId + " ex=" + ex.GetType().Name + ": " + ex.Message);
			}
		}

		private static string FormatTravelEndSyncContext(Entity vehicle, NodeID finalNodeId, bool routeQueued, bool finalizedTravelState)
		{
			try
			{
				EntityID vehicleId = vehicle?.Id ?? EntityID.INVALID;
				if (!vehicleId.IsValid)
				{
					return $"vehicle=0 finalNode={finalNodeId} routeQueued={routeQueued} finalized={finalizedTravelState} pending=none active=False queued=False";
				}

				string pending = "none";
				if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(vehicleId, out EntityID pendingPeepId, out NodeID expectedNodeId, out NodeID goalNodeId))
				{
					NodeID startNodeId = NodeID.INVALID;
					MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravelStartNode(vehicleId, out startNodeId);
					pending = $"peep={pendingPeepId.id},start={startNodeId},expected={expectedNodeId},goal={goalNodeId}";
				}

				return $"vehicle={vehicleId.id} finalNode={finalNodeId} routeQueued={routeQueued} finalized={finalizedTravelState} pending={pending} active={MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId)} queued={MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId)}";
			}
			catch (Exception contextEx)
			{
				return $"vehicle={(vehicle?.Id.id ?? 0UL)} finalNode={finalNodeId} routeQueued={routeQueued} finalized={finalizedTravelState} contextFailed={contextEx.GetType().Name}";
			}
		}

		private static void TryContinueDeliveryAutomationAfterTravel(Entity vehicle, PlayerCrew crew, NodeID finalNodeId, bool routeQueued)
		{
			try
			{
				if (vehicle?.Id.IsValid != true || crew == null || !finalNodeId.IsValid)
				{
					return;
				}
				if (MultiCrewVehicleHelper.ShouldSkipForAfterProhibitionRoutesBehaviorOwner("delivery-pump", "travel-arrival"))
				{
					return;
				}
				PlayerInfo player = vehicle.data?.mobile?.pid.FindPlayer();
				if (player?.PID.IsHumanPlayer != true || player.automation == null || player.commands == null)
				{
					return;
				}
				EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(crew, vehicle.Id);
				CrewAssignment driverAssignment = driverPeepId.IsValid ? crew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
				if (!driverAssignment.IsValid
					|| !driverAssignment.IsInVehicle
					|| driverAssignment.VehicleID != vehicle.Id
					|| !MultiCrewVehicleHelper.IsActiveVehicleOccupant(crew, driverAssignment))
				{
					return;
				}
				AutomationSequence sequence = player.automation.GetAutoOrNull(driverAssignment);
				if (sequence == null || sequence.IsAutoNotActive)
				{
					return;
				}
				MultiCrewVehicleHelper.LogAfterProhibitionRoutesDeliveryPumpDecision(vehicle.Id, finalNodeId, "travel-arrival-start", 0, 0);
				if (TryConsumeManualDeliveryStop(vehicle.Id, finalNodeId, driverPeepId, sequence))
				{
					return;
				}
				if (ShouldDelayDeliveryPumpAfterRecentSegmentStart(vehicle.Id, finalNodeId, driverPeepId, sequence))
				{
					return;
				}
				if (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicle.Id))
				{
					LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "active-route-before-pump", dedupeKey: "active-before");
					return;
				}
				if (routeQueued || MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicle.Id))
				{
					LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, $"queued-resume routeQueued={routeQueued}", dedupeKey: "queued-resume");
					return;
				}

				int queuePumps = 0;
				int automationPumps = 0;
				for (int attempts = 0; attempts < AutomationExecutor.MAX_ATTEMPTS_TO_SPEND_ALL_POINTS; attempts++)
				{
					if (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicle.Id))
					{
						LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "active-route-loop", dedupeKey: "active-loop");
						break;
					}
					if (player.commands.PeepHasTask(driverPeepId))
					{
						MultiCrewVehicleHelper.LogAfterProhibitionRoutesDeliveryPumpDecision(vehicle.Id, finalNodeId, "before-queue-pump", queuePumps, automationPumps);
						if (!TryProcessDeliveryCommandQueue(player.commands, driverPeepId, vehicle.Id, finalNodeId, "travel-arrival", ref queuePumps))
						{
							break;
						}
						if (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicle.Id))
						{
							RememberDeliveryPumpStartedSegment(vehicle.Id);
							LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "active-route-after-queue", dedupeKey: "active-after-queue");
							break;
						}
						if (MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicle.Id))
						{
							LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "queued-resume-after-queue", dedupeKey: "queue-pump");
							break;
						}
						continue;
					}

					if (!CommandAutomationStep.CanExecuteOneMore(driverAssignment))
					{
						break;
					}
					if (MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicle.Id))
					{
						LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "queued-resume-before-automation", dedupeKey: "before-automation");
						break;
					}
					if (AutomationExecutorRunAutomationStepMethod == null)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"delivery-route-pump-skip",
							$"{vehicle.Id.id}:{driverPeepId.id}:missing-run-step",
							$"delivery-route-pump-skip vehicle={vehicle.Id.id} peep={driverPeepId.id} node={finalNodeId} reason=missing-run-step",
							dedupe: true);
						break;
					}

					int movesBefore = driverPeepId.FindEntity()?.components?.agent?.MovesRemaining ?? -1;
					int actionsBefore = driverPeepId.FindEntity()?.components?.agent?.ActionsRemaining ?? -1;
					MultiCrewVehicleHelper.LogAfterProhibitionRoutesDeliveryPumpDecision(vehicle.Id, finalNodeId, "before-automation-pump", queuePumps, automationPumps);
					AutomationExecutorRunAutomationStepMethod.Invoke(player.automation, new object[] { driverAssignment, sequence });
					automationPumps++;
					MultiCrewVehicleHelper.LogAfterProhibitionRoutesDeliveryPumpDecision(vehicle.Id, finalNodeId, "after-automation-pump", queuePumps, automationPumps);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"delivery-route-automation-pump",
						$"{vehicle.Id.id}:{driverPeepId.id}:{finalNodeId}:{sequence.id.id}:{automationPumps}",
						$"delivery-route-automation-pump vehicle={vehicle.Id.id} peep={driverPeepId.id} node={finalNodeId} route={sequence.id.id} nextStep={sequence.nextstep} movesBefore={movesBefore} actionsBefore={actionsBefore} routeQueued={routeQueued}",
						dedupe: false);

					if (player.commands.PeepHasTask(driverPeepId))
					{
						TryProcessDeliveryCommandQueue(player.commands, driverPeepId, vehicle.Id, finalNodeId, "automation-pump", ref queuePumps);
					}
					if (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicle.Id))
					{
						RememberDeliveryPumpStartedSegment(vehicle.Id);
						LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "active-route-after-automation", dedupeKey: "active-after-automation");
						break;
					}
					if (MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicle.Id))
					{
						LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, "queued-resume-after-automation", dedupeKey: "after-automation");
						break;
					}
					LogDeliveryRoutePumpWait(vehicle.Id, driverPeepId, finalNodeId, sequence, $"automation-step-budget automationPumps={automationPumps}", dedupeKey: "automation-budget");
					break;
				}

				if (queuePumps > 0 || automationPumps > 0)
				{
					Entity driverPeep = driverPeepId.FindEntity();
					MultiCrewVehicleHelper.LogAfterProhibitionRoutesDeliveryPumpDecision(vehicle.Id, finalNodeId, "pump-summary", queuePumps, automationPumps);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"delivery-route-pump-summary",
						$"{vehicle.Id.id}:{driverPeepId.id}:{finalNodeId}:{queuePumps}:{automationPumps}",
						$"delivery-route-pump-summary vehicle={vehicle.Id.id} peep={driverPeepId.id} node={finalNodeId} route={sequence.id.id} queuePumps={queuePumps} automationPumps={automationPumps} movesAfter={driverPeep?.components?.agent?.MovesRemaining ?? -1} actionsAfter={driverPeep?.components?.agent?.ActionsRemaining ?? -1} routeActive={MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicle.Id)} routeQueued={MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicle.Id)}",
						dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] Delivery automation travel pump failed: " + ex.GetType().Name + ": " + ex.Message);
			}
		}

		internal static void MarkManualDeliveryStopIfNeeded(PlayerInfo player, CrewAssignment assignment, NodeID commandGoalNodeId, NodeID expectedNodeId, string source)
		{
			try
			{
				if (player?.PID.IsHumanPlayer != true
					|| player.automation == null
					|| !assignment.IsValid
					|| !assignment.IsInVehicle
					|| !assignment.VehicleID.IsValid)
				{
					return;
				}

				AutomationSequence sequence = player.automation.GetAutoOrNull(assignment);
				if (sequence == null || sequence.IsAutoNotActive)
				{
					return;
				}

				AutomationStep nextStep = sequence.GetNextStep();
				if (!TryResolveAutomationStepNode(nextStep, out NodeID automationTargetNodeId))
				{
					return;
				}

				if ((commandGoalNodeId.IsValid && commandGoalNodeId == automationTargetNodeId)
					|| (expectedNodeId.IsValid && expectedNodeId == automationTargetNodeId))
				{
					return;
				}

				long vehicleKey = (long)assignment.VehicleID.id;
				ManualDeliveryStopByVehicleId[vehicleKey] = new DeliveryManualRouteStop
				{
					StopNodeId = expectedNodeId.IsValid ? expectedNodeId : commandGoalNodeId,
					AutomationTargetNodeId = automationTargetNodeId,
					SequenceId = sequence.id,
					NextStep = sequence.nextstep,
					Frame = Time.frameCount
				};
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"delivery-route-manual-stop-marked",
					$"{assignment.VehicleID.id}:{assignment.peepId.id}:{commandGoalNodeId}:{expectedNodeId}:{automationTargetNodeId}",
					$"delivery-route-manual-stop-marked vehicle={assignment.VehicleID.id} peep={assignment.peepId.id} commandGoal={commandGoalNodeId} expectedNode={expectedNodeId} automationTarget={automationTargetNodeId} route={sequence.id.id} nextStep={sequence.nextstep} source={source}",
					dedupe: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] MarkManualDeliveryStopIfNeeded failed: " + ex.Message);
			}
		}

		private static bool TryConsumeManualDeliveryStop(EntityID vehicleId, NodeID finalNodeId, EntityID peepId, AutomationSequence sequence)
		{
			if (!vehicleId.IsValid || sequence == null)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			if (!ManualDeliveryStopByVehicleId.TryGetValue(vehicleKey, out DeliveryManualRouteStop stop))
			{
				return false;
			}

			if (stop.StopNodeId.IsValid && finalNodeId.IsValid && stop.StopNodeId != finalNodeId)
			{
				int staleFrameDelta = Math.Max(0, Time.frameCount - stop.Frame);
				if (staleFrameDelta > 120)
				{
					ManualDeliveryStopByVehicleId.Remove(vehicleKey);
				}
				return false;
			}

			ManualDeliveryStopByVehicleId.Remove(vehicleKey);
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"delivery-route-manual-stop-hold",
				$"{vehicleId.id}:{peepId.id}:{finalNodeId}:{sequence.id.id}:{sequence.nextstep}",
				$"delivery-route-manual-stop-hold vehicle={vehicleId.id} peep={peepId.id} node={finalNodeId} route={sequence.id.id} nextStep={sequence.nextstep} markedNextStep={stop.NextStep} automationTarget={stop.AutomationTargetNodeId} reason=manual-detour-suppress-same-arrival-pump",
				dedupe: false);
			return true;
		}

		private static bool TryResolveAutomationStepNode(AutomationStep step, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			Entity target = step?.target.FindEntity();
			if (target == null)
			{
				return false;
			}

			nodeId = target.data?.board?.bead.nodeId ?? NodeID.INVALID;
			return nodeId.IsValid;
		}

		private static void RememberDeliveryPumpStartedSegment(EntityID vehicleId)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			DeliveryPumpSegmentStartFrameByVehicleId[vehicleKey] = Time.frameCount;
			if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(vehicleId, out _, out NodeID expectedNodeId, out _)
				&& expectedNodeId.IsValid)
			{
				DeliveryPumpSegmentTargetByVehicleId[vehicleKey] = expectedNodeId;
			}
			else
			{
				DeliveryPumpSegmentTargetByVehicleId.Remove(vehicleKey);
			}
		}

		internal static void ClearDeliveryPumpSegmentGuard(EntityID vehicleId, string reason)
		{
			if (!vehicleId.IsValid)
			{
				return;
			}

			long vehicleKey = (long)vehicleId.id;
			bool removedFrame = DeliveryPumpSegmentStartFrameByVehicleId.Remove(vehicleKey);
			bool removedTarget = DeliveryPumpSegmentTargetByVehicleId.Remove(vehicleKey);
			if (removedFrame || removedTarget)
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"delivery-route-pump-guard-cleared",
					$"{vehicleId.id}:{reason}",
					$"delivery-route-pump-guard-cleared vehicle={vehicleId.id} reason={reason}",
					dedupe: false);
			}
		}

		private static bool ShouldDelayDeliveryPumpAfterRecentSegmentStart(EntityID vehicleId, NodeID finalNodeId, EntityID peepId, AutomationSequence sequence)
		{
			if (!vehicleId.IsValid || !finalNodeId.IsValid)
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			if (!DeliveryPumpSegmentStartFrameByVehicleId.TryGetValue(vehicleKey, out int segmentStartFrame))
			{
				return false;
			}

			if (DeliveryPumpSegmentTargetByVehicleId.TryGetValue(vehicleKey, out NodeID segmentTargetNodeId)
				&& segmentTargetNodeId.IsValid)
			{
				if (segmentTargetNodeId != finalNodeId
					&& IsDeliveryPumpGuardTargetReached(vehicleId, finalNodeId, segmentStartFrame, out string supersededSource))
				{
					ClearDeliveryPumpSegmentGuard(vehicleId, $"superseded-final-node oldTarget={segmentTargetNodeId} finalNode={finalNodeId} source={supersededSource}");
					return false;
				}

				if (IsDeliveryPumpGuardTargetReached(vehicleId, segmentTargetNodeId, segmentStartFrame, out string targetSource))
				{
					ClearDeliveryPumpSegmentGuard(vehicleId, $"target-reached targetNode={segmentTargetNodeId} source={targetSource}");
					return false;
				}

				LogDeliveryRoutePumpWait(vehicleId, peepId, finalNodeId, sequence, $"recent-active-route-await-physical targetNode={segmentTargetNodeId}", dedupeKey: "recent-active-await-physical");
				return true;
			}

			int frameDelta = Math.Max(0, Time.frameCount - segmentStartFrame);
			if (frameDelta > 0)
			{
				DeliveryPumpSegmentStartFrameByVehicleId.Remove(vehicleKey);
				return false;
			}

			LogDeliveryRoutePumpWait(vehicleId, peepId, finalNodeId, sequence, $"recent-active-route-same-frame frameDelta={frameDelta}", dedupeKey: "recent-active-same-frame");
			return true;
		}

		internal static bool ShouldDelayCommandForDeliveryPumpGuard(EntityID vehicleId, NodeID startNodeId, NodeID goalNodeId, out NodeID guardTargetNodeId, out string reason)
		{
			guardTargetNodeId = NodeID.INVALID;
			reason = "none";
			if (!vehicleId.IsValid)
			{
				return false;
			}
			if (MultiCrewVehicleHelper.ShouldSkipForAfterProhibitionRoutesBehaviorOwner("delivery-pump", "command-guard"))
			{
				return false;
			}

			long vehicleKey = (long)vehicleId.id;
			if (!DeliveryPumpSegmentStartFrameByVehicleId.TryGetValue(vehicleKey, out int segmentStartFrame)
				|| !DeliveryPumpSegmentTargetByVehicleId.TryGetValue(vehicleKey, out guardTargetNodeId)
				|| !guardTargetNodeId.IsValid)
			{
				return false;
			}

			if (goalNodeId.IsValid && goalNodeId == guardTargetNodeId)
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"delivery-route-command-guard-pass-through",
					$"{vehicleId.id}:{guardTargetNodeId}:{startNodeId}:{goalNodeId}:toward-target",
					$"delivery-route-command-guard-pass-through vehicle={vehicleId.id} guardTarget={guardTargetNodeId} startNode={startNodeId} goalNode={goalNodeId} reason=command-target-is-guard",
					dedupe: true);
				return false;
			}
			if (goalNodeId.IsValid
				&& goalNodeId != guardTargetNodeId
				&& (!startNodeId.IsValid || startNodeId != guardTargetNodeId))
			{
				ClearDeliveryPumpSegmentGuard(vehicleId, $"stale-command-target targetNode={guardTargetNodeId} startNode={startNodeId} goalNode={goalNodeId}");
				return false;
			}

			if (IsDeliveryPumpGuardTargetReached(vehicleId, guardTargetNodeId, segmentStartFrame, out string targetSource))
			{
				ClearDeliveryPumpSegmentGuard(vehicleId, $"target-reached targetNode={guardTargetNodeId} source={targetSource}");
				return false;
			}
			if (TryClearDeliveryPumpGuardByLiveAuthority(vehicleId, guardTargetNodeId, segmentStartFrame, out string liveAuthoritySource))
			{
				ClearDeliveryPumpSegmentGuard(vehicleId, $"target-reached-live-authority targetNode={guardTargetNodeId} source={liveAuthoritySource}");
				return false;
			}
			if (TryClearSettledDeliveryPumpGuardForCommand(vehicleId, guardTargetNodeId, startNodeId, segmentStartFrame, out string settledSource))
			{
				ClearDeliveryPumpSegmentGuard(vehicleId, $"settled-command-start targetNode={guardTargetNodeId} source={settledSource}");
				return false;
			}

			reason = $"await-pump-target targetNode={guardTargetNodeId}";
			if (startNodeId.IsValid)
			{
				reason += $" startNode={startNodeId}";
			}
			if (goalNodeId.IsValid)
			{
				reason += $" goalNode={goalNodeId}";
			}
			return true;
		}

		private static bool TryClearDeliveryPumpGuardByLiveAuthority(EntityID vehicleId, NodeID guardTargetNodeId, int segmentStartFrame, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !guardTargetNodeId.IsValid)
			{
				return false;
			}

			int frameDelta = Math.Max(0, Time.frameCount - segmentStartFrame);
			if (frameDelta <= 0
				|| MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId)
				|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId))
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource)
				&& liveNodeId == guardTargetNodeId)
			{
				source = $"{liveSource} frameDelta={frameDelta}";
				return true;
			}

			return false;
		}

		private static bool TryClearSettledDeliveryPumpGuardForCommand(EntityID vehicleId, NodeID guardTargetNodeId, NodeID startNodeId, int segmentStartFrame, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !guardTargetNodeId.IsValid || startNodeId != guardTargetNodeId)
			{
				return false;
			}

			int frameDelta = Math.Max(0, Time.frameCount - segmentStartFrame);
			if (frameDelta <= 0
				|| MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId)
				|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId))
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetVehicleLiveAuthorityNodeId(vehicleId, out NodeID liveNodeId, out string liveSource)
				&& liveNodeId == guardTargetNodeId)
			{
				source = $"{liveSource} frameDelta={frameDelta}";
				return true;
			}

			if (MultiCrewVehicleHelper.TryGetCommittedHumanVehicleInteractiveNodeId(vehicleId, out NodeID committedNodeId, out string committedSource)
				&& committedNodeId == guardTargetNodeId)
			{
				source = $"{committedSource} frameDelta={frameDelta}";
				return true;
			}

			return false;
		}

		private static bool IsDeliveryPumpGuardTargetReached(EntityID vehicleId, NodeID nodeId, int minimumFrame, out string source)
		{
			source = "none";
			if (!vehicleId.IsValid || !nodeId.IsValid)
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetObservedHumanVehicleReachedNode(vehicleId, nodeId, minimumFrame, out string observedSource))
			{
				source = $"observed-{observedSource}";
				return true;
			}

			if (MultiCrewVehicleHelper.IsHumanVehicleStrictlyPhysicalAtNode(vehicleId, nodeId, out string physicalSource))
			{
				source = string.IsNullOrWhiteSpace(physicalSource) ? "physical" : physicalSource;
				return true;
			}

			return false;
		}

		private static void LogDeliveryRoutePumpWait(EntityID vehicleId, EntityID peepId, NodeID finalNodeId, AutomationSequence sequence, string reason, string dedupeKey)
		{
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"delivery-route-pump-wait",
				$"{vehicleId.id}:{peepId.id}:{finalNodeId}:{dedupeKey}",
				$"delivery-route-pump-wait vehicle={vehicleId.id} peep={peepId.id} node={finalNodeId} route={sequence?.id.id ?? 0} reason={reason} routeActive={MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId)} routeQueued={MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId)}",
				dedupe: true);
		}

		private static bool TryProcessDeliveryCommandQueue(CommandExecutor commands, EntityID peepId, EntityID vehicleId, NodeID finalNodeId, string source, ref int queuePumps)
		{
			if (commands?.tuples == null || CommandExecutorProcessCommandQueueMethod == null)
			{
				return false;
			}
			CommandExecutor.CommandTuple tuple = commands.tuples.FirstOrDefault(item => item != null && item.peepId == peepId);
			if (tuple == null || !tuple.HasAnyCommands)
			{
				return false;
			}
			Command commandBefore = tuple.ActiveOrQueued;
			int movesBefore = peepId.FindEntity()?.components?.agent?.MovesRemaining ?? -1;
			int actionsBefore = peepId.FindEntity()?.components?.agent?.ActionsRemaining ?? -1;
			CommandExecutorProcessCommandQueueMethod.Invoke(commands, new object[] { tuple });
			queuePumps++;
			Command commandAfter = tuple.ActiveOrQueued;
			MultiCrewVehicleHelper.LogAfterProhibitionRoutesDeliveryPumpDecision(vehicleId, finalNodeId, "after-queue-pump-" + source, queuePumps, 0);
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"delivery-route-queue-pump",
				$"{vehicleId.id}:{peepId.id}:{finalNodeId}:{source}:{queuePumps}",
				$"delivery-route-queue-pump vehicle={vehicleId.id} peep={peepId.id} node={finalNodeId} source={source} before={commandBefore?.type.ToString() ?? "none"} after={commandAfter?.type.ToString() ?? "none"} movesBefore={movesBefore} actionsBefore={actionsBefore}",
				dedupe: false);
			return commandAfter != commandBefore || !tuple.HasAnyCommands || commandAfter?.type != commandBefore?.type;
		}
	}

	internal static class HumanVehicleAutomationDriverPatch
	{
		private static readonly MethodInfo AutomationExecutorRunAutomationStepMethod = AccessTools.Method(typeof(AutomationExecutor), "RunAutomationStep");

		[HarmonyPrefix]
		internal static bool Prefix(AutomationExecutor __instance)
		{
			try
			{
				PlayerInfo player = __instance?.PlayerInfo;
				if (player?.PID.IsHumanPlayer != true || player.crew == null || player.automation == null || player.commands == null)
				{
					return true;
				}
				if (AutomationExecutorRunAutomationStepMethod == null)
				{
					return true;
				}

				HashSet<long> processedVehicles = new HashSet<long>();
				foreach (CrewAssignment assignment in player.crew.GetLiving())
				{
					if (assignment.IsInVehicle && assignment.VehicleID.IsValid)
					{
						long vehicleKey = (long)assignment.VehicleID.id;
						if (!processedVehicles.Add(vehicleKey))
						{
							continue;
						}

						EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(player.crew, assignment.VehicleID);
						CrewAssignment driverAssignment = driverPeepId.IsValid ? player.crew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
						if (!driverAssignment.IsValid
							|| !driverAssignment.IsInVehicle
							|| driverAssignment.VehicleID != assignment.VehicleID
							|| !MultiCrewVehicleHelper.IsActiveVehicleOccupant(player.crew, driverAssignment))
						{
							continue;
						}

						RunAutomationForCrew(__instance, player, driverAssignment);
						continue;
					}

					RunAutomationForCrew(__instance, player, assignment);
				}

				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleAutomationDriverPatch failed: " + ex.GetType().Name + ": " + ex.Message);
				return true;
			}
		}

		private static void RunAutomationForCrew(AutomationExecutor executor, PlayerInfo player, CrewAssignment assignment)
		{
			if (!assignment.IsValid || !assignment.peepId.IsValid || player.commands.PeepHasTask(assignment.peepId))
			{
				return;
			}

			AutomationSequence sequence = player.automation.GetAutoOrNull(assignment);
			if (sequence == null || sequence.IsAutoNotActive)
			{
				return;
			}

			for (int attempts = 0; attempts < AutomationExecutor.MAX_ATTEMPTS_TO_SPEND_ALL_POINTS; attempts++)
			{
				if (player.commands.PeepHasTask(assignment.peepId))
				{
					break;
				}
				if (!CommandAutomationStep.CanExecuteOneMore(assignment))
				{
					break;
				}

				AutomationExecutorRunAutomationStepMethod.Invoke(executor, new object[] { assignment, sequence });
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
				if (__instance != null
					&& MultiCrewVehicleHelper.TryGetHumanVehicleCommandContext(__instance, out _, out _, out _, out CrewAssignment assignment, out _)
					&& MultiCrewVehicleHelper.ShouldDeferHumanVehicleRouteCommandUntilArrival(assignment.VehicleID, __instance, out NodeID expectedNodeId, out NodeID commandGoalId))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"route-canactivate-deferred",
						$"{assignment.VehicleID.id}:{expectedNodeId}:{commandGoalId}",
						$"route-canactivate-deferred vehicle={assignment.VehicleID.id} peep={__instance.peepId.id} expectedNode={expectedNodeId} commandGoal={commandGoalId} reason=await-active-arrival",
						dedupe: true);
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

	internal static class HumanVehicleCommandGotoExecuteTimingPatch
	{
		private const long DetailThresholdMs = 6L;

		internal static int Patch(Harmony harmony)
		{
			if (harmony == null)
			{
				return 0;
			}

			int patched = 0;
			patched += PatchMethod(harmony, "OnTurnStarted");
			patched += PatchMethod(harmony, "CanConsumePoints");
			patched += PatchMethod(harmony, "DoConsumePoints");
			patched += PatchMethod(harmony, "PerformTurnActions");
			patched += PatchMethod(harmony, "ContinuesToNextTurn");
			return patched;
		}

		private static int PatchMethod(Harmony harmony, string methodName)
		{
			MethodInfo method = typeof(CommandGoto).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
			if (method == null)
			{
				return 0;
			}

			harmony.Patch(
				method,
				prefix: new HarmonyMethod(typeof(HumanVehicleCommandGotoExecuteTimingPatch), nameof(Prefix)),
				postfix: new HarmonyMethod(typeof(HumanVehicleCommandGotoExecuteTimingPatch), nameof(Postfix)));
			return 1;
		}

		[HarmonyPrefix]
		internal static void Prefix(CommandGoto __instance, out long __state)
		{
			__state = ShouldTime(__instance) ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
		}

		[HarmonyPostfix]
		internal static void Postfix(CommandGoto __instance, MethodBase __originalMethod, long __state)
		{
			if (__state == 0L || __instance == null)
			{
				return;
			}

			long elapsedMs = GetElapsedMs(__state);
			long thresholdMs = (GameplayTweaksPlugin.EnablePerformanceDiagnostics?.Value ?? false) ? DetailThresholdMs : 80L;
			if (elapsedMs < thresholdMs)
			{
				return;
			}

			PathData path = __instance.Path;
			string owner = __instance.pid.IsHumanPlayer ? "human" : (__instance.pid.IsAIPlayer ? "ai" : "other");
			Debug.Log(
				"[PERF][CommandGotoExecuteSegment] ms=" + elapsedMs +
				" segment=" + (__originalMethod?.Name ?? "unknown") +
				" owner=" + owner +
				" pid=" + __instance.pid.id +
				" peep=" + __instance.peepId.id +
				" goalNode=" + __instance.goalID +
				" pathNodes=" + (path?.nodes?.Count ?? 0) +
				" pathWorld=" + (path?.world?.Count ?? 0) +
				" frame=" + Time.frameCount);
		}

		private static bool ShouldTime(CommandGoto command)
		{
			return command != null
				&& (command.pid.IsHumanPlayer || command.pid.IsAIPlayer)
				&& (global::Game.Game.ctx?.IsInteractive ?? false);
		}

		private static long GetElapsedMs(long startTicks)
		{
			return (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000L / System.Diagnostics.Stopwatch.Frequency;
		}
	}

	internal static class CommandGotoDriverGuardPatch
	{
		// #region agent log
		private static readonly bool EnableCommandGotoDriverGuardDebugLog =
			string.Equals(Environment.GetEnvironmentVariable("COG_COMMAND_GOTO_DEBUG"), "1", StringComparison.Ordinal);
		private static readonly bool EnableRoutineMoveStartLog =
			string.Equals(Environment.GetEnvironmentVariable("COG_VERBOSE_VEHICLE_AUTHORITY"), "1", StringComparison.Ordinal);

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
				long totalTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				long segmentTicks = totalTicks;
				long pathReadMs = 0L;
				long resolveStartMs = 0L;
				long reachedNodeMs = 0L;
				long guardMs = 0L;
				long markKnownMs = 0L;
				long stateMs = 0L;
				long displayHudMs = 0L;
				long hideMs = 0L;
				long driveMs = 0L;
				long xpHealthMs = 0L;
				if (MultiCrewVehicleHelper.TryGetHumanVehicleTravelConflict(assignment.VehicleID, __instance, out int activeCommandKey, out int blockedCommandKey, out bool sameOwner))
				{
					if (MultiCrewVehicleHelper.ShouldDiscardDuplicateHumanVehicleRouteCommand(assignment.VehicleID, __instance, out NodeID duplicateExpectedNodeId, out NodeID duplicateGoalNodeId, out NodeID duplicateCommandGoalId))
					{
						pathField?.SetValue(__instance, null);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"start-driving-duplicate-discarded",
							$"{assignment.VehicleID.id}:{duplicateExpectedNodeId}:{duplicateGoalNodeId}:{duplicateCommandGoalId}",
							$"start-driving-duplicate-discarded vehicle={assignment.VehicleID.id} expectedNode={duplicateExpectedNodeId} finalGoal={duplicateGoalNodeId} commandGoal={duplicateCommandGoalId} reason=already-routed-await-arrival",
							dedupe: true);
						__result = false;
						return false;
					}
					if (MultiCrewVehicleHelper.ShouldDeferHumanVehicleRouteCommandUntilArrival(assignment.VehicleID, __instance, out NodeID expectedNodeId, out NodeID commandGoalId))
					{
						pathField?.SetValue(__instance, null);
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"start-driving-deferred",
							$"{assignment.VehicleID.id}:{expectedNodeId}:{commandGoalId}",
							$"start-driving-deferred vehicle={assignment.VehicleID.id} expectedNode={expectedNodeId} commandGoal={commandGoalId} reason=await-active-arrival",
							dedupe: true);
						__result = false;
						return false;
					}
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
				HumanVehicleCommandGotoPathPatch.ClearCachedPathFrame(__instance);
				pathReadMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
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
				resolveStartMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				WorldPos reachedPos = path.world[path.world.Count - 1];
				Node reachedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(reachedPos, 5f);
				reachedNodeMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				if (reachedNode == null || reachedPos.IsZero)
				{
					MultiCrewVehicleHelper.ClearPendingHumanVehicleTravel(assignment.VehicleID, "invalid-reached-node");
					__result = false;
					return false;
				}
				NodeID startNodeId = startNode?.id ?? NodeID.INVALID;
				if (HumanVehicleTravelEndSyncPatch.ShouldDelayCommandForDeliveryPumpGuard(assignment.VehicleID, startNodeId, reachedNode.id, out NodeID guardTargetNodeId, out string guardReason))
				{
					pathField?.SetValue(__instance, null);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"delivery-route-start-deferred",
						$"{assignment.VehicleID.id}:{guardTargetNodeId}:{startNodeId}:{reachedNode.id}:start-driving",
						$"delivery-route-start-deferred vehicle={assignment.VehicleID.id} peep={__instance.peepId.id} guardTarget={guardTargetNodeId} startNode={startNodeId} goalNode={reachedNode.id} reason={guardReason}",
						dedupe: true);
					__result = false;
					return false;
				}
				guardMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				HumanVehicleTravelEndSyncPatch.MarkManualDeliveryStopIfNeeded(player, assignment, __instance.goalID, reachedNode.id, "start-driving");
				bool instant = !player.IsHuman;
				foreach (PathNode node2 in path.nodes)
				{
					if (player.IsHuman && !node2.node.known.Get(PlayerID.HumanPlayer))
					{
						peep.components.agent.IncrementStat(CrewStats.NodeScouted, 1);
					}
					player.meetings.MarkNodeAsKnown(node2.node, expectedSeen: true, instant);
				}
				markKnownMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				MultiCrewVehicleHelper.MarkHumanVehicleTravelActive(assignment.VehicleID, assignment.peepId, MultiCrewVehicleHelper.GetCommandOwnerKey(__instance), startNodeId, reachedNode.id, __instance.goalID);
				if (EnableRoutineMoveStartLog && reachedNode.id.IsValid)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"travel-logical-commit-deferred",
						$"{assignment.VehicleID.id}:{reachedNode.id}:{__instance.goalID}",
						$"travel-logical-commit-deferred vehicle={assignment.VehicleID.id} expectedNode={reachedNode.id} goalNode={__instance.goalID} reason=await-physical-arrival",
						dedupe: false);
				}
				stateMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				NodeID previewFinalNodeId = reachedNode.id;
				MultiCrewVehicleHelper.TryGetHumanVehicleTravelDisplayFinalNodeId(assignment.VehicleID, reachedNode.id, out previewFinalNodeId, out string previewFinalSource);
				MultiCrewVehicleHelper.QueueDeferredCrewHudStateAfterTravel(assignment.VehicleID, previewFinalNodeId, "travel-start", 8);
				if (EnableRoutineMoveStartLog)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"travel-start-business-preview-skipped",
						$"{assignment.VehicleID.id}:{startNodeId}:{reachedNode.id}:{__instance.goalID}",
						$"travel-start-business-preview-skipped vehicle={assignment.VehicleID.id} startNode={startNodeId} nextNode={reachedNode.id} finalGoal={__instance.goalID} reason=route-node-authority",
						dedupe: false);
				}
				displayHudMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				if (startNodeId.IsValid && __instance.goalID.IsValid && startNodeId != __instance.goalID)
				{
					MultiCrewVehicleHelper.HideCornerInfoIfShowingNode(startNodeId);
				}
				hideMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				if (EnableRoutineMoveStartLog)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"pre-drive",
						$"{assignment.VehicleID.id}:{assignment.peepId.id}:{startNodeId}:{reachedNode.id}:{__instance.goalID}",
						$"pre-drive-agent-skip vehicle={assignment.VehicleID.id} peep={assignment.peepId.id} startNode={startNodeId} reachedNode={reachedNode.id} goalNode={__instance.goalID} previewNode={previewFinalNodeId} previewSource={previewFinalSource}");
				}
				global::Game.Game.ctx.transit.DriveOnPath(player.PID, vehicle, path);
				driveMs = GetElapsedMs(segmentTicks);
				segmentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
				peep.components.agent.AddXP(XPSource.FromDriving);
				vehicle.components.mobile.UpdateHealthFrom(assignment, VehicleHealthSource.FromDriving);
				xpHealthMs = GetElapsedMs(segmentTicks);
				long totalMs = GetElapsedMs(totalTicks);
				bool detailedPerformance = GameplayTweaksPlugin.EnablePerformanceDiagnostics?.Value ?? false;
				if ((detailedPerformance && (totalMs >= 8L || driveMs >= 6L || markKnownMs >= 6L || displayHudMs >= 6L || resolveStartMs >= 6L || reachedNodeMs >= 6L || xpHealthMs >= 6L))
					|| totalMs >= 80L
					|| driveMs >= 80L
					|| markKnownMs >= 80L
					|| displayHudMs >= 80L
					|| resolveStartMs >= 80L
					|| reachedNodeMs >= 80L
					|| xpHealthMs >= 80L)
				{
					Debug.Log($"[PERF][HumanVehicleStartDriving] ms={totalMs} pathReadMs={pathReadMs} resolveStartMs={resolveStartMs} reachedNodeMs={reachedNodeMs} guardMs={guardMs} markKnownMs={markKnownMs} stateMs={stateMs} displayHudMs={displayHudMs} hideMs={hideMs} driveMs={driveMs} xpHealthMs={xpHealthMs} vehicle={assignment.VehicleID.id} peep={assignment.peepId.id} startNode={startNodeId} reachedNode={reachedNode.id} goalNode={__instance.goalID} pathNodes={path.nodes?.Count ?? 0} pathWorld={path.world?.Count ?? 0} frame={Time.frameCount}");
				}
				bool segmentStopsBeforeFinalGoal = reachedNode.id.IsValid
					&& __instance.goalID.IsValid
					&& reachedNode.id != __instance.goalID;
				if (segmentStopsBeforeFinalGoal)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"travel-turn-continuation-owned",
						$"{assignment.VehicleID.id}:{reachedNode.id}:{__instance.goalID}:{MultiCrewVehicleHelper.GetCommandOwnerKey(__instance)}",
						$"travel-turn-continuation-owned vehicle={assignment.VehicleID.id} expectedNode={reachedNode.id} finalGoal={__instance.goalID} command={MultiCrewVehicleHelper.GetCommandOwnerKey(__instance)} reason=mod-queued-resume",
						dedupe: false);
				}
				__result = false;
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

		private static long GetElapsedMs(long startTicks)
		{
			return (System.Diagnostics.Stopwatch.GetTimestamp() - startTicks) * 1000L / System.Diagnostics.Stopwatch.Frequency;
		}

	}

	/// <summary>
	/// After RefreshPanel: for CrewMuscle cards with crew in vehicle, show "Passengers:" then one line of initials (e.g. TL, HR, TW) in Extras above "In vehicle".
	/// Also shows Scout button for passengers (1 MP to reveal one adjacent node).
	/// </summary>
}
