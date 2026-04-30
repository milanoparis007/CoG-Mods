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
using Game.Session.Sim.Modules;
using Game.Session.Input;
using Game.Services;
using Game.Services.Input;
using Game.UI.Session.Combat;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Picks;
using Game.UI.Session.Popups;
using Game.UI.Util;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameplayTweaks
{
	internal static class CrewInfoGenGotoCornerPatch
	{
		[HarmonyPrefix]
		internal static bool GetGotoTargetPrefix(CrewInfoGen __instance, ref Entity __result)
		{
			try
			{
				if (__instance?.data == null || !__instance.data.crew.IsInVehicle)
					return true;
				Entity vehicle = __instance.data.crew.VehicleID.FindEntity();
				if (vehicle == null)
					return true;
				__result = vehicle;
				return false;
			}
			catch
			{
				return true;
			}
		}

		[HarmonyPrefix]
		internal static bool GetCornerNodePrefix(CrewInfoGen __instance, ref Node __result)
		{
			try
			{
				if (__instance?.data == null || !__instance.data.crew.IsInVehicle)
					return true;
				if (__instance.data.crew.peepId.FindEntity()?.data?.agent?.pid.IsHumanPlayer == true)
				{
					if (!TryGetStableHumanVehicleCornerNode(__instance.data.crew.VehicleID, out Node node, "CrewInfoGen.GetCornerNode"))
					{
						return true;
					}
					__result = node;
					return false;
				}
				if (!MultiCrewVehicleHelper.TryGetCrewCommandNode(__instance.data.crew, out Node aiNode, MultiCrewVehicleHelper.CrewNodeResolutionMode.LiveAuthoritative))
					return true;
				__result = aiNode;
				return false;
			}
			catch
			{
				return true;
			}
		}

		private static bool TryGetStableHumanVehicleCornerNode(EntityID vehicleId, out Node node, string contextTag)
		{
			node = null;
			if (!vehicleId.IsValid)
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNode(vehicleId, out node, out _, contextTag) && node != null)
			{
				return true;
			}

			if (MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNode(vehicleId, out node, out _, contextTag) && node != null)
			{
				return true;
			}

			if (MultiCrewVehicleHelper.TryGetCommittedHumanVehicleInteractiveNode(vehicleId, out node, out _) && node != null)
			{
				return true;
			}

			if (MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNode(vehicleId.FindEntity(), out node, out _) && node != null)
			{
				return true;
			}

			return MultiCrewVehicleHelper.TryGetRecentFinalizedNode(vehicleId, out node) && node != null;
		}
	}

	internal static class AllUnassignedVehiclesPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(PlayerCrew __instance, ref IEnumerable<EntityID> __result)
		{
			__result = MultiCrewVehicleHelper.GetVehiclesWithSpace(__instance);
			return false;
		}
	}

	internal static class DriverMappedCrewLookupPatch
	{
		private static bool _loggedFindPeepOverrideOnce;

		private static bool _loggedGetCrewOverrideOnce;

		private static bool IsVehicleLookupAssignmentUsable(PlayerCrew crew, CrewAssignment assignment, EntityID vehicleId, MultiCrewVehicleHelper.EnemyVehicleDisplayState state)
		{
			if (!assignment.IsValid || !assignment.IsInVehicle || assignment.VehicleID != vehicleId)
			{
				return false;
			}

			if (state?.Presentation == MultiCrewVehicleHelper.EnemyVehiclePresentationMode.DeadCrew)
			{
				return assignment.peepId.IsValid && assignment.peepId == state.InspectablePeepId;
			}

			return MultiCrewVehicleHelper.IsInspectableVehicleOccupant(crew, assignment);
		}

		[HarmonyPrefix]
		internal static bool FindPeepAssignedToVehiclePrefix(PlayerCrew __instance, EntityID vehicleId, ref EntityID __result)
		{
			try
			{
				if (__instance == null || !vehicleId.IsValid)
					return true;

				Entity vehicle = vehicleId.FindEntity();
				bool isAiVehicle = vehicle?.data?.mobile?.pid.IsAIPlayer == true;
				if (!isAiVehicle)
				{
					return true;
				}
				if (!MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(__instance, vehicleId, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
				{
					return true;
				}
				if (!state.HasInspectableOccupants || !state.InspectablePeepId.IsValid)
				{
					__result = EntityID.INVALID;
					return false;
				}

				CrewAssignment assignment = __instance.GetCrewForPeep(state.InspectablePeepId);
				if (!IsVehicleLookupAssignmentUsable(__instance, assignment, vehicleId, state))
				{
					__result = EntityID.INVALID;
					return false;
				}

				__result = state.InspectablePeepId;
				if (!_loggedFindPeepOverrideOnce)
				{
					_loggedFindPeepOverrideOnce = true;
					Debug.Log($"[GameplayTweaks] DriverMappedCrewLookupPatch.FindPeepAssignedToVehicle override vehicle={vehicleId.id} peep={state.InspectablePeepId.id}");
				}
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DriverMappedCrewLookupPatch.FindPeepAssignedToVehicle: " + ex.Message);
				return true;
			}
		}

		[HarmonyPrefix]
		internal static bool GetCrewForTargetPrefix(PlayerCrew __instance, EntityID targetId, ref CrewAssignment __result)
		{
			try
			{
				if (__instance == null || !targetId.IsValid)
					return true;

				Entity target = targetId.FindEntity();
				if (target?.components?.mobile == null)
					return true;
				if (!target.data.mobile.pid.IsAIPlayer)
				{
					return true;
				}
				if (!MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(__instance, targetId, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
				{
					return true;
				}
				if (!state.HasInspectableOccupants || !state.InspectablePeepId.IsValid)
				{
					__result = CrewAssignment.EMPTY;
					return false;
				}

				CrewAssignment assignment = __instance.GetCrewForPeep(state.InspectablePeepId);
				if (!IsVehicleLookupAssignmentUsable(__instance, assignment, targetId, state))
				{
					__result = CrewAssignment.EMPTY;
					return false;
				}

				__result = assignment;
				if (!_loggedGetCrewOverrideOnce)
				{
					_loggedGetCrewOverrideOnce = true;
					Debug.Log($"[GameplayTweaks] DriverMappedCrewLookupPatch.GetCrewForTarget override vehicle={targetId.id} peep={state.InspectablePeepId.id}");
				}
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] DriverMappedCrewLookupPatch.GetCrewForTarget: " + ex.Message);
				return true;
			}
		}
	}

	internal static class HumanFindFirstCrewAtLocationPatch
	{
		internal static bool IsUsableWorldPresenceResult(PlayerCrew crew, CrewAssignment assignment)
		{
			if (crew == null || !assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsNotDead)
			{
				return false;
			}

			if (!crew.IsOnBoard(assignment.peepId))
			{
				return false;
			}

			try
			{
				if (GameplayTweaksPlugin.IsCrewCurrentlyJailed(assignment.peepId))
				{
					return false;
				}
			}
			catch
			{
			}

			return !assignment.IsInVehicle || !assignment.VehicleID.IsValid || MultiCrewVehicleHelper.IsActiveVehicleOccupant(crew, assignment);
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, NodeID nodeId, bool onlyInVehicles, ref CrewAssignment __result)
		{
			try
			{
				if (__instance == null || !__instance.PID.IsHumanPlayer || !nodeId.IsValid)
				{
					return;
				}

				if (__result.IsValid && !IsUsableWorldPresenceResult(__instance, __result))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"arrival-presence-stale-cleared",
						$"{nodeId}:{__result.peepId.id}:{__result.VehicleID.id}",
						$"arrival-presence-stale-cleared node={nodeId} peep={__result.peepId.id} vehicle={__result.VehicleID.id} onBoard={__instance.IsOnBoard(__result.peepId)} jailed={GameplayTweaksPlugin.IsCrewCurrentlyJailed(__result.peepId)}");
					__result = CrewAssignment.EMPTY;
				}

				if (__result.IsValid)
				{
					return;
				}

				if (MultiCrewVehicleHelper.TryFindPendingHumanVehiclePresence(__instance, nodeId, onlyInVehicles, out CrewAssignment scopedMatch))
				{
					__result = scopedMatch;
					MultiCrewVehicleHelper.CrewNodeResolutionMode mode = (MultiCrewVehicleHelper.IsPendingPresenceSelectionScopeActive() || MultiCrewVehicleHelper.IsPendingBuildingInteractionScopeActive())
						? MultiCrewVehicleHelper.GetMapScopeCrewNodeResolutionMode()
						: MultiCrewVehicleHelper.GetInteractiveCrewNodeResolutionMode();
					string interactiveSource = MultiCrewVehicleHelper.GetHumanVehicleSourceTag(scopedMatch, mode);
					MultiCrewVehicleHelper.LogVehicleAuthority("arrival-presence-live", $"{scopedMatch.VehicleID.id}:{nodeId}:{scopedMatch.peepId.id}:{interactiveSource}", $"VehicleArrivalPresence live crew={scopedMatch.peepId.id} vehicle={scopedMatch.VehicleID.id} node={nodeId} source={interactiveSource}", dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanFindFirstCrewAtLocationPatch: " + ex.Message);
			}
		}
	}

	internal static class HumanFindAllDriversAtNodePatch
	{
		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, NodeID nodeId, ref List<CrewAssignment> __result)
		{
			try
			{
				if (__instance == null || !__instance.PID.IsHumanPlayer || !nodeId.IsValid)
				{
					return;
				}

				if (__result != null && __result.Count > 0)
				{
					int originalCount = __result.Count;
					__result = __result
						.Where(item => HumanFindFirstCrewAtLocationPatch.IsUsableWorldPresenceResult(__instance, item))
						.ToList();
					if (__result.Count != originalCount)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"arrival-drivers-stale-cleared",
							$"{nodeId}:{originalCount}:{__result.Count}",
							$"arrival-drivers-stale-cleared node={nodeId} before={originalCount} after={__result.Count}");
					}
				}

				List<CrewAssignment> scopedMatches = MultiCrewVehicleHelper.GetPendingHumanVehicleDriversAtNode(__instance, nodeId);
				if ((scopedMatches == null || scopedMatches.Count <= 0)
					&& MultiCrewVehicleHelper.IsPendingBuildingInteractionScopeActive())
				{
					scopedMatches = GetPhysicalHumanVehicleDriversAtNode(__instance, nodeId);
					if (scopedMatches.Count <= 0)
					{
						scopedMatches = GetPhysicalHumanVehicleDriversAtOwnedBuildingAccessNodes(__instance, nodeId);
					}
				}
				if (scopedMatches.Count > 0)
				{
					if (__result == null)
					{
						__result = new List<CrewAssignment>();
					}
					int beforeMerge = __result.Count;
					HashSet<long> seenVehicles = new HashSet<long>(__result
						.Where(item => item.IsValid && item.VehicleID.IsValid)
						.Select(item => unchecked((long)item.VehicleID.id)));
					foreach (CrewAssignment scopedMatch in scopedMatches)
					{
						if (!scopedMatch.IsValid || !scopedMatch.VehicleID.IsValid)
						{
							continue;
						}
						if (seenVehicles.Add(unchecked((long)scopedMatch.VehicleID.id)))
						{
							__result.Add(scopedMatch);
						}
					}
					MultiCrewVehicleHelper.CrewNodeResolutionMode mode = (MultiCrewVehicleHelper.IsPendingPresenceSelectionScopeActive() || MultiCrewVehicleHelper.IsPendingBuildingInteractionScopeActive())
						? MultiCrewVehicleHelper.GetMapScopeCrewNodeResolutionMode()
						: MultiCrewVehicleHelper.GetInteractiveCrewNodeResolutionMode();
					string interactiveSource = MultiCrewVehicleHelper.GetHumanVehicleSourceTag(scopedMatches[0], mode);
					MultiCrewVehicleHelper.LogVehicleAuthority("arrival-drivers-live", $"{nodeId}:{scopedMatches.Count}:{interactiveSource}:{beforeMerge}:{__result.Count}", $"VehicleArrivalPresence drivers node={nodeId} count={scopedMatches.Count} source={interactiveSource} before={beforeMerge} after={__result.Count}", dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanFindAllDriversAtNodePatch: " + ex.Message);
			}
		}

		private static List<CrewAssignment> GetPhysicalHumanVehicleDriversAtNode(PlayerCrew crew, NodeID nodeId)
		{
			List<CrewAssignment> matches = new List<CrewAssignment>();
			if (crew == null || !crew.PID.IsHumanPlayer || !nodeId.IsValid)
			{
				return matches;
			}

			HashSet<long> seenVehicles = new HashSet<long>();
			foreach (CrewAssignment assignment in crew.GetLiving())
			{
				if (!assignment.IsValid
					|| !assignment.IsInVehicle
					|| !assignment.VehicleID.IsValid
					|| !assignment.peepId.IsValid
					|| !crew.IsOnBoard(assignment.peepId)
					|| GameplayTweaksPlugin.IsCrewCurrentlyJailed(assignment.peepId)
					|| !MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(assignment.VehicleID, nodeId)
					|| !seenVehicles.Add(unchecked((long)assignment.VehicleID.id)))
				{
					continue;
				}

				EntityID driverId = MultiCrewVehicleHelper.GetDriverPeepId(crew, assignment.VehicleID);
				CrewAssignment driver = driverId.IsValid ? crew.GetCrewForPeep(driverId) : CrewAssignment.EMPTY;
				matches.Add(driver.IsValid && driver.VehicleID == assignment.VehicleID ? driver : assignment);
			}

			if (matches.Count > 0)
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"arrival-drivers-physical",
					$"{nodeId}:{matches.Count}",
					$"VehicleArrivalPresence physical drivers node={nodeId} count={matches.Count} source=live-physical",
					dedupe: false);
			}

			return matches;
		}

		private static List<CrewAssignment> GetPhysicalHumanVehicleDriversAtOwnedBuildingAccessNodes(PlayerCrew crew, NodeID primaryNodeId)
		{
			List<CrewAssignment> matches = new List<CrewAssignment>();
			if (crew == null || !crew.PID.IsHumanPlayer || !primaryNodeId.IsValid)
			{
				return matches;
			}

			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer?.territory == null)
			{
				return matches;
			}

			List<Entity> ownedBuildings = new List<Entity>();
			try
			{
				foreach (EntityID buildingId in MultiCrewVehicleHelper.GetControlledBuildingIds(humanPlayer))
				{
					Entity building = buildingId.FindEntity();
					if (building != null)
					{
						ownedBuildings.Add(building);
					}
				}
			}
			catch
			{
			}

			try
			{
				Entity safehouse = humanPlayer.territory.Safehouse.IsValid ? humanPlayer.territory.Safehouse.FindEntity() : null;
				if (safehouse != null && ownedBuildings.All(building => building.Id != safehouse.Id))
				{
					ownedBuildings.Add(safehouse);
				}
			}
			catch
			{
			}

			HashSet<long> seenVehicles = new HashSet<long>();
			foreach (Entity building in ownedBuildings)
			{
				if (building == null || !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(building))
				{
					continue;
				}

				NodeID buildingNodeId = MultiCrewVehicleHelper.TryGetEntityBoardNodeId(building, out NodeID resolvedBuildingNodeId)
					? resolvedBuildingNodeId
					: NodeID.INVALID;
				if (buildingNodeId != primaryNodeId)
				{
					continue;
				}

				if (!CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> comparisonNodeIds, out _)
					|| comparisonNodeIds == null)
				{
					comparisonNodeIds = new List<NodeID>();
				}
				if (buildingNodeId.IsValid && !comparisonNodeIds.Contains(buildingNodeId))
				{
					comparisonNodeIds.Add(buildingNodeId);
				}
				OwnedBuildingInteractionSelectionPatch.AddOwnedBuildingFrontageComparisonNodes(building, comparisonNodeIds);

				foreach (NodeID nodeId in comparisonNodeIds.Where(nodeId => nodeId.IsValid).Distinct())
				{
					foreach (CrewAssignment match in GetPhysicalHumanVehicleDriversAtNode(crew, nodeId))
					{
						if (!match.IsValid || !match.VehicleID.IsValid)
						{
							continue;
						}
						if (seenVehicles.Add(unchecked((long)match.VehicleID.id)))
						{
							matches.Add(match);
						}
					}
				}

				if (matches.Count > 0)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-footer-access-node-drivers",
						$"{building.Id.id}:{primaryNodeId}:{matches.Count}",
						$"ownedbiz-footer-access-node-drivers building={building.Id.id} primaryNode={primaryNodeId} count={matches.Count} crew={string.Join(",", matches.Where(match => match.IsValid && match.peepId.IsValid).Select(match => match.peepId.id.ToString()))}",
						dedupe: false);
					break;
				}
			}

			return matches;
		}
	}

	internal static class HumanCrewSelectorNodePatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(NodeID nid, string message, Action<EntityID> onSelect, Action<EntityID> onCancel = null)
		{
			try
			{
				if (!nid.IsValid || onSelect == null)
				{
					return true;
				}

				PlayerCrew humanCrew = global::Game.Game.ctx?.players?.Human?.crew;
				if (humanCrew == null)
				{
					return true;
				}

				List<EntityID> peeps = humanCrew.FindAllDriversAtNode(nid)
					.Select(crew => crew.peepId)
					.Where(peepId => peepId.IsValid)
					.Distinct()
					.ToList();
				if (peeps.Count > 0)
				{
					return true;
				}

				if (!MultiCrewVehicleHelper.TryGetHumanCrewUsableForMapScopeAtNode(humanCrew, nid, out List<CrewAssignment> matches, out string sourceTag)
					|| matches.Count <= 0)
				{
					return true;
				}

				peeps = matches
					.Select(crew => crew.peepId)
					.Where(peepId => peepId.IsValid)
					.Distinct()
					.ToList();
				if (peeps.Count <= 0)
				{
					return true;
				}

				if (OwnedBuildingInteractionSelectionPatch.TryResolveSingleVehicleInteractionCrew(humanCrew, matches, out EntityID singleVehiclePeepId))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority("crew-selector-bypassed", $"{nid}:{singleVehiclePeepId.id}:{sourceTag}:single-vehicle", $"crew-selector-bypassed node={nid} crew={singleVehiclePeepId.id} source={sourceTag} reason=single-vehicle", dedupe: false);
					onSelect(singleVehiclePeepId);
					return false;
				}

				MultiCrewVehicleHelper.LogVehicleAuthority("crew-selector-fallback", $"{nid}:{peeps.Count}:{sourceTag}", $"crew-selector-fallback node={nid} count={peeps.Count} source={sourceTag}", dedupe: false);
				if (global::Game.Game.ctx?.players?.Human?.PID.IsHumanPlayer == true
					&& MultiCrewVehicleHelper.IsFreshHumanStartup(PlayerID.HumanPlayer))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority("crew-selector-startup-fallback", $"{nid}:{peeps.Count}:{sourceTag}", $"crew-selector-startup-fallback node={nid} count={peeps.Count} source={sourceTag}", dedupe: false);
				}
				if (peeps.Count == 1)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority("crew-selector-bypassed", $"{nid}:{peeps[0].id}:{sourceTag}", $"crew-selector-bypassed node={nid} crew={peeps[0].id} source={sourceTag}", dedupe: false);
					onSelect(peeps[0]);
					return false;
				}
				EntitySelectionPopup.ShowCrewSelector(peeps, message, onSelect, onCancel);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanCrewSelectorNodePatch.ShowCrewSelector: " + ex.Message);
				return true;
			}
		}
	}

	internal static class CrewHireEligibilityPatch
	{
		internal static bool IsAllowedCrewHireCandidate(Entity candidate, out string reason)
		{
			reason = "ok";
			if (candidate?.data?.person == null || candidate.data.agent == null)
			{
				reason = "missing-person-or-agent";
				return false;
			}

			PersonData person = candidate.data.person;
			if (!person.IsAlive)
			{
				reason = "dead";
				return false;
			}
			float minHireAge = GetEffectiveCrewHireMinAge();
			if (person.GetAge(G.GetNow()).YearsFloat < minHireAge)
			{
				reason = "underage";
				return false;
			}
			if (person.business.IsValid)
			{
				reason = "business-worker-or-owner";
				return false;
			}
			if (person.resassigned.IsValid)
			{
				reason = "residence-assigned";
				return false;
			}
			if (global::Game.Game.ctx?.simman?.politics?.GetPoliticianData(candidate.Id) != null)
			{
				reason = "politician";
				return false;
			}
			string affiliationReason = GameplayTweaksPlugin.PotentialBizOwnerEligibilityPatch.GetGangAffiliationBlockReason(candidate);
			if (!string.IsNullOrEmpty(affiliationReason))
			{
				reason = affiliationReason;
				return false;
			}

			PlayerID pid = candidate.data.agent.pid;
			if (pid.id != 0)
			{
				PlayerInfo owner = pid.FindPlayer();
				if (owner != null)
				{
					reason = owner.IsJustGang ? "gang-member" : (owner.IsJustGoon ? "troublemaker" : (owner.IsCopOrFed ? "law-enforcement" : "already-player-owned"));
				}
				else
				{
					reason = "non-neutral-owner";
				}
				return false;
			}

			return true;
		}

		internal static bool ShouldBlockCrewHire(PlayerCrew crew, Entity candidate, bool isBoss, out string reason)
		{
			reason = "ok";
			if (crew == null || candidate == null || isBoss)
			{
				return false;
			}
			return !IsAllowedCrewHireCandidate(candidate, out reason);
		}

		[HarmonyPostfix]
		internal static void IsEligibleCrewMemberPostfix(Entity person, ref bool __result)
		{
			if (!__result)
			{
				return;
			}
			if (!IsAllowedCrewHireCandidate(person, out string reason))
			{
				__result = false;
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"eligible-blocked peep={person?.Id.id ?? 0UL} reason={reason}");
			}
		}

		[HarmonyPostfix]
		internal static void GetBestCrewCandidateFromPostfix(PlayerCrewGrowth __instance, EntityID playersFriend, ref EntityID __result)
		{
			if (__instance == null)
			{
				return;
			}
			Entity currentCandidate = __result.IsValid ? __result.FindEntity() : null;
			if (currentCandidate != null && IsAllowedCrewHireCandidate(currentCandidate, out _) && GetCrewHireAgeBucket(currentCandidate) == 0)
			{
				return;
			}

			List<PlayerCrewGrowth.CandidateData> candidates = new List<PlayerCrewGrowth.CandidateData>();
			__instance.GetAllCrewCandidateFrom(playersFriend, candidates);
			EntityID replacement = candidates
				.Where(candidate => candidate.candidate.IsValid && IsAllowedCrewHireCandidate(candidate.candidate.FindEntity(), out _))
				.OrderBy(candidate => GetCrewHireAgeBucket(candidate.candidate.FindEntity()))
				.ThenByDescending(candidate => candidate.candidate.FindEntity()?.data?.person?.born.days ?? int.MinValue)
				.Select(candidate => candidate.candidate)
				.FirstOrDefault();
			if (replacement.IsValid)
			{
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"candidate-replaced introducer={playersFriend.id} old={__result.id} replacement={replacement.id}");
				__result = replacement;
				return;
			}

			if (__result.IsValid)
			{
				IsAllowedCrewHireCandidate(__result.FindEntity(), out string reason);
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"candidate-left-for-ui introducer={playersFriend.id} old={__result.id} reason={reason}");
			}
		}

		private static int GetCrewHireAgeBucket(Entity candidate)
		{
			try
			{
				float age = candidate?.data?.person?.GetAge(G.GetNow()).YearsFloat ?? 0f;
				float minAge = GetEffectiveCrewHireMinAge();
				if (age >= minAge && age <= 35f)
				{
					return 0;
				}
				if (age < 50f)
				{
					return 1;
				}
				return 2;
			}
			catch
			{
				return 3;
			}
		}

		private static float GetEffectiveCrewHireMinAge()
		{
			return Mathf.Max(18f, (GameplayTweaksPlugin.HireableMinAge != null) ? GameplayTweaksPlugin.HireableMinAge.Value : 18f);
		}

		[HarmonyPostfix]
		internal static void ConvoDataCrewHireEnabledPostfix(ConvoDataCrewHire __instance, ref bool __result)
		{
			if (!__result || __instance == null)
			{
				return;
			}
			if (!IsAllowedCrewHireCandidate(__instance.targetId.FindEntity(), out string reason))
			{
				__result = false;
				GameplayTweaksPlugin.VerificationLog("CrewHire", $"convo-disabled candidate={__instance.targetId.id} reason={reason}");
			}
		}

		[HarmonyPrefix]
		internal static bool ShowCrewHirePopupPrefix(ConvoButton button, ref OnClickResult __result)
		{
			return ValidateCrewHireButton(button, ref __result, "show-popup");
		}

		[HarmonyPrefix]
		internal static bool ExecuteCrewHirePrefix(ConvoButton button, ref OnClickResult __result)
		{
			return ValidateCrewHireButton(button, ref __result, "execute");
		}

		private static bool ValidateCrewHireButton(ConvoButton button, ref OnClickResult __result, string source)
		{
			ConvoDataCrewHire data = button?.GetData<ConvoDataCrewHire>();
			if (data == null || IsAllowedCrewHireCandidate(data.targetId.FindEntity(), out string reason))
			{
				return true;
			}

			__result = OnClickResult.CONTINUE;
			MultiCrewVehicleHelper.ShowHudMessage("That person is not available to hire.");
			GameplayTweaksPlugin.VerificationLog("CrewHire", $"hire-blocked source={source} candidate={data.targetId.id} introducer={data.introducerId.id} reason={reason}");
			return false;
		}
	}

	internal static class HumanBuildingCrewPresencePatch
	{
		private static readonly FieldInfo BuildingComponentEntityField = AccessTools.Field(typeof(BuildingComponent), "_entity");
		private static readonly Dictionary<ulong, bool> PresenceResultByFrameKey = new Dictionary<ulong, bool>();
		private static int _presenceCacheFrame = -1;

		[HarmonyPrefix]
		internal static bool Prefix(BuildingComponent __instance, PlayerID pid, ref bool __result)
		{
			try
			{
				if (__instance == null || !pid.IsHumanPlayer)
				{
					return true;
				}

				PlayerCrew humanCrew = pid.FindPlayer()?.crew;
				Entity building = BuildingComponentEntityField?.GetValue(__instance) as Entity;
				if (building == null)
				{
					building = Traverse.Create(__instance).Field("_entity").GetValue<Entity>();
				}
				if (building != null)
				{
					if (_presenceCacheFrame != Time.frameCount)
					{
						_presenceCacheFrame = Time.frameCount;
						PresenceResultByFrameKey.Clear();
					}
					ulong cacheKey = building.Id.id ^ ((ulong)(uint)pid.id << 32);
					if (PresenceResultByFrameKey.TryGetValue(cacheKey, out bool cachedResult))
					{
						__result = cachedResult;
						return false;
					}
				}
				Node resolvedBuildingNode = null;
				NodeID nodeId = NodeID.INVALID;
				List<NodeID> comparisonNodeIds = null;
				if (CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(building, out resolvedBuildingNode, out comparisonNodeIds, out NodeID comparisonNodeId))
				{
					nodeId = comparisonNodeId;
				}
				else if (building?.data?.board != null)
				{
					nodeId = building.data.board.bead.nodeId;
				}
				if (humanCrew == null || !nodeId.IsValid)
				{
					return true;
				}

				if (MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
					&& selectedVehicleId.IsValid
					&& (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(selectedVehicleId)
						|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(selectedVehicleId)
						|| MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(selectedVehicleId, out _, out NodeID selectedExpectedNodeId, out NodeID selectedGoalNodeId)
							&& (selectedExpectedNodeId.IsValid || selectedGoalNodeId.IsValid))
					&& MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out NodeID selectedPreviewNodeId, out string selectedPreviewSource, "presence")
					&& selectedPreviewNodeId.IsValid
					&& !CommandButtonScopeOutPatch.TryMatchScopePreviewNodeToBuilding(building, selectedPreviewNodeId, out _, out _, out _, out _))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"building-presence-preview-ignored",
						$"{selectedVehicleId.id}:{nodeId}:{selectedPreviewNodeId}:{building?.Id.id ?? 0UL}",
						$"building-presence-preview-ignored vehicle={selectedVehicleId.id} building={building?.Id.id ?? 0UL} requestedNode={nodeId} previewNode={selectedPreviewNodeId} previewSource={selectedPreviewSource}",
						dedupe: false);
				}

				List<CrewAssignment> matches = new List<CrewAssignment>();
				string sourceTag = "live-authority";
				foreach (NodeID comparisonNode in (comparisonNodeIds ?? new List<NodeID> { nodeId }).Where(candidate => candidate.IsValid).Distinct())
				{
					List<CrewAssignment> liveMatches = MultiCrewVehicleHelper.GetHumanCrewPresentAtNodeLive(humanCrew, comparisonNode);
					liveMatches = CommandButtonScopeOutPatch.FilterSelectedVehicleTransientScopeMatches(building, comparisonNodeIds ?? new List<NodeID> { nodeId }, liveMatches, sourceTag);
					liveMatches = CommandButtonScopeOutPatch.FilterSafehouseIdleCrewForBuilding(building, comparisonNodeIds ?? new List<NodeID> { nodeId }, liveMatches, sourceTag);
					if (liveMatches.Count <= 0)
					{
						continue;
					}

					matches = liveMatches;
					if (comparisonNode != nodeId)
					{
						_ = resolvedBuildingNode;
					}
					break;
				}
				if (matches.Count <= 0
					&& building != null
					&& !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(building)
					&& !CommandButtonScopeOutPatch.IsRecentlyRejectedWrongCornerScopeBuilding(building, selectedVehicleId)
					&& MultiCrewVehicleHelper.TryGetHumanCrewUsableForScopePreviewAtBuilding(humanCrew, building, out List<CrewAssignment> previewMatches, out string previewSource)
					&& previewMatches.Count > 0)
				{
					matches = previewMatches;
					GameplayTweaksPlugin.VerificationLog(
						"ScopeOut",
						$"building-presence-scope-preview building={building.Id.id} node={nodeId} crew={string.Join(",", matches.Where(match => match.IsValid && match.peepId.IsValid).Select(match => match.peepId.id.ToString()))} source={previewSource}");
				}
				__result = matches.Count > 0;
				if (building != null)
				{
					PresenceResultByFrameKey[building.Id.id ^ ((ulong)(uint)pid.id << 32)] = __result;
				}
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanBuildingCrewPresencePatch: " + ex.Message);
				return true;
			}
		}
	}

	internal static class HumanVehicleBuildingInteractionScopePatch
	{
		[HarmonyPrefix]
		internal static void RefreshContentsPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("BuildingPick.RefreshContents");
		}

		[HarmonyPrefix]
		internal static void OnClickPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("BuildingPick.OnClick");
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception)
		{
			MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
			return __exception;
		}
	}

	internal static class BuildingScopeOutDescriptionLocPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(Entity building, ref string __result)
		{
			try
			{
				List<Game.Session.Sim.Modules.IBizModule> modules = building?.components?.modules?.bizmodules;
				if (modules == null)
				{
					__result = string.Empty;
					return false;
				}

				List<string> fragments = new List<string>();
				HashSet<string> seenKeys = new HashSet<string>(StringComparer.Ordinal);
				foreach (Game.Session.Sim.Modules.IBizModule module in modules)
				{
					string locKey = module?.LocData?.locScopeOut;
					if (string.IsNullOrWhiteSpace(locKey) || !seenKeys.Add(locKey))
					{
						continue;
					}

					if (Loc.instance?.HasKey(locKey) == true)
					{
						fragments.Add(Loc.Get(locKey));
						continue;
					}

					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scopeout-missing-loc-suppressed key={locKey} building={building.Id.id}");
				}

				__result = string.Join(" ", fragments);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] BuildingScopeOutDescriptionLocPatch: " + ex.Message);
				return true;
			}
		}
	}

	internal static class HumanVehicleBuildingActivationScopePatch
	{
		[HarmonyPrefix]
		internal static void BizActivatedPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("BizComponent.OnBizActivated");
		}

		[HarmonyPrefix]
		internal static void BizConversationPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("BizComponent.StartConversation");
		}

		[HarmonyPrefix]
		internal static void ResidenceConversationPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("ResidenceComponent.StartCasinoConversation");
		}

		[HarmonyPrefix]
		internal static void CivicConversationPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("CivicComponent.StartConversation");
		}

		[HarmonyPrefix]
		internal static void CivicPoliticsPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("CivicComponent.ShowPoliticsDialog");
		}

		[HarmonyPrefix]
		internal static void BuildingPickMouseoverPrefix()
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("BuildingPickUtil.MakeMouseover");
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception)
		{
			MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
			return __exception;
		}
	}

	internal static class OwnedBizVehicleVisitStatePatch
	{
		[HarmonyPrefix]
		internal static void SetModelPrefix(VisitState visit)
		{
			try
			{
				NormalizeOwnedBizVisitDriver(visit, "OwnedBizController.SetModel");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.SetModel: " + ex.Message);
			}
		}

		[HarmonyPrefix]
		internal static void RefreshFooterPrefix(object __instance)
		{
			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("OwnedBizDialog.RefreshFooter");
			NormalizeOwnedBizDialogVisit(__instance, "OwnedBizDialog.RefreshFooter");
		}

		[HarmonyFinalizer]
		internal static Exception RefreshFooterFinalizer(Exception __exception)
		{
			MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
			return __exception;
		}

		internal static bool NormalizeOwnedBizVisitDriver(VisitState visit, string source)
		{
			if (visit == null || visit.building == null)
			{
				return false;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null || !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(visit.building))
			{
				return false;
			}

			if (OwnedBuildingInteractionSelectionPatch.TryResolveSelectedDirectedOwnedBuildingVehicleDriver(humanCrew, visit.building, out CrewAssignment selectedDriver, out string selectedMatchSource))
			{
				if (selectedDriver.IsValid
					&& selectedDriver.IsInVehicle
					&& selectedDriver.VehicleID.IsValid
					&& (!visit.crew.IsValid
						|| !visit.crew.IsInVehicle
						|| visit.crew.VehicleID != selectedDriver.VehicleID
						|| visit.crew.peepId != selectedDriver.peepId
						|| !MultiCrewVehicleHelper.IsDriver(humanCrew, visit.crew)))
				{
					visit.SetCrew(selectedDriver);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-directed-vehicle-priority",
						$"{visit.building.Id.id}:{selectedDriver.peepId.id}:{selectedDriver.VehicleID.id}:{source}",
						$"ownedbiz-directed-vehicle-priority building={visit.building.Id.id} crew={selectedDriver.peepId.id} vehicle={selectedDriver.VehicleID.id} source={source} match={selectedMatchSource}",
						dedupe: false);
					return true;
				}
			}

			bool needsVehicleDriver = !visit.crew.IsValid
				|| !visit.crew.IsInVehicle
				|| !visit.crew.VehicleID.IsValid
				|| visit.vehicle == null
				|| !MultiCrewVehicleHelper.IsDriver(humanCrew, visit.crew);
			if (!needsVehicleDriver)
			{
				return false;
			}

			if (!OwnedBuildingInteractionSelectionPatch.TryResolveOwnedBuildingVehicleMatches(humanCrew, visit.building, out List<CrewAssignment> matches, allowAnyHumanVehicle: true, requireLiveVehicleNode: true)
				|| matches == null
				|| matches.Count <= 0)
			{
				return false;
			}

			EntityID driverPeepId = ResolveVisitVehicleDriverFirst(humanCrew, visit, matches);
			CrewAssignment driver = driverPeepId.IsValid ? humanCrew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
			if (!driver.IsValid || !driver.IsInVehicle || !driver.VehicleID.IsValid)
			{
				return false;
			}

			visit.SetCrew(driver);
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"ownedbiz-visit-driver-normalized",
				$"{visit.building.Id.id}:{driver.peepId.id}:{driver.VehicleID.id}:{source}",
				$"ownedbiz-visit-driver-normalized building={visit.building.Id.id} crew={driver.peepId.id} vehicle={driver.VehicleID.id} source={source}",
				dedupe: false);
			return true;
		}

		internal static bool TryEnsureOwnedBizPhysicalVehicleAccess(VisitState visit, string source)
		{
			if (visit == null || visit.building == null)
			{
				return true;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null || !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(visit.building))
			{
				return true;
			}

			if (TryResolveOwnedBizPhysicalVehicleAccess(humanCrew, visit, out CrewAssignment physicalDriver, source))
			{
				visit.SetCrew(physicalDriver);
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"ownedbiz-inventory-physical-allowed",
					$"{visit.building.Id.id}:{physicalDriver.peepId.id}:{physicalDriver.VehicleID.id}:{source}",
					$"ownedbiz-inventory-physical-allowed building={visit.building.Id.id} crew={physicalDriver.peepId.id} vehicle={physicalDriver.VehicleID.id} source={source}",
					dedupe: false);
				return true;
			}

			if (TryResolveOwnedBizFinalGoalVehicleAccess(humanCrew, visit.building, out CrewAssignment finalGoalDriver, out NodeID finalGoalNodeId, out NodeID finalGoalMatchedNodeId, out string finalGoalSource))
			{
				visit.SetCrew(finalGoalDriver);
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"ownedbiz-inventory-finalgoal-allowed",
					$"{visit.building.Id.id}:{finalGoalDriver.peepId.id}:{finalGoalDriver.VehicleID.id}:{finalGoalNodeId}:{source}",
					$"ownedbiz-inventory-finalgoal-allowed building={visit.building.Id.id} crew={finalGoalDriver.peepId.id} vehicle={finalGoalDriver.VehicleID.id} node={finalGoalNodeId} matchedNode={finalGoalMatchedNodeId} accessSource={finalGoalSource} source={source}",
					dedupe: false);
				return true;
			}

			if (TryResolveOwnedBizViewOnlyVehicleContext(humanCrew, out CrewAssignment viewOnlyDriver, out string viewOnlySource))
			{
				visit.SetCrew(viewOnlyDriver);
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"ownedbiz-inventory-viewonly-vehicle",
					$"{visit.building.Id.id}:{viewOnlyDriver.peepId.id}:{viewOnlyDriver.VehicleID.id}:{source}",
					$"ownedbiz-inventory-viewonly-vehicle building={visit.building.Id.id} crew={viewOnlyDriver.peepId.id} vehicle={viewOnlyDriver.VehicleID.id} source={source} context={viewOnlySource} reason=no-live-vehicle-at-building",
					dedupe: false);
				return true;
			}

			visit.SetCrew(CrewAssignment.EMPTY);
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"ownedbiz-inventory-viewonly-allowed",
				$"{visit.building.Id.id}:{source}",
				$"ownedbiz-inventory-viewonly-allowed building={visit.building.Id.id} source={source} reason=no-live-vehicle-at-building",
				dedupe: false);
			return true;
		}

		internal static bool CanOwnedBizInventoryTransferNow(VisitState visit, string source)
		{
			try
			{
				if (visit == null || visit.building == null)
				{
					return true;
				}

				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null || !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(visit.building))
				{
					return true;
				}

				return TryResolveOwnedBizPhysicalVehicleAccess(humanCrew, visit, out _, source)
					|| TryResolveOwnedBizFinalGoalVehicleAccess(humanCrew, visit.building, out _, out _, out _, out _);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.CanOwnedBizInventoryTransferNow: " + ex.Message);
				return true;
			}
		}

		private static bool TryResolveOwnedBizViewOnlyVehicleContext(PlayerCrew humanCrew, out CrewAssignment driver, out string source)
		{
			driver = CrewAssignment.EMPTY;
			source = "none";
			if (humanCrew == null)
			{
				return false;
			}

			if (!MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out string selectedSource)
				|| !selectedVehicleId.IsValid
				|| !OwnedBuildingInteractionSelectionPatch.TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out driver)
				|| !driver.IsValid
				|| !driver.IsInVehicle
				|| !driver.VehicleID.IsValid)
			{
				return false;
			}

			source = string.IsNullOrWhiteSpace(selectedSource) ? "selected-vehicle" : selectedSource;
			return true;
		}

		private static bool TryResolveOwnedBizPhysicalVehicleAccess(PlayerCrew humanCrew, VisitState visit, out CrewAssignment driver, string source)
		{
			driver = CrewAssignment.EMPTY;
			if (humanCrew == null || visit?.building == null)
			{
				return false;
			}

			if (!OwnedBuildingInteractionSelectionPatch.TryResolveOwnedBuildingVehicleMatches(
					humanCrew,
					visit.building,
					out List<CrewAssignment> matches,
					allowAnyHumanVehicle: true,
					requireLiveVehicleNode: true)
				|| matches == null
				|| matches.Count <= 0)
			{
				return false;
			}

			EntityID driverPeepId = ResolveVisitVehicleDriverFirst(humanCrew, visit, matches);
			driver = driverPeepId.IsValid ? humanCrew.GetCrewForPeep(driverPeepId) : CrewAssignment.EMPTY;
			return driver.IsValid && driver.IsInVehicle && driver.VehicleID.IsValid;
		}

		private static bool TryResolveOwnedBizFinalGoalVehicleAccess(
			PlayerCrew humanCrew,
			Entity building,
			out CrewAssignment driver,
			out NodeID finalGoalNodeId,
			out NodeID matchedNodeId,
			out string source)
		{
			driver = CrewAssignment.EMPTY;
			finalGoalNodeId = NodeID.INVALID;
			matchedNodeId = NodeID.INVALID;
			source = "none";
			if (humanCrew == null || building == null || !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(building))
			{
				return false;
			}
			if (!MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out string selectedSource)
				|| !selectedVehicleId.IsValid)
			{
				return false;
			}
			bool routeActive = MultiCrewVehicleHelper.IsHumanVehicleTravelActive(selectedVehicleId)
				|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(selectedVehicleId);
			if (!MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(selectedVehicleId, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
				|| (!expectedNodeId.IsValid && !goalNodeId.IsValid)
				|| (!routeActive && !expectedNodeId.IsValid))
			{
				return false;
			}

			if (!CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> comparisonNodeIds, out _)
				|| comparisonNodeIds == null)
			{
				comparisonNodeIds = new List<NodeID>();
			}
			comparisonNodeIds = comparisonNodeIds
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList();
			NodeID buildingBoardNodeId = MultiCrewVehicleHelper.TryGetEntityBoardNodeId(building, out NodeID resolvedBuildingNodeId)
				? resolvedBuildingNodeId
				: NodeID.INVALID;
			if (buildingBoardNodeId.IsValid && !comparisonNodeIds.Contains(buildingBoardNodeId))
			{
				comparisonNodeIds.Add(buildingBoardNodeId);
			}
			OwnedBuildingInteractionSelectionPatch.AddOwnedBuildingFrontageComparisonNodes(building, comparisonNodeIds);
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			bool safehouseBuilding = OwnedBuildingInteractionSelectionPatch.IsHumanSafehouseInteractionBuilding(building);
			if (!TryMatchFinalGoalAccessNode(building, expectedNodeId, comparisonNodeIds, safehouseBuilding, out matchedNodeId))
			{
				if (expectedNodeId.IsValid || !TryMatchFinalGoalAccessNode(building, goalNodeId, comparisonNodeIds, safehouseBuilding, out matchedNodeId))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-inventory-finalgoal-miss",
						$"{building.Id.id}:{selectedVehicleId.id}:{expectedNodeId}:{goalNodeId}",
						$"ownedbiz-inventory-finalgoal-miss building={building.Id.id} vehicle={selectedVehicleId.id} expectedNode={expectedNodeId} goalNode={goalNodeId} comparisonNodes={string.Join(",", comparisonNodeIds)} safehouse={safehouseBuilding}",
						dedupe: false);
					return false;
				}
				finalGoalNodeId = goalNodeId;
				source = "pending-final-goal";
			}
			else
			{
				finalGoalNodeId = expectedNodeId;
				source = "pending-expected";
			}

			if (!OwnedBuildingInteractionSelectionPatch.TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out driver))
			{
				return false;
			}

			source += "-" + (string.IsNullOrWhiteSpace(selectedSource) ? "selected" : selectedSource);
			return true;
		}

		private static bool TryMatchFinalGoalAccessNode(Entity building, NodeID nodeId, List<NodeID> comparisonNodeIds, bool safehouseBuilding, out NodeID matchedNodeId)
		{
			matchedNodeId = NodeID.INVALID;
			if (!nodeId.IsValid || comparisonNodeIds == null || comparisonNodeIds.Count <= 0)
			{
				return false;
			}
			if (comparisonNodeIds.Contains(nodeId))
			{
				matchedNodeId = nodeId;
				return true;
			}
			if (safehouseBuilding)
			{
				return false;
			}
			return OwnedBuildingInteractionSelectionPatch.TryMatchOwnedBuildingPreviewNode(building, nodeId, comparisonNodeIds, out matchedNodeId);
		}

		internal static bool IsInventoryModuleContext(object moduleContext)
		{
			try
			{
				object module = Traverse.Create(moduleContext).Field("module").GetValue();
				return module is InventoryModule;
			}
			catch
			{
				return false;
			}
		}

		internal static VisitState TryGetControllerVisit(object controllerInstance)
		{
			try
			{
				if (controllerInstance is OwnedBizController controller)
				{
					return controller.Model?.visit;
				}
			}
			catch
			{
			}
			return null;
		}

		internal static VisitState TryGetInventoryViewVisit(object viewInstance)
		{
			try
			{
				OwnedBizModel model = Traverse.Create(viewInstance).Field("Model").GetValue<OwnedBizModel>();
				return model?.visit;
			}
			catch
			{
				return null;
			}
		}

		internal static object TryGetInventoryViewController(object viewInstance)
		{
			try
			{
				return Traverse.Create(viewInstance).Field("Controller").GetValue();
			}
			catch
			{
				return null;
			}
		}

		internal static void RefreshOwnedBizDialogFooter(object controllerInstance, string source)
		{
			try
			{
				if (!(controllerInstance is OwnedBizController controller))
				{
					return;
				}

				OwnedBizModel model = controller.Model;
				VisitState visit = model?.visit;
				if (visit == null || visit.building == null)
				{
					return;
				}

				NormalizeOwnedBizVisitDriver(visit, source);
				OwnedBizDialog dialog = controller.View;
				if (dialog == null)
				{
					return;
				}

				bool isShowing = dialog.IsShowing;
				Traverse.Create(dialog).Method("RefreshFooter").GetValue();
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"ownedbiz-footer-forced-refresh",
					$"{visit.building.Id.id}:{visit.crew.peepId.id}:{visit.crew.VehicleID.id}:{source}:{isShowing}",
					$"ownedbiz-footer-forced-refresh building={visit.building.Id.id} crew={visit.crew.peepId.id} vehicle={visit.crew.VehicleID.id} source={source} dialogShowing={isShowing}",
					dedupe: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.RefreshOwnedBizDialogFooter: " + ex.Message);
			}
		}

		internal static void ForceOwnedBizInventoryLoading(object controllerInstance, string source)
		{
			try
			{
				if (!(controllerInstance is OwnedBizController controller))
				{
					return;
				}

				OwnedBizModel model = controller.Model;
				VisitState visit = model?.visit;
				if (visit == null || visit.building == null)
				{
					return;
				}

				object currentSlot = Traverse.Create(model).Field("currentSlot").GetValue();
				IModule module = currentSlot == null ? null : Traverse.Create(currentSlot).Field("module").GetValue<IModule>();
				if (!(module is InventoryModule))
				{
					return;
				}

				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null
					|| (!TryResolveOwnedBizPhysicalVehicleAccess(humanCrew, visit, out CrewAssignment transferDriver, source)
						&& !TryResolveOwnedBizFinalGoalVehicleAccess(humanCrew, visit.building, out transferDriver, out NodeID finalGoalNodeId, out NodeID finalGoalMatchedNodeId, out string finalGoalSource)))
				{
					if (humanCrew != null && TryResolveOwnedBizViewOnlyVehicleContext(humanCrew, out CrewAssignment viewOnlyDriver, out string viewOnlySource))
					{
						visit.SetCrew(viewOnlyDriver);
						controller.SetInventoryLoadingMode(loading: true, shutdown: false);
						Traverse.Create(controller.View).Method("ControllerRequestsSubviewRefresh").GetValue();
						RefreshOwnedBizDialogFooter(controller, source + ".ViewOnly");
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"ownedbiz-inventory-loading-viewonly-vehicle",
							$"{visit.building.Id.id}:{viewOnlyDriver.peepId.id}:{viewOnlyDriver.VehicleID.id}:{source}",
							$"ownedbiz-inventory-loading-viewonly-vehicle building={visit.building.Id.id} crew={viewOnlyDriver.peepId.id} vehicle={viewOnlyDriver.VehicleID.id} source={source} context={viewOnlySource} reason=transfer-blocked",
							dedupe: false);
						return;
					}

					visit.SetCrew(CrewAssignment.EMPTY);
					controller.SetInventoryLoadingMode(loading: false, shutdown: false);
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-inventory-loading-viewonly",
						$"{visit.building.Id.id}:{source}",
						$"ownedbiz-inventory-loading-viewonly building={visit.building.Id.id} source={source} reason=no-physical-vehicle-at-building",
						dedupe: false);
					return;
				}
				visit.SetCrew(transferDriver);
				if (!MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(transferDriver.VehicleID, visit.GetBldgNodeID()))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-inventory-loading-finalgoal",
						$"{visit.building.Id.id}:{transferDriver.peepId.id}:{transferDriver.VehicleID.id}:{source}",
						$"ownedbiz-inventory-loading-finalgoal building={visit.building.Id.id} crew={transferDriver.peepId.id} vehicle={transferDriver.VehicleID.id} source={source}",
						dedupe: false);
				}

				bool hadVehicle = Traverse.Create(model).Field("invstate").Field("vehicle").GetValue<InventoryModule>() != null;
				bool hadBuilding = Traverse.Create(model).Field("invstate").Field("building").GetValue<InventoryModule>() != null;
				controller.SetInventoryLoadingMode(loading: true, shutdown: false);
				Traverse.Create(controller.View).Method("ControllerRequestsSubviewRefresh").GetValue();
				RefreshOwnedBizDialogFooter(controller, source + ".ForceLoading");
				bool hasVehicle = Traverse.Create(model).Field("invstate").Field("vehicle").GetValue<InventoryModule>() != null;
				bool hasBuilding = Traverse.Create(model).Field("invstate").Field("building").GetValue<InventoryModule>() != null;

				MultiCrewVehicleHelper.LogVehicleAuthority(
					"ownedbiz-inventory-loading-forced",
					$"{visit.building.Id.id}:{visit.crew.peepId.id}:{visit.crew.VehicleID.id}:{source}:{hadVehicle}:{hasVehicle}:{hadBuilding}:{hasBuilding}",
					$"ownedbiz-inventory-loading-forced building={visit.building.Id.id} crew={visit.crew.peepId.id} vehicle={visit.crew.VehicleID.id} source={source} hadVehicle={hadVehicle} hasVehicle={hasVehicle} hadBuilding={hadBuilding} hasBuilding={hasBuilding}",
					dedupe: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.ForceOwnedBizInventoryLoading: " + ex.Message);
			}
		}

		internal static void ShowOwnedBizPhysicalAccessBlocked(VisitState visit, string source)
		{
			MultiCrewVehicleHelper.ShowHudMessage("Vehicle must be at that building.");
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"ownedbiz-inventory-tab-blocked",
				$"{visit?.building?.Id.id ?? 0UL}:{source}",
				$"ownedbiz-inventory-tab-blocked building={visit?.building?.Id.id ?? 0UL} source={source} reason=vehicle-not-physical",
				dedupe: false);
		}

		private static EntityID ResolveVisitVehicleDriverFirst(PlayerCrew humanCrew, VisitState visit, List<CrewAssignment> matches)
		{
			if (humanCrew == null || visit == null || matches == null || matches.Count <= 0)
			{
				return EntityID.INVALID;
			}

			if (visit.crew.IsValid && visit.crew.IsInVehicle && visit.crew.VehicleID.IsValid)
			{
				EntityID visitVehicleId = visit.crew.VehicleID;
				EntityID visitVehicleDriverId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, visitVehicleId);
				CrewAssignment matchedVisitVehicleDriver = matches.FirstOrDefault(match =>
					match.IsValid
					&& match.IsInVehicle
					&& match.VehicleID == visitVehicleId
					&& match.peepId == visitVehicleDriverId);
				if (matchedVisitVehicleDriver.IsValid && matchedVisitVehicleDriver.peepId.IsValid)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-passenger-visit-driver-priority",
						$"{visit.building.Id.id}:{matchedVisitVehicleDriver.peepId.id}:{visitVehicleId.id}",
						$"ownedbiz-passenger-visit-driver-priority building={visit.building.Id.id} crew={matchedVisitVehicleDriver.peepId.id} vehicle={visitVehicleId.id}",
						dedupe: false);
					return matchedVisitVehicleDriver.peepId;
				}
			}

			return OwnedBuildingInteractionSelectionPatch.ResolveDefaultInteractionCrew(humanCrew, matches);
		}

		internal static void NormalizeInventoryViewVisit(object viewInstance, string source)
		{
			try
			{
				if (viewInstance == null)
				{
					return;
				}

				OwnedBizModel model = Traverse.Create(viewInstance).Field("Model").GetValue<OwnedBizModel>();
				if (model?.visit == null)
				{
					return;
				}

				if (NormalizeOwnedBizVisitDriver(model.visit, source))
				{
					Traverse invstateTraverse = Traverse.Create(model).Field("invstate");
					object invstate = invstateTraverse.GetValue();
					if (invstate != null)
					{
						Traverse.Create(invstate).Method("RefreshOnCrewChange", model).GetValue();
					}
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-inventory-driver-normalized",
						$"{model.visit.building?.Id.id ?? 0UL}:{model.visit.crew.peepId.id}:{model.visit.crew.VehicleID.id}:{source}",
						$"ownedbiz-inventory-driver-normalized building={model.visit.building?.Id.id ?? 0UL} crew={model.visit.crew.peepId.id} vehicle={model.visit.crew.VehicleID.id} source={source}",
						dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.NormalizeInventoryViewVisit: " + ex.Message);
			}
		}

		internal static void NormalizeControllerVisit(object controllerInstance, string source)
		{
			try
			{
				if (!(controllerInstance is OwnedBizController controller))
				{
					return;
				}

				OwnedBizModel model = controller.Model;
				if (model?.visit == null)
				{
					return;
				}

				if (NormalizeOwnedBizVisitDriver(model.visit, source))
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"ownedbiz-controller-driver-normalized",
						$"{model.visit.building?.Id.id ?? 0UL}:{model.visit.crew.peepId.id}:{model.visit.crew.VehicleID.id}:{source}",
						$"ownedbiz-controller-driver-normalized building={model.visit.building?.Id.id ?? 0UL} crew={model.visit.crew.peepId.id} vehicle={model.visit.crew.VehicleID.id} source={source}",
						dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.NormalizeControllerVisit: " + ex.Message);
			}
		}

		private static void NormalizeOwnedBizDialogVisit(object dialogInstance, string source)
		{
			try
			{
				if (dialogInstance == null)
				{
					return;
				}

				object controller = Traverse.Create(dialogInstance).Property("Controller").GetValue();
				if (controller == null)
				{
					controller = Traverse.Create(dialogInstance).Field("_controller").GetValue();
				}
				NormalizeControllerVisit(controller, source);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBizVehicleVisitStatePatch.NormalizeOwnedBizDialogVisit: " + ex.Message);
			}
		}
	}

	internal static class PassengerCommandAuthorityPatch
	{
		private static readonly HashSet<CommandType> PassengerAllowedCommands = new HashSet<CommandType>
		{
			CommandType.Cancel
		};

		[HarmonyPostfix]
		internal static void GetAvailableCommandsPostfix(CrewAssignment crew, ref List<CommandButtonState> __result)
		{
			try
			{
				if (__result == null || !TryGetHumanVehiclePassenger(crew, out PlayerCrew humanCrew, out CrewAssignment latestCrew))
				{
					return;
				}

				int before = __result.Count;
				__result = __result
					.Where(state => state?.handler != null && PassengerAllowedCommands.Contains(state.handler.Type))
					.ToList();
				int removed = before - __result.Count;
				if (removed > 0)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"passenger-command-filter",
						$"{latestCrew.VehicleID.id}:{latestCrew.peepId.id}:{removed}",
						$"passenger-command-filter vehicle={latestCrew.VehicleID.id} crew={latestCrew.peepId.id} removed={removed}",
						dedupe: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] PassengerCommandAuthorityPatch.GetAvailableCommands: " + ex.Message);
			}
		}

		private static bool TryGetHumanVehiclePassenger(CrewAssignment crew, out PlayerCrew humanCrew, out CrewAssignment latestCrew)
		{
			humanCrew = G.GetHumanCrew();
			latestCrew = CrewAssignment.EMPTY;
			if (humanCrew == null || !crew.IsValid || !crew.peepId.IsValid)
			{
				return false;
			}

			latestCrew = humanCrew.GetCrewForPeep(crew.peepId);
			if (!latestCrew.IsValid)
			{
				latestCrew = crew;
			}

			return latestCrew.IsValid
				&& latestCrew.IsInVehicle
				&& latestCrew.VehicleID.IsValid
				&& latestCrew.peepId.IsValid
				&& !MultiCrewVehicleHelper.IsDriver(humanCrew, latestCrew);
		}
	}

	internal static class OwnedBizInventoryVehicleVisitPatch
	{
		[HarmonyPrefix]
		internal static bool OnActivatedPrefix(object __instance)
		{
			VisitState visit = OwnedBizVehicleVisitStatePatch.TryGetInventoryViewVisit(__instance);
			if (!OwnedBizVehicleVisitStatePatch.TryEnsureOwnedBizPhysicalVehicleAccess(visit, "ViewInventory.OnActivated"))
			{
				OwnedBizVehicleVisitStatePatch.ShowOwnedBizPhysicalAccessBlocked(visit, "ViewInventory.OnActivated");
				return false;
			}

			OwnedBizVehicleVisitStatePatch.NormalizeInventoryViewVisit(__instance, "ViewInventory.OnActivated");
			OwnedBizVehicleVisitStatePatch.RefreshOwnedBizDialogFooter(
				OwnedBizVehicleVisitStatePatch.TryGetInventoryViewController(__instance),
				"ViewInventory.OnActivated");
			return true;
		}

		[HarmonyPrefix]
		internal static bool RefreshAllPanelsPrefix(object __instance)
		{
			VisitState visit = OwnedBizVehicleVisitStatePatch.TryGetInventoryViewVisit(__instance);
			if (!OwnedBizVehicleVisitStatePatch.TryEnsureOwnedBizPhysicalVehicleAccess(visit, "ViewInventory.RefreshAllPanels"))
			{
				OwnedBizVehicleVisitStatePatch.ShowOwnedBizPhysicalAccessBlocked(visit, "ViewInventory.RefreshAllPanels");
				return false;
			}

			OwnedBizVehicleVisitStatePatch.NormalizeInventoryViewVisit(__instance, "ViewInventory.RefreshAllPanels");
			OwnedBizVehicleVisitStatePatch.RefreshOwnedBizDialogFooter(
				OwnedBizVehicleVisitStatePatch.TryGetInventoryViewController(__instance),
				"ViewInventory.RefreshAllPanels");
			return true;
		}

		[HarmonyPrefix]
		internal static void ControllerInventoryPrefix(object __instance)
		{
			OwnedBizVehicleVisitStatePatch.NormalizeControllerVisit(__instance, "OwnedBizController.Inventory");
		}

		[HarmonyPrefix]
		internal static bool CanLoadAtLeastOnePrefix(object __instance, ref bool __result)
		{
			VisitState visit = OwnedBizVehicleVisitStatePatch.TryGetControllerVisit(__instance);
			if (OwnedBizVehicleVisitStatePatch.CanOwnedBizInventoryTransferNow(visit, "OwnedBizController.CanLoadAtLeastOne"))
			{
				return true;
			}

			__result = false;
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"ownedbiz-inventory-transfer-card-blocked",
				$"{visit?.building?.Id.id ?? 0UL}:{visit?.crew.VehicleID.id ?? 0UL}",
				$"ownedbiz-inventory-transfer-card-blocked building={visit?.building?.Id.id ?? 0UL} crew={visit?.crew.peepId.id ?? 0UL} vehicle={visit?.crew.VehicleID.id ?? 0UL} reason=viewonly-not-at-building",
				dedupe: true);
			return false;
		}

		[HarmonyPrefix]
		internal static bool DoPerformLoadingPrefix(object __instance)
		{
			VisitState visit = OwnedBizVehicleVisitStatePatch.TryGetControllerVisit(__instance);
			if (OwnedBizVehicleVisitStatePatch.CanOwnedBizInventoryTransferNow(visit, "OwnedBizController.DoPerformLoading"))
			{
				return true;
			}

			MultiCrewVehicleHelper.ShowHudMessage("Vehicle must be at that building.");
			MultiCrewVehicleHelper.LogVehicleAuthority(
				"ownedbiz-inventory-transfer-click-blocked",
				$"{visit?.building?.Id.id ?? 0UL}:{visit?.crew.VehicleID.id ?? 0UL}",
				$"ownedbiz-inventory-transfer-click-blocked building={visit?.building?.Id.id ?? 0UL} crew={visit?.crew.peepId.id ?? 0UL} vehicle={visit?.crew.VehicleID.id ?? 0UL} reason=viewonly-not-at-building",
				dedupe: false);
			return false;
		}

		[HarmonyPrefix]
		internal static bool ModuleButtonClickPrefix(object __instance, object __0)
		{
			if (!OwnedBizVehicleVisitStatePatch.IsInventoryModuleContext(__0))
			{
				return true;
			}

			VisitState visit = OwnedBizVehicleVisitStatePatch.TryGetControllerVisit(__instance);
			if (OwnedBizVehicleVisitStatePatch.TryEnsureOwnedBizPhysicalVehicleAccess(visit, "OwnedBizController.OnModuleButtonClick"))
			{
				OwnedBizVehicleVisitStatePatch.RefreshOwnedBizDialogFooter(__instance, "OwnedBizController.OnModuleButtonClick");
				return true;
			}

			OwnedBizVehicleVisitStatePatch.ShowOwnedBizPhysicalAccessBlocked(visit, "OwnedBizController.OnModuleButtonClick");
			return false;
		}

		[HarmonyPostfix]
		internal static void ModuleButtonClickPostfix(object __instance, object __0)
		{
			if (!OwnedBizVehicleVisitStatePatch.IsInventoryModuleContext(__0))
			{
				return;
			}

			OwnedBizVehicleVisitStatePatch.ForceOwnedBizInventoryLoading(__instance, "OwnedBizController.OnModuleButtonClick.Postfix");
		}
	}

	internal static class HumanVehicleVisitStateNodePatch
	{
		private static bool TryGetOwnedBuildingFinalGoalNode(VisitState visit, out Node node, out string source)
		{
			node = null;
			source = "none";
			if (visit?.crew.IsValid != true || !visit.crew.IsInVehicle || !visit.crew.VehicleID.IsValid || visit.building == null)
			{
				return false;
			}

			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (humanPlayer?.territory == null)
			{
				return false;
			}

			bool ownedTarget = humanPlayer.territory.IsControlled(visit.building)
				|| humanPlayer.territory.Safehouse == visit.building.Id;
			if (!ownedTarget)
			{
				return false;
			}

			if (!CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(visit.building, out _, out List<NodeID> comparisonNodeIds, out _)
				|| comparisonNodeIds == null)
			{
				return false;
			}

			comparisonNodeIds = comparisonNodeIds
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList();
			OwnedBuildingInteractionSelectionPatch.AddOwnedBuildingFrontageComparisonNodes(visit.building, comparisonNodeIds);
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			bool hasPendingRoute = MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(visit.crew.VehicleID, out _, out NodeID pendingNodeId, out NodeID goalNodeId)
				&& (pendingNodeId.IsValid || goalNodeId.IsValid);
			if (hasPendingRoute)
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"visit-building-final-goal-blocked",
					$"{visit.crew.VehicleID.id}:{pendingNodeId}:{goalNodeId}:{visit.building.Id.id}",
					$"visit-building-final-goal-blocked vehicle={visit.crew.VehicleID.id} pendingNode={pendingNodeId} finalGoal={goalNodeId} building={visit.building.Id.id} reason=route-active",
					dedupe: false);
				return false;
			}

			return false;
		}

		[HarmonyPrefix]
		internal static bool GetCrewNodePrefix(VisitState __instance, ref Node __result)
		{
			try
			{
					if (__instance?.crew.IsValid != true || !__instance.crew.IsInVehicle || !__instance.pid.IsHumanPlayer)
					{
						return true;
					}
					if (TryGetOwnedBuildingFinalGoalNode(__instance, out Node targetBuildingNode, out string targetSource)
						&& targetBuildingNode != null)
					{
						__result = targetBuildingNode;
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"visit-building-final-goal",
							$"{__instance.crew.VehicleID.id}:{targetBuildingNode.id}:{targetSource}:{__instance.building.Id.id}",
							$"visit-building-final-goal vehicle={__instance.crew.VehicleID.id} node={targetBuildingNode.id} source={targetSource} building={__instance.building.Id.id}",
							dedupe: false);
						return false;
					}
					string contextTag = MultiCrewVehicleHelper.GetPendingBuildingInteractionScopeSource();
					if (MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNode(__instance.crew.VehicleID, out Node committedNode, out _, contextTag)
						&& committedNode != null)
					{
						__result = committedNode;
						return false;
					}
					if (MultiCrewVehicleHelper.ShouldFailClosedHumanVehicleInteraction(__instance.crew.VehicleID, contextTag, out _))
					{
						__result = null;
						return false;
					}
					if (!MultiCrewVehicleHelper.TryGetCrewCommandNode(__instance.crew, out Node node, MultiCrewVehicleHelper.GetActualHumanInteractionCrewNodeResolutionMode()) || node == null)
					{
						return true;
					}
					__result = node;
					return false;
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] HumanVehicleVisitStateNodePatch.GetCrewNode: " + ex.Message);
					return true;
				}
			}

			[HarmonyPrefix]
			internal static bool GetCrewNodeIdPrefix(VisitState __instance, ref NodeID __result)
			{
				try
				{
					if (__instance?.crew.IsValid != true || !__instance.crew.IsInVehicle || !__instance.pid.IsHumanPlayer)
					{
						return true;
					}
					if (TryGetOwnedBuildingFinalGoalNode(__instance, out Node targetBuildingNode, out string targetSource)
						&& targetBuildingNode != null)
					{
						__result = targetBuildingNode.id;
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"visit-building-final-goal",
							$"{__instance.crew.VehicleID.id}:{targetBuildingNode.id}:{targetSource}:{__instance.building.Id.id}:id",
							$"visit-building-final-goal vehicle={__instance.crew.VehicleID.id} node={targetBuildingNode.id} source={targetSource} building={__instance.building.Id.id} field=node-id",
							dedupe: false);
						return false;
					}
					string contextTag = MultiCrewVehicleHelper.GetPendingBuildingInteractionScopeSource();
					if (MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNode(__instance.crew.VehicleID, out Node committedNode, out _, contextTag)
						&& committedNode != null)
					{
						__result = committedNode.id;
						return false;
					}
					if (MultiCrewVehicleHelper.ShouldFailClosedHumanVehicleInteraction(__instance.crew.VehicleID, contextTag, out _))
					{
						__result = NodeID.INVALID;
						return false;
					}
					if (!MultiCrewVehicleHelper.TryGetCrewCommandNode(__instance.crew, out Node node, MultiCrewVehicleHelper.GetActualHumanInteractionCrewNodeResolutionMode()) || node == null)
					{
						return true;
					}
					__result = node.id;
					return false;
				}
				catch (Exception ex)
				{
					Debug.LogWarning("[GameplayTweaks] HumanVehicleVisitStateNodePatch.GetCrewNodeID: " + ex.Message);
					return true;
				}
			}
		}

	internal static class HumanVehicleConvoAttackPopupPatch
	{
		private static readonly PropertyInfo VisitProperty = typeof(ConvoCallbacks).GetProperty("Visit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		[HarmonyPrefix]
		internal static bool Prefix(ConvoCallbacks __instance, ConvoButton button, ref OnClickResult __result)
		{
			try
			{
				VisitState visit = VisitProperty?.GetValue(__instance, null) as VisitState;
				ConvoDataCombat combatData = null;
				try
				{
					combatData = button?.GetData<ConvoDataCombat>();
				}
				catch
				{
				}

				CrewAssignment attacker = combatData != null && combatData.attacker.IsValid && combatData.attacker.IsInVehicle
					? combatData.attacker
					: visit?.crew ?? CrewAssignment.EMPTY;
				PlayerInfo human = global::Game.Game.ctx?.players?.Human;
				if (human?.crew == null)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority(
						"attack-popup-prefix-pass",
						"no-human-crew",
						"attack-popup-prefix-pass reason=no-human-crew",
						dedupe: false);
					return true;
				}
				Entity attackerPeep = attacker.GetPeep();
				PlayerID attackerPid = attackerPeep?.data?.agent?.pid ?? PlayerID.INVALID;
				if (!attacker.IsValid || !attacker.IsInVehicle || !attackerPid.IsHumanPlayer)
				{
					if (!TryResolveSelectedVehicleAttackAttacker(human.crew, out attacker, out string attackerSource))
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"attack-popup-prefix-pass",
							$"{attacker.peepId.id}:{attacker.VehicleID.id}:no-selected-driver",
							$"attack-popup-prefix-pass crew={attacker.peepId.id} vehicle={attacker.VehicleID.id} reason=no-selected-driver",
							dedupe: false);
						return true;
					}

					attackerPeep = attacker.GetPeep();
					attackerPid = attackerPeep?.data?.agent?.pid ?? PlayerID.INVALID;
					if (!attacker.IsValid || !attacker.IsInVehicle || !attackerPid.IsHumanPlayer)
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"attack-popup-prefix-pass",
							$"{attacker.peepId.id}:{attacker.VehicleID.id}:normalized-invalid",
							$"attack-popup-prefix-pass crew={attacker.peepId.id} vehicle={attacker.VehicleID.id} reason=normalized-invalid",
							dedupe: false);
						return true;
					}

					MultiCrewVehicleHelper.LogVehicleAuthority(
						"attack-popup-attacker-normalized",
						$"{attacker.peepId.id}:{attacker.VehicleID.id}:{attackerSource}",
						$"attack-popup-attacker-normalized crew={attacker.peepId.id} vehicle={attacker.VehicleID.id} source={attackerSource}",
						dedupe: false);
				}
				NodeID staleNodeId = attackerPeep?.data?.agent?.nid ?? NodeID.INVALID;
				bool previewOnly = false;
				Entity primaryTarget = combatData != null && combatData.target.IsValid
					? combatData.target.GetPeep()
					: null;
				primaryTarget = primaryTarget ?? visit?.npc ?? visit?.peep;
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"attack-popup-prefix-enter",
					$"{attacker.peepId.id}:{attacker.VehicleID.id}:{primaryTarget?.Id.id ?? 0UL}",
					$"attack-popup-prefix-enter crew={attacker.peepId.id} vehicle={attacker.VehicleID.id} target={primaryTarget?.Id.id ?? 0UL}",
					dedupe: false);
				Node resolvedNode = null;
				string resolvedSource = "none";
				if (TryGetStrictAttackPreviewBeforeArrival(attacker, primaryTarget, out resolvedNode, out resolvedSource))
				{
					previewOnly = true;
				}
				else if (!MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNode(attacker.VehicleID, out resolvedNode, out resolvedSource, "ConvoCallbacks.ExecuteCombat") || resolvedNode == null)
				{
					if (TryGetAttackPopupPreviewNode(attacker, primaryTarget, out resolvedNode, out resolvedSource))
					{
						previewOnly = true;
					}
					else if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(attacker.VehicleID, out _, out NodeID pendingNodeId, out NodeID goalNodeId)
						&& (pendingNodeId.IsValid || goalNodeId.IsValid))
					{
						MultiCrewVehicleHelper.LogVehicleAuthority(
							"actual-convo-preview-blocked",
							$"{attacker.VehicleID.id}:{pendingNodeId}:{goalNodeId}",
							$"actual-convo-preview-blocked vehicle={attacker.VehicleID.id} pendingNode={pendingNodeId} finalGoal={goalNodeId} reason=no-committed-attack-node",
							dedupe: false);
						__result = OnClickResult.END_CONVERSATION;
						return false;
					}
					return true;
				}
				MultiCrewVehicleHelper.CrewNodeResolutionMode mode = previewOnly
					? MultiCrewVehicleHelper.CrewNodeResolutionMode.QueuedFinalGoalAllowed
					: MultiCrewVehicleHelper.GetActualHumanInteractionCrewNodeResolutionMode();
				List<Entity> mycrew = MultiCrewVehicleHelper.GetHumanCombatPopupCrewAtNode(resolvedNode.id, mode);
				if (mycrew == null || mycrew.Count == 0)
				{
					Entity fallbackPeep = attacker.GetPeep();
					if (fallbackPeep != null)
					{
						mycrew = new List<Entity> { fallbackPeep };
					}
				}
				List<Entity> enemies = GetAttackPopupEnemiesAtNode(human.PID, resolvedNode.id, primaryTarget, attacker.VehicleID);
				string logTag = previewOnly ? "attack-popup-preview-node" : "attack-node";
				MultiCrewVehicleHelper.LogVehicleAuthority(
					logTag,
					$"{attacker.VehicleID.id}:{staleNodeId}:{resolvedNode.id}:{resolvedSource}:{enemies.Count}",
					$"{logTag} crew={attacker.peepId.id} vehicle={attacker.VehicleID.id} staleNode={staleNodeId} resolvedNode={resolvedNode.id} resolvedSource={resolvedSource} enemies={enemies.Count}",
					dedupe: false);
				global::Game.Game.serv.ui.AddPopup(new CombatPopupPlanning(mycrew, enemies));
				__result = OnClickResult.END_CONVERSATION;
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] HumanVehicleConvoAttackPopupPatch: " + ex.Message);
				return true;
			}
		}

		private static bool TryGetStrictAttackPreviewBeforeArrival(CrewAssignment attacker, Entity primaryTarget, out Node resolvedNode, out string resolvedSource)
		{
			resolvedNode = null;
			resolvedSource = "none";
			if (!attacker.IsValid || !attacker.IsInVehicle || !attacker.VehicleID.IsValid)
			{
				return false;
			}

			if (!MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(attacker.VehicleID, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
				|| !goalNodeId.IsValid
				|| (expectedNodeId.IsValid && expectedNodeId != goalNodeId)
				|| !MultiCrewVehicleHelper.IsHumanVehicleTravelActive(attacker.VehicleID)
				|| MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(attacker.VehicleID, goalNodeId))
			{
				return false;
			}

			if (TryGetAttackPopupPreviewNode(attacker, primaryTarget, out resolvedNode, out resolvedSource))
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"attack-popup-preview-forced-before-arrival",
					$"{attacker.VehicleID.id}:{expectedNodeId}:{goalNodeId}:{primaryTarget?.Id.id ?? 0UL}",
					$"attack-popup-preview-forced-before-arrival vehicle={attacker.VehicleID.id} expectedNode={expectedNodeId} finalGoal={goalNodeId} target={primaryTarget?.Id.id ?? 0UL} source={resolvedSource}",
					dedupe: false);
				return true;
			}

			MultiCrewVehicleHelper.LogVehicleAuthority(
				"attack-popup-preview-forced-miss",
				$"{attacker.VehicleID.id}:{expectedNodeId}:{goalNodeId}:{primaryTarget?.Id.id ?? 0UL}",
				$"attack-popup-preview-forced-miss vehicle={attacker.VehicleID.id} expectedNode={expectedNodeId} finalGoal={goalNodeId} target={primaryTarget?.Id.id ?? 0UL}",
				dedupe: false);
			return false;
		}

		private static bool TryResolveSelectedVehicleAttackAttacker(PlayerCrew humanCrew, out CrewAssignment attacker, out string source)
		{
			attacker = CrewAssignment.EMPTY;
			source = "none";
			if (humanCrew == null)
			{
				return false;
			}

			if (!MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out string selectedSource)
				|| !selectedVehicleId.IsValid)
			{
				return false;
			}

			if (!OwnedBuildingInteractionSelectionPatch.TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out attacker)
				|| !attacker.IsValid
				|| !attacker.IsInVehicle
				|| attacker.VehicleID != selectedVehicleId)
			{
				return false;
			}

			source = string.IsNullOrWhiteSpace(selectedSource) ? "selected-vehicle-driver" : selectedSource + "-driver";
			return true;
		}

		private static bool TryGetAttackPopupPreviewNode(CrewAssignment attacker, Entity primaryTarget, out Node resolvedNode, out string resolvedSource)
		{
			resolvedNode = null;
			resolvedSource = "none";
			if (!attacker.IsValid || !attacker.IsInVehicle || !attacker.VehicleID.IsValid)
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(attacker.VehicleID, out NodeID previewNodeId, out resolvedSource, "attack-popup-preview")
				&& previewNodeId.IsValid
				&& MultiCrewVehicleHelper.IsHumanVehicleFinalGoalScopePreviewAllowed(attacker.VehicleID, previewNodeId, resolvedSource)
				&& IsAttackTargetAtPreviewNode(primaryTarget, previewNodeId))
			{
				resolvedNode = previewNodeId.FindNode();
				if (resolvedNode != null)
				{
					return true;
				}
			}

			if (!MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(attacker.VehicleID, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
				|| !goalNodeId.IsValid
				|| (expectedNodeId.IsValid && expectedNodeId != goalNodeId))
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"attack-popup-preview-miss",
					$"{attacker.VehicleID.id}:{expectedNodeId}:{goalNodeId}:{primaryTarget?.Id.id ?? 0UL}",
					$"attack-popup-preview-miss vehicle={attacker.VehicleID.id} expectedNode={expectedNodeId} finalGoal={goalNodeId} target={primaryTarget?.Id.id ?? 0UL} reason=no-strict-final-goal",
					dedupe: false);
				return false;
			}

			resolvedNode = goalNodeId.FindNode();
			resolvedSource = "attack-final-goal";
			if (resolvedNode == null)
			{
				return false;
			}

			bool strictTarget = IsAttackTargetAtPreviewNode(primaryTarget, goalNodeId);
			if (!strictTarget && !ShouldForceIncludeAttackPopupTarget(attacker.VehicleID, attackerPid: G.GetHumanPlayer()?.PID ?? PlayerID.INVALID, primaryTarget))
			{
				MultiCrewVehicleHelper.LogVehicleAuthority(
					"attack-popup-preview-miss",
					$"{attacker.VehicleID.id}:{expectedNodeId}:{goalNodeId}:{primaryTarget?.Id.id ?? 0UL}:target",
					$"attack-popup-preview-miss vehicle={attacker.VehicleID.id} expectedNode={expectedNodeId} finalGoal={goalNodeId} target={primaryTarget?.Id.id ?? 0UL} reason=invalid-target",
					dedupe: false);
				return false;
			}

			MultiCrewVehicleHelper.LogVehicleAuthority(
				"attack-popup-preview-direct",
				$"{attacker.VehicleID.id}:{goalNodeId}:{primaryTarget?.Id.id ?? 0UL}",
				$"attack-popup-preview-direct vehicle={attacker.VehicleID.id} node={goalNodeId} target={primaryTarget?.Id.id ?? 0UL} source={resolvedSource} strictTarget={strictTarget}",
				dedupe: false);
			return true;
		}

		private static bool IsAttackTargetAtPreviewNode(Entity target, NodeID nodeId)
		{
			if (target == null || !target.Id.IsValid || !nodeId.IsValid)
			{
				return false;
			}

			NodeID targetNodeId = target.data?.agent?.nid ?? NodeID.INVALID;
			if (targetNodeId == nodeId)
			{
				return true;
			}

			PlayerID targetPid = target.data?.agent?.pid ?? PlayerID.INVALID;
			if (targetPid.IsValid && target.components?.mobile != null)
			{
				if (MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(target.Id, out NodeID targetVehicleNodeId, out _)
					&& targetVehicleNodeId == nodeId)
				{
					return true;
				}
			}

			PlayerID attackerPid = G.GetHumanPlayer()?.PID ?? PlayerID.INVALID;
			return attackerPid.IsValid
				&& (CombatManager.GetAttackTargetsAtNode(attackerPid, nodeId) ?? Enumerable.Empty<EntityID>())
					.Any(targetId => targetId == target.Id);
		}

		private static List<Entity> GetAttackPopupEnemiesAtNode(PlayerID attackerPid, NodeID nodeId, Entity primaryTarget = null, EntityID attackerVehicleId = default)
		{
			var enemies = new List<Entity>();
			var seen = new HashSet<ulong>();
			foreach (EntityID attackTargetId in CombatManager.GetAttackTargetsAtNode(attackerPid, nodeId) ?? Enumerable.Empty<EntityID>())
			{
				Entity attackTarget = attackTargetId.FindEntity();
				if (attackTarget == null || !attackTarget.Id.IsValid || !seen.Add(attackTarget.Id.id))
				{
					continue;
				}
				enemies.Add(attackTarget);
			}
			if (IsValidDirectAttackPopupTarget(attackerPid, primaryTarget) && seen.Add(primaryTarget.Id.id))
			{
				enemies.Insert(0, primaryTarget);
			}
			else if (ShouldForceIncludeAttackPopupTarget(attackerVehicleId, attackerPid, primaryTarget) && seen.Add(primaryTarget.Id.id))
			{
				enemies.Insert(0, primaryTarget);
				Debug.Log($"[GameplayTweaks] Forced primary target into attack popup attackerPid={attackerPid.id} target={primaryTarget.Id.id} node={nodeId}");
			}
			Debug.Log($"[GameplayTweaks] Attack popup enemies built attackerPid={attackerPid.id} node={nodeId} primaryTarget={(primaryTarget?.Id.id ?? 0UL)} count={enemies.Count}");
			return enemies;
		}

		private static bool ShouldForceIncludeAttackPopupTarget(EntityID attackerVehicleId, PlayerID attackerPid, Entity entity)
		{
			if (entity == null || !entity.Id.IsValid)
			{
				return false;
			}
			PlayerID targetPid = entity.data?.agent?.pid ?? PlayerID.INVALID;
			if (!targetPid.IsValid || targetPid == attackerPid)
			{
				return false;
			}
			if (entity.data?.person?.IsAlive != true || entity.components?.agent?.HasHealthPointsLeft != true)
			{
				return false;
			}
			PlayerInfo targetPlayer = targetPid.FindPlayer();
			if (targetPlayer?.IsJustCop == true)
			{
				return true;
			}

			return attackerVehicleId.IsValid
				&& MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(attackerVehicleId, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
				&& goalNodeId.IsValid
				&& (!expectedNodeId.IsValid || expectedNodeId == goalNodeId);
		}

		private static bool IsValidDirectAttackPopupTarget(PlayerID attackerPid, Entity entity)
		{
			if (entity == null || !entity.Id.IsValid)
			{
				return false;
			}
			PlayerID targetPid = entity.data?.agent?.pid ?? PlayerID.INVALID;
			if (!targetPid.IsValid || targetPid == attackerPid)
			{
				return false;
			}
			PlayerInfo targetPlayer = targetPid.FindPlayer();
			if (targetPlayer == null)
			{
				return false;
			}
			bool isCopOrFed = targetPlayer.IsCopOrFed;
			if (!isCopOrFed)
			{
				PlayerCrew targetCrew = targetPlayer.crew;
				CrewAssignment targetAssignment = (targetCrew != null) ? targetCrew.GetCrewForPeep(entity.Id) : CrewAssignment.EMPTY;
				if (!targetAssignment.IsValid && targetCrew != null)
				{
					targetAssignment = targetCrew.GetCrewForTarget(entity.Id);
				}
				if (!targetAssignment.IsValid || !targetAssignment.IsInVehicle)
				{
					return false;
				}
			}
			if (entity.data?.person?.IsAlive != true)
			{
				return false;
			}
			bool isAlive = entity.components?.agent?.HasHealthPointsLeft == true;
			if (isAlive)
			{
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"attack-popup-direct-target attackerPid={attackerPid.id} target={entity.Id.id} targetPid={targetPid.id} copOrFed={isCopOrFed}");
			}
			return isAlive;
		}
	}

	internal static class EnemyVehicleCrewPickPatch
	{
		private static readonly FieldInfo CrewPickPeepField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_peep");
		private static readonly FieldInfo CrewPickCrewField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_crew");
		private static readonly FieldInfo CrewPickVehicleField = AccessTools.Field(AccessTools.TypeByName("Game.UI.Session.Picks.CrewPick"), "_vehicle");
		private static readonly Vector2 HiddenScreenVector = new Vector2(-100000f, -100000f);
		private static readonly Vector3 HiddenSceneVector = new Vector3(-100000f, -100000f, -100000f);

		[HarmonyPrefix]
		internal static bool OnClickPrefix(object __instance)
		{
			try
			{
				if (TryBlockUnmetAiCrewPick(__instance, "click"))
				{
					return false;
				}
				if (TryBlockEmptyEnemyVehiclePick(__instance, out Entity blockedEntity, out EntityID blockedVehicleId))
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-skip reason=no-inspectable-occupants clickedPeep={blockedEntity?.Id.id ?? 0UL} vehicle={blockedVehicleId.id}");
					return false;
				}
				if (!TryResolveEnemyVehiclePick(__instance, out Entity clickedPeep, out PlayerCrew ownerCrew, out CrewAssignment clickedCrew, out Entity resolvedPeep))
				{
					return true;
				}
				if (resolvedPeep == null)
				{
					LogSkip(clickedPeep, "resolved-peep-missing");
					return true;
				}
				if (resolvedPeep.Id == clickedPeep.Id)
				{
					return true;
				}
				if (!MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(clickedCrew.VehicleID, out NodeID nodeId, out _))
				{
					LogSkip(clickedPeep, "node-missing");
					return true;
				}
				if (!MultiCrewVehicleHelper.TryFindActualHumanCrewAtNode(nodeId, out CrewAssignment matchingCrew, out int matchingCount, out string sourceTag)
					&& !TryFindPreviewHumanCrewForAttackNode(nodeId, out matchingCrew, out matchingCount, out sourceTag))
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-skip reason=no-human-crew clickedPeep={clickedPeep.Id.id} node={nodeId} matches={matchingCount} source={sourceTag}");
					return true;
				}
				if (!MultiCrewVehicleHelper.TryStartCrewVisitWithPendingSelectionScope(resolvedPeep, matchingCrew))
				{
					LogSkip(clickedPeep, "controller-missing");
					return true;
				}
				if (ownerCrew != null)
				{
					MultiCrewVehicleHelper.RefreshEnemyVehicleRepresentative(ownerCrew, clickedCrew.VehicleID, "hostile-normalize");
				}
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-normalized vehicle={clickedCrew.VehicleID.id} clickedPeep={clickedPeep.Id.id} resolvedPeep={resolvedPeep.Id.id} matches={matchingCount} source={sourceTag}");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.OnClick: " + ex.Message);
				return true;
			}
		}

		private static bool TryFindPreviewHumanCrewForAttackNode(NodeID nodeId, out CrewAssignment matchingCrew, out int matchingCount, out string sourceTag)
		{
			matchingCrew = CrewAssignment.EMPTY;
			matchingCount = 0;
			sourceTag = "attack-preview-none";
			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null || !nodeId.IsValid)
			{
				return false;
			}

			if (!MultiCrewVehicleHelper.TryGetHumanCrewUsableForScopePreviewAtNode(humanCrew, nodeId, out List<CrewAssignment> previewMatches, out string previewSource)
				|| previewMatches == null
				|| previewMatches.Count <= 0)
			{
				sourceTag = string.IsNullOrWhiteSpace(previewSource) ? "attack-preview-miss" : previewSource;
				return false;
			}

			bool finalGoalPreview = !string.IsNullOrWhiteSpace(previewSource)
				&& (previewSource.IndexOf("final-goal", StringComparison.OrdinalIgnoreCase) >= 0
					|| string.Equals(previewSource, "queued-resume-goal", StringComparison.Ordinal));
			if (!finalGoalPreview)
			{
				sourceTag = previewSource;
				return false;
			}

			matchingCount = previewMatches.Count;
			CrewAssignment bossCrew = humanCrew.GetCrewForPlayerPeep();
			CrewAssignment bossMatch = previewMatches.FirstOrDefault(item => item.IsValid && bossCrew.IsValid && item.peepId == bossCrew.peepId);
			matchingCrew = bossMatch.IsValid
				? bossMatch
				: previewMatches.FirstOrDefault(item => item.IsValid && item.peepId.IsValid);
			if (!matchingCrew.IsValid)
			{
				sourceTag = previewSource;
				return false;
			}

			sourceTag = "attack-preview-" + previewSource;
			GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-preview crew={matchingCrew.peepId.id} node={nodeId} matches={matchingCount} source={sourceTag}");
			return true;
		}

		[HarmonyPrefix]
		internal static void MakeMouseoverPrefix(object __instance)
		{
			TryNormalizeCrewPickUiState(__instance, "mouseover");
		}

		[HarmonyPostfix]
		internal static void MakeMouseoverPostfix(object __instance, ref string __result)
		{
			try
			{
				Entity indicatorEntity = CrewPickVehicleField?.GetValue(__instance) as Entity ?? CrewPickPeepField?.GetValue(__instance) as Entity;
				if (!MultiCrewVehicleHelper.TryGetEnemyVehicleCrewIndicator(indicatorEntity, out string indicator, out _))
				{
					return;
				}
				if (string.IsNullOrWhiteSpace(__result))
				{
					__result = indicator;
					return;
				}
				if (__result.IndexOf(indicator, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return;
				}
				__result = __result.TrimEnd() + Environment.NewLine + indicator;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.MakeMouseover: " + ex.Message);
			}
		}

		[HarmonyFinalizer]
		internal static Exception MakeMouseoverFinalizer(object __instance, ref string __result, Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			try
			{
				if (TryBuildFallbackMouseover(__instance, out string fallbackText, out string reason))
				{
					__result = fallbackText ?? string.Empty;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"crew-pick-mouseover-fallback reason={reason}");
					return null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.MakeMouseoverFinalizer: " + ex.Message);
			}

			return __exception;
		}

		[HarmonyPrefix]
		internal static bool MakeSceneVectorPrefix(object __instance, ref Vector3 __result)
		{
			if (TryBlockUnmetAiCrewPick(__instance, "scene"))
			{
				__result = HiddenSceneVector;
				return false;
			}
			TryNormalizeCrewPickUiState(__instance, "scene");
			try
			{
				if (TryBlockEmptyEnemyVehiclePick(__instance, out _, out _)
					&& TryPruneInvalidCrewPick(__instance, "scene-prefix-empty"))
				{
					ForceHideCrewPick(__instance);
					__result = HiddenSceneVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-scene-prefix-pruned-empty");
					return false;
				}
				bool missingPickTarget = false;
				try
				{
					if (__instance is BasePick basePick)
					{
						missingPickTarget = !basePick.Target.eid.IsValid;
					}
				}
				catch
				{
				}
				if (missingPickTarget && TryBuildFallbackSceneVector(__instance, out __result, out string reason))
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"crew-pick-scene-prefix-fallback reason={reason} scene={__result}");
					return false;
				}
				if (missingPickTarget && TryPruneInvalidCrewPick(__instance, "scene-prefix"))
				{
					ForceHideCrewPick(__instance);
					__result = HiddenSceneVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-scene-prefix-pruned");
					return false;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.MakeSceneVectorPrefix: " + ex.Message);
				ForceHideCrewPick(__instance);
				__result = HiddenSceneVector;
				return false;
			}

			return true;
		}

		[HarmonyPrefix]
		internal static bool MakeScreenVectorPrefix(object __instance, ref Vector2 __result)
		{
			if (TryBlockUnmetAiCrewPick(__instance, "screen"))
			{
				__result = HiddenScreenVector;
				return false;
			}
			TryNormalizeCrewPickUiState(__instance, "screen");
			try
			{
				if (TryBlockEmptyEnemyVehiclePick(__instance, out _, out _)
					&& TryPruneInvalidCrewPick(__instance, "screen-prefix-empty"))
				{
					ForceHideCrewPick(__instance);
					__result = HiddenScreenVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-screen-prefix-pruned-empty");
					return false;
				}

				bool missingPickTarget = false;
				try
				{
					if (__instance is BasePick basePick)
					{
						missingPickTarget = !basePick.Target.eid.IsValid;
					}
				}
				catch
				{
				}

				if (missingPickTarget && TryPruneInvalidCrewPick(__instance, "screen-prefix"))
				{
					ForceHideCrewPick(__instance);
					__result = HiddenScreenVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-screen-prefix-pruned");
					return false;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.MakeScreenVectorPrefix: " + ex.Message);
				ForceHideCrewPick(__instance);
				__result = HiddenScreenVector;
				return false;
			}

			return true;
		}

		[HarmonyFinalizer]
		internal static Exception MakeSceneVectorFinalizer(object __instance, ref Vector3 __result, Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			try
			{
				if (TryBuildFallbackSceneVector(__instance, out __result, out string reason))
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"crew-pick-scene-fallback reason={reason} scene={__result}");
					return null;
				}

				if (TryPruneInvalidCrewPick(__instance, "scene-finalizer"))
				{
					ForceHideCrewPick(__instance);
					__result = HiddenSceneVector;
					return null;
				}

				if (__exception is NullReferenceException)
				{
					ForceHideCrewPick(__instance);
					__result = HiddenSceneVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-scene-nullref-swallowed");
					return null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.MakeSceneVectorFinalizer: " + ex.Message);
				if (__exception is NullReferenceException)
				{
					ForceHideCrewPick(__instance);
					__result = HiddenSceneVector;
					return null;
				}
			}

			return __exception;
		}

		[HarmonyFinalizer]
		internal static Exception MakeScreenVectorFinalizer(object __instance, ref Vector2 __result, Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}

			try
			{
				TryNormalizeCrewPickUiState(__instance, "screen-finalizer");
				if (TryPruneInvalidCrewPick(__instance, "screen-finalizer"))
				{
					ForceHideCrewPick(__instance);
					__result = HiddenScreenVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-screen-pruned");
					return null;
				}

				if (__exception is NullReferenceException)
				{
					ForceHideCrewPick(__instance);
					__result = HiddenScreenVector;
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", "crew-pick-screen-nullref-swallowed");
					return null;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewPickPatch.MakeScreenVectorFinalizer: " + ex.Message);
				if (__exception is NullReferenceException)
				{
					ForceHideCrewPick(__instance);
					__result = HiddenScreenVector;
					return null;
				}
			}

			return __exception;
		}

		private static void ForceHideCrewPick(object pickInstance)
		{
			if (!(pickInstance is BasePick basePick))
			{
				return;
			}

			try
			{
				Image portraitImage = basePick.go?.transform.Find("Button/Portrait")?.GetComponent<Image>();
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
				basePick.active = false;
			}
			catch
			{
			}

			try
			{
				if (basePick.go != null)
				{
					basePick.go.SetActive(false);
				}
			}
			catch
			{
			}
		}

		private static bool TryBlockEmptyEnemyVehiclePick(object pickInstance, out Entity clickedPeep, out EntityID vehicleId)
		{
			clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
			Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
			vehicleId = vehicle?.Id ?? EntityID.INVALID;
			Entity stateEntity = vehicle ?? clickedPeep;
			if (!MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(stateEntity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
			{
				return false;
			}
			vehicleId = state.VehicleID;
			return !state.HasInspectableOccupants;
		}

		private static bool TryResolveEnemyVehiclePick(object pickInstance, out Entity clickedPeep, out PlayerCrew ownerCrew, out CrewAssignment clickedCrew, out Entity resolvedPeep)
		{
			clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
			Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
			ownerCrew = null;
			clickedCrew = CrewAssignment.EMPTY;
			resolvedPeep = null;
			Entity stateEntity = vehicle ?? clickedPeep;
			if (!MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(stateEntity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state)
				|| !state.HasInspectableOccupants)
			{
				return false;
			}
			resolvedPeep = state.InspectablePeep;
			if (resolvedPeep == null)
			{
				return false;
			}
			if (clickedPeep == null || !clickedPeep.Id.IsValid || clickedPeep.data?.agent == null)
			{
				clickedPeep = resolvedPeep;
			}
			ownerCrew = state.OwnerPid.FindPlayer()?.crew;
			if (ownerCrew == null)
			{
				return false;
			}
			EntityID resolvedPeepId = resolvedPeep.Id;
			if (!resolvedPeepId.IsValid)
			{
				return false;
			}

			object rawCrew = CrewPickCrewField?.GetValue(pickInstance);
			if (rawCrew is CrewAssignment assignment
				&& assignment.IsValid
				&& assignment.VehicleID == state.VehicleID
				&& assignment.peepId == resolvedPeepId)
			{
				clickedCrew = assignment;
			}
			if (!clickedCrew.IsValid)
			{
				clickedCrew = ownerCrew.GetCrewForPeep(resolvedPeepId);
			}
			bool deadCrewPresentation = state.Presentation == MultiCrewVehicleHelper.EnemyVehiclePresentationMode.DeadCrew;
			if (!clickedCrew.IsValid
				|| !clickedCrew.IsInVehicle
				|| clickedCrew.VehicleID != state.VehicleID
				|| (!deadCrewPresentation && !MultiCrewVehicleHelper.IsInspectableVehicleOccupant(ownerCrew, clickedCrew))
				|| (deadCrewPresentation && clickedCrew.peepId != resolvedPeepId))
			{
				return false;
			}
			return true;
		}

		private static void LogSkip(Entity clickedEntity, string reason)
		{
			if (clickedEntity == null)
			{
				return;
			}
			GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"ai-vehicle-target-skip reason={reason} clickedPeep={clickedEntity.Id.id}");
		}

		private static bool TryBlockUnmetAiCrewPick(object pickInstance, string sourceTag)
		{
			try
			{
				Entity clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
				Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
				PlayerID pid = ResolveCrewPickPlayerId(clickedPeep, vehicle);
				if (!pid.IsValid || pid.IsHumanPlayer)
				{
					return false;
				}
				PlayerInfo human = G.GetHumanPlayer();
				if (human?.meetings == null || human.meetings.IsPlayerMet(pid))
				{
					return false;
				}

				TryPruneInvalidCrewPick(pickInstance, sourceTag + "-unmet-player");
				ForceHideCrewPick(pickInstance);
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"crew-pick-blocked-unmet pid={pid.id} vehicle={vehicle?.Id.id ?? 0UL} peep={clickedPeep?.Id.id ?? 0UL} source={sourceTag}");
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static PlayerID ResolveCrewPickPlayerId(Entity clickedPeep, Entity vehicle)
		{
			try
			{
				PlayerID vehiclePid = vehicle?.data?.mobile?.pid ?? PlayerID.INVALID;
				if (vehiclePid.IsValid)
				{
					return vehiclePid;
				}
			}
			catch
			{
			}
			try
			{
				PlayerID peepPid = clickedPeep?.data?.agent?.pid ?? PlayerID.INVALID;
				if (peepPid.IsValid)
				{
					return peepPid;
				}
			}
			catch
			{
			}
			return PlayerID.INVALID;
		}

		private static bool TryNormalizeCrewPickUiState(object pickInstance, string sourceTag)
		{
			if (pickInstance == null)
			{
				return false;
			}

			Entity clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
			Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
			Entity stateEntity = vehicle ?? clickedPeep;
			if (!MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(stateEntity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
			{
				return false;
			}

			Entity resolvedVehicle = state.VehicleID.IsValid ? state.VehicleID.FindEntity() : vehicle;
			Entity resolvedPeep = state.InspectablePeep;
			PlayerCrew ownerCrew = state.OwnerPid.FindPlayer()?.crew;
			bool changed = false;

			if (!state.HasInspectableOccupants)
			{
				TryPruneInvalidCrewPick(pickInstance, sourceTag + "-empty");
				return false;
			}

			if (resolvedVehicle != null && resolvedVehicle != vehicle && CrewPickVehicleField != null)
			{
				CrewPickVehicleField.SetValue(pickInstance, resolvedVehicle);
				changed = true;
			}

			bool currentPeepUsable = clickedPeep != null
				&& clickedPeep.Id.IsValid
				&& clickedPeep.data?.agent != null
				&& (state.Presentation != MultiCrewVehicleHelper.EnemyVehiclePresentationMode.DeadCrew || clickedPeep.data?.person != null);
			if (resolvedPeep != null && (!currentPeepUsable || clickedPeep?.Id != resolvedPeep.Id) && CrewPickPeepField != null)
			{
				CrewPickPeepField.SetValue(pickInstance, resolvedPeep);
				changed = true;
			}

			if (ownerCrew != null && resolvedPeep != null && CrewPickCrewField != null)
			{
				CrewAssignment assignment = ownerCrew.GetCrewForPeep(resolvedPeep.Id);
				if (assignment.IsValid)
				{
					object rawCrew = CrewPickCrewField.GetValue(pickInstance);
					if (!(rawCrew is CrewAssignment currentAssignment)
						|| !currentAssignment.IsValid
						|| currentAssignment.peepId != assignment.peepId
						|| currentAssignment.VehicleID != assignment.VehicleID)
					{
						CrewPickCrewField.SetValue(pickInstance, assignment);
						changed = true;
					}
				}
			}

			if (changed)
			{
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"crew-pick-ui-normalized vehicle={state.VehicleID.id} peep={resolvedPeep?.Id.id ?? 0UL} source={sourceTag} presentation={state.Presentation}");
			}

			return changed;
		}

		private static bool TryPruneInvalidCrewPick(object pickInstance, string sourceTag)
		{
			Entity clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
			Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
			EntityID peepId = clickedPeep?.Id ?? EntityID.INVALID;
			EntityID vehicleId = vehicle?.Id ?? EntityID.INVALID;
			List<EntityID> targetIds = new List<EntityID>();
			if (peepId.IsValid)
			{
				targetIds.Add(peepId);
			}
			if (vehicleId.IsValid && vehicleId != peepId)
			{
				targetIds.Add(vehicleId);
			}

			try
			{
				if (pickInstance is BasePick basePick)
				{
					EntityID targetId = basePick.Target.eid;
					if (targetId.IsValid && !targetIds.Contains(targetId))
					{
						targetIds.Add(targetId);
					}
				}
			}
			catch
			{
			}

			int removedCount = GameplayTweaksPlugin.ClearCrewPicksForTargets("crew-pick-prune:" + sourceTag, targetIds.ToArray());
			if (removedCount > 0)
			{
				string targetSummary = string.Join(",", targetIds.Where(id => id.IsValid).Select(id => id.id.ToString()).Distinct());
				GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"crew-pick-pruned peep={peepId.id} vehicle={vehicleId.id} targets=[{targetSummary}] source={sourceTag}");
				return true;
			}

			return false;
		}

		private static bool TryBuildFallbackSceneVector(object pickInstance, out Vector3 sceneVector, out string reason)
		{
			sceneVector = Vector3.zero;
			reason = "none";

			Entity clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
			Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
			Entity stateEntity = vehicle ?? clickedPeep;
			if (MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(stateEntity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
			{
				Entity resolvedVehicle = state.VehicleID.IsValid ? state.VehicleID.FindEntity() : vehicle;
				if (TryResolveSceneVectorFromEntity(resolvedVehicle, out sceneVector))
				{
					reason = "vehicle";
					return true;
				}
				if (TryResolveSceneVectorFromEntity(state.InspectablePeep, out sceneVector))
				{
					reason = "inspectable-peep";
					return true;
				}
			}

			if (TryResolveSceneVectorFromEntity(vehicle, out sceneVector))
			{
				reason = "raw-vehicle";
				return true;
			}
			if (TryResolveSceneVectorFromEntity(clickedPeep, out sceneVector))
			{
				reason = "raw-peep";
				return true;
			}

			reason = "unresolved";
			return false;
		}

		private static bool TryResolveSceneVectorFromEntity(Entity entity, out Vector3 sceneVector)
		{
			sceneVector = Vector3.zero;
			if (entity == null || !entity.Id.IsValid)
			{
				return false;
			}

			WorldPos worldPos = entity.data?.board?.worldpos ?? entity.data?.mobile?.worldpos ?? WorldPos.Zero;
			if (worldPos.IsZero && MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(entity.Id, out NodeID nodeId, out _))
			{
				worldPos = nodeId.FindNode()?.pos ?? WorldPos.Zero;
			}
			if (worldPos.IsZero)
			{
				return false;
			}

			sceneVector = worldPos.AsVector3XZ;
			return true;
		}

		private static bool TryBuildFallbackMouseover(object pickInstance, out string text, out string reason)
		{
			text = string.Empty;
			reason = "none";

			Entity clickedPeep = CrewPickPeepField?.GetValue(pickInstance) as Entity;
			Entity vehicle = CrewPickVehicleField?.GetValue(pickInstance) as Entity;
			Entity stateEntity = vehicle ?? clickedPeep;
			if (MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(stateEntity, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
			{
				if (MultiCrewVehicleHelper.TryGetEnemyVehicleCrewIndicator(stateEntity, out string indicator, out _))
				{
					text = indicator;
					reason = "indicator";
					return true;
				}

				Entity inspectablePeep = state.InspectablePeep;
				if (inspectablePeep != null)
				{
					text = inspectablePeep.ToString();
					reason = "inspectable-peep";
					return true;
				}

				Entity resolvedVehicle = state.VehicleID.IsValid ? state.VehicleID.FindEntity() : vehicle;
				if (resolvedVehicle != null)
				{
					text = resolvedVehicle.ToString();
					reason = "vehicle";
					return true;
				}
			}

			if (TryPruneInvalidCrewPick(pickInstance, "mouseover-finalizer"))
			{
				text = string.Empty;
				reason = "pruned";
				return true;
			}

			return false;
		}
	}

	internal static class VehicleSearchBlockPatch
	{
		[HarmonyPrefix]
		internal static bool CheckNpcHasLootPrefix(VisitState visit, ref bool __result)
		{
			MultiCrewVehicleHelper.PushPendingPresenceSelectionScope();
			return TryBlockVehicleSearch(visit, ref __result);
		}

		[HarmonyPrefix]
		internal static bool CheckHasCashInVehiclePrefix(VisitState visit, ref bool __result)
		{
			MultiCrewVehicleHelper.PushPendingPresenceSelectionScope();
			return TryBlockVehicleSearch(visit, ref __result);
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(Exception __exception)
		{
			MultiCrewVehicleHelper.PopPendingPresenceSelectionScope();
			return __exception;
		}

		[HarmonyPostfix]
		internal static void LootDropMakeReplacementsPostfix(VisitState visit, int index, ref string[] __result)
		{
			try
			{
				if (__result == null || __result.Length == 0 || visit == null)
				{
					return;
				}
				Entity deadPeep = visit.npc ?? visit.peep;
				if (deadPeep == null || !GameplayTweaksPlugin.TryGetDeathSourceLabel(deadPeep.Id, out string label))
				{
					return;
				}
				for (int i = 0; i < __result.Length; i++)
				{
					if (!string.IsNullOrWhiteSpace(__result[i]) && __result[i].IndexOf("unknown", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						__result[i] = __result[i].Replace("unknown", label).Replace("Unknown", label);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleSearchBlockPatch.LootDropMakeReplacements: " + ex.Message);
			}
		}

		private static bool TryBlockVehicleSearch(VisitState visit, ref bool __result)
		{
			try
			{
				if (!TryResolveVisitVehicleState(visit, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state))
				{
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"vehicle-search-pass reason=no-valid-vehicle-target visitVehicle={visit?.vehicle?.Id.id ?? 0UL} visitPeep={(visit?.npc ?? visit?.peep)?.Id.id ?? 0UL}");
					return true;
				}
				EntityID vehicleId = state.VehicleID;
				if (!MultiCrewVehicleHelper.ShouldBlockVehicleSearch(vehicleId, out int survivorCount))
				{
					if (!state.HasLiveOccupants)
					{
						PlayerCrew ownerCrew = state.OwnerPid.FindPlayer()?.crew;
						if (ownerCrew != null
							&& MultiCrewVehicleHelper.TryCleanupEmptyEnemyVehiclePresentation(ownerCrew, vehicleId, "vehicle-search", out bool rewardAvailable)
							&& !rewardAvailable)
						{
							GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"vehicle-search-empty-cleanup vehicle={vehicleId.id} survivors={survivorCount} presentation={state.Presentation} reason=no-loot");
							__result = false;
							return false;
						}
					}
					GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"vehicle-search-pass vehicle={vehicleId.id} survivors={survivorCount} presentation={state.Presentation} reason=search-allowed");
					return true;
				}
				Entity deadPeep = visit?.npc ?? visit?.peep;
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"vehicle-search-blocked vehicle={vehicleId.id} deadPeep={deadPeep?.Id.id ?? 0UL} survivors={survivorCount} reason=non-empty");
				__result = false;
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VehicleSearchBlockPatch: " + ex.Message);
				return true;
			}
		}

		private static bool TryResolveVisitVehicleState(VisitState visit, out MultiCrewVehicleHelper.EnemyVehicleDisplayState state)
		{
			state = null;
			if (visit?.vehicle != null && MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(visit.vehicle, out state))
			{
				return true;
			}
			Entity entity = visit?.npc ?? visit?.peep;
			return MultiCrewVehicleHelper.TryGetEnemyVehicleDisplayState(entity, out state);
		}
	}

	internal static class EnemyVehicleCrewIndicatorPatch
	{
		[HarmonyPostfix]
		internal static void MobileMouseoverPostfix(Entity __0, ref string __result)
		{
			try
			{
				if (!MultiCrewVehicleHelper.TryGetEnemyVehicleCrewIndicator(__0, out string indicator, out _))
				{
					return;
				}
				if (string.IsNullOrWhiteSpace(__result))
				{
					__result = indicator;
					return;
				}
				if (__result.IndexOf(indicator, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return;
				}
				__result = __result.TrimEnd() + Environment.NewLine + indicator;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewIndicatorPatch.MobileMouseover: " + ex.Message);
			}
		}
	}

	internal static class EnemyVehicleCrewInteractPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(ref Entity crewPeep)
		{
			try
			{
				if (crewPeep == null || !crewPeep.Id.IsValid)
				{
					return true;
				}
				if (TryHandleHumanCrewInteract(crewPeep))
				{
					return false;
				}
				if (!MultiCrewVehicleHelper.TryResolveEnemyVehicleInspectTarget(crewPeep, out Entity resolvedPeep, out EntityID vehicleId, out _, out string reason))
				{
					return true;
				}
				if (resolvedPeep == null)
				{
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"inspect-clear-suppressed vehicle={vehicleId.id} peep={crewPeep.Id.id} reason={reason}");
					return false;
				}
				if (resolvedPeep.Id != crewPeep.Id)
				{
					PlayerID ownerPid = crewPeep.data?.agent?.pid ?? PlayerID.INVALID;
					PlayerCrew ownerCrew = ownerPid.FindPlayer()?.crew;
					if (ownerCrew != null && vehicleId.IsValid)
					{
						MultiCrewVehicleHelper.RefreshEnemyVehicleRepresentative(ownerCrew, vehicleId, "hostile-inspect");
					}
					GameplayTweaksPlugin.VerificationLog("HostileMobileSelect", $"inspect-vehicle-normalized vehicle={vehicleId.id} from={crewPeep.Id.id} to={resolvedPeep.Id.id} reason={reason}");
					crewPeep = resolvedPeep;
				}
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] EnemyVehicleCrewInteractPatch: " + ex.Message);
				return true;
			}
		}

		private static bool TryHandleHumanCrewInteract(Entity targetPeep)
		{
			if (targetPeep?.data?.agent == null || !targetPeep.data.agent.pid.IsHumanPlayer)
			{
				return false;
			}

			PlayerInfo humanPlayer = G.GetHumanPlayer();
			PlayerCrew humanCrew = humanPlayer?.crew;
			if (humanPlayer?.schemes?.GetSchemeForCrew(targetPeep) != null || humanCrew == null)
			{
				return false;
			}

			CrewAssignment targetCrew = humanCrew.GetCrewForPeep(targetPeep.Id);
			if (!targetCrew.IsValid)
			{
				return false;
			}

			if (!TryResolveHumanCrewConversationNode(targetCrew, targetPeep, out NodeID targetNodeId, out string nodeSource)
				|| !targetNodeId.IsValid)
			{
				return false;
			}

			if (!TryResolveHumanCrewConversationVisitor(humanCrew, targetCrew, targetNodeId, out CrewAssignment visitorCrew, out string visitorSource))
			{
				return false;
			}

			if (!MultiCrewVehicleHelper.TryStartCrewVisitWithPendingSelectionScope(targetPeep, visitorCrew))
			{
				return false;
			}

			GameplayTweaksPlugin.VerificationLog(
				"VehicleNodeAuthority",
				$"crew-convo-visitor-normalized target={targetPeep.Id.id} visitor={visitorCrew.peepId.id} node={targetNodeId} nodeSource={nodeSource} visitorSource={visitorSource}");
			return true;
		}

		private static bool TryResolveHumanCrewConversationNode(CrewAssignment targetCrew, Entity targetPeep, out NodeID targetNodeId, out string source)
		{
			targetNodeId = NodeID.INVALID;
			source = "none";
			if (targetCrew.IsInVehicle && targetCrew.VehicleID.IsValid)
			{
				if (MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNodeId(targetCrew.VehicleID, out targetNodeId, out source, "crew-convo")
					&& targetNodeId.IsValid)
				{
					return true;
				}
				if (MultiCrewVehicleHelper.TryGetVehicleLiveAuthorityNodeId(targetCrew.VehicleID, out targetNodeId, out source)
					&& targetNodeId.IsValid)
				{
					return true;
				}
			}

			targetNodeId = targetPeep?.data?.agent?.nid ?? NodeID.INVALID;
			source = targetNodeId.IsValid ? "peep-agent" : "none";
			return targetNodeId.IsValid;
		}

		private static bool TryResolveHumanCrewConversationVisitor(PlayerCrew humanCrew, CrewAssignment targetCrew, NodeID targetNodeId, out CrewAssignment visitorCrew, out string source)
		{
			visitorCrew = CrewAssignment.EMPTY;
			source = "none";
			if (humanCrew == null || !targetNodeId.IsValid)
			{
				return false;
			}

			List<CrewAssignment> liveMatches = MultiCrewVehicleHelper.GetHumanCrewPresentAtNodeLive(humanCrew, targetNodeId)
				.Where(match => match.IsValid && match.peepId.IsValid && match.peepId != targetCrew.peepId)
				.ToList();
			CrewAssignment bossCrew = humanCrew.GetCrewForPlayerPeep();
			CrewAssignment bossMatch = liveMatches.FirstOrDefault(match => bossCrew.IsValid && match.peepId == bossCrew.peepId);
			if (bossMatch.IsValid)
			{
				visitorCrew = bossMatch;
				source = "boss-live";
				return true;
			}

			if (targetCrew.IsInVehicle && targetCrew.VehicleID.IsValid)
			{
				EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, targetCrew.VehicleID);
				CrewAssignment driverMatch = liveMatches.FirstOrDefault(match => driverPeepId.IsValid && match.peepId == driverPeepId);
				if (driverMatch.IsValid)
				{
					visitorCrew = driverMatch;
					source = "same-vehicle-driver";
					return true;
				}
			}

			visitorCrew = liveMatches.FirstOrDefault();
			if (visitorCrew.IsValid)
			{
				source = "same-node-live";
				return true;
			}

			if (targetCrew.IsInVehicle
				&& targetCrew.VehicleID.IsValid
				&& MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(targetCrew.VehicleID, targetNodeId))
			{
				visitorCrew = targetCrew;
				source = "target-self-physical";
				return true;
			}

			return false;
		}
	}

	internal static class AmbientTrafficCanSpawnPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(TransitManager __instance, ref bool __result)
		{
			try
			{
				if (!global::Game.Game.serv.saveload.prefs.game.trafficEnabled)
				{
					__result = false;
					MultiCrewVehicleHelper.LogVehicleAuthority("ambient-can-spawn", "traffic-disabled", "ambient-spawn-skip reason=traffic-disabled");
					return false;
				}
				int count = MultiCrewVehicleHelper.CountLiveAmbientTrafficVehicles();
				int visibleFloor = MultiCrewVehicleHelper.GetAmbientVisibleTrafficFloor();
				bool needsFloorSpawn = count < visibleFloor;
				MultiCrewVehicleHelper.SetPendingAmbientSpawnReason(needsFloorSpawn ? "floor" : "normal");
				if (!needsFloorSpawn && GameplayTweaksPlugin.SharedRng.NextDouble() > 0.25)
				{
					__result = false;
					MultiCrewVehicleHelper.LogVehicleAuthority("ambient-can-spawn", "rng", "ambient-spawn-skip reason=rng");
					return false;
				}

				int hardCap = MultiCrewVehicleHelper.GetAmbientVisibleTrafficHardCap();
				if (count >= hardCap)
				{
					__result = false;
					MultiCrewVehicleHelper.LogVehicleAuthority("ambient-can-spawn", $"hardcap:{count}:{hardCap}", $"ambient-spawn-skip reason=hard-cap count={count} cap={hardCap}");
					return false;
				}

				double territoryCap = MultiCrewVehicleHelper.GetAmbientTerritoryCap();
				if ((double)count >= territoryCap)
				{
					__result = false;
					MultiCrewVehicleHelper.LogVehicleAuthority("ambient-can-spawn", $"territory:{count}:{territoryCap:0}", $"ambient-spawn-skip reason=territory-cap count={count} cap={territoryCap:0}");
					return false;
				}

				MultiCrewVehicleHelper.LogVehicleAuthority("ambient-policy", $"{count}:{visibleFloor}:{territoryCap:0}:{hardCap}:{needsFloorSpawn}", $"ambient-spawn-policy count={count} floor={visibleFloor} territoryCap={territoryCap:0} hardCap={hardCap} floorForced={needsFloorSpawn}", dedupe: true);
				__result = true;
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] AmbientTrafficCanSpawnPatch: " + ex.Message);
				return true;
			}
		}
	}

		internal static class AmbientTrafficSpawnPatch
		{
		[HarmonyPrefix]
		internal static bool Prefix(TransitManager __instance, Node start, Node mid, Node end, ref Entity __result)
		{
			try
			{
				Node routeStart = start;
				Node routeMid = mid;
				Node routeEnd = end;
				bool usingCitywideRoute = MultiCrewVehicleHelper.TryChooseAmbientTrafficRouteNodes(out Node citywideStart, out Node citywideMid, out Node citywideEnd);
				if (usingCitywideRoute)
				{
					routeStart = citywideStart;
					routeMid = citywideMid;
					routeEnd = citywideEnd;
				}

				if (routeStart == null || routeMid == null || routeEnd == null)
				{
					return true;
				}

				PathData firstPath = BuildRoadPath(PlayerID.System, routeStart.pos, routeMid.pos);
				PathData secondPath = BuildRoadPath(PlayerID.System, routeMid.pos, routeEnd.pos);
				bool hasFirstSegment = firstPath != null && firstPath.nodes.Count > 1;
				bool hasSecondSegment = secondPath != null && secondPath.nodes.Count > 1;
				if (!hasFirstSegment && usingCitywideRoute && start != null && mid != null && end != null)
				{
					routeStart = start;
					routeMid = mid;
					routeEnd = end;
					firstPath = BuildRoadPath(PlayerID.System, routeStart.pos, routeMid.pos);
					secondPath = BuildRoadPath(PlayerID.System, routeMid.pos, routeEnd.pos);
					hasFirstSegment = firstPath != null && firstPath.nodes.Count > 1;
					hasSecondSegment = secondPath != null && secondPath.nodes.Count > 1;
					usingCitywideRoute = false;
				}
				if (!hasFirstSegment)
				{
					MultiCrewVehicleHelper.LogVehicleAuthority("ambient-spawn-skip", $"{routeStart.id}:{routeMid.id}:{routeEnd.id}:no-first-path", $"ambient-spawn-skip reason=no-first-path startNode={routeStart.id} midNode={routeMid.id} endNode={routeEnd.id}", dedupe: false);
					__result = null;
					return false;
				}

				Label template = MultiCrewVehicleHelper.ChooseAmbientVehicleTemplate(__instance);
				Entity entity = __instance.SpawnCarPossiblyHidden(PlayerID.System, template, routeStart.pos, forceReveal: true);
				if (entity == null)
				{
					return false;
				}

				MultiCrewVehicleHelper.MarkAmbientTrafficVehicle(entity, template);
				List<GameAction> actions = new List<GameAction>();
				string spawnReason = MultiCrewVehicleHelper.ConsumePendingAmbientSpawnReason();
				if (hasFirstSegment)
				{
					actions.Add(new ActionPause(MultiCrewVehicleHelper.GetAmbientPauseDelay(template, betweenSegments: false)));
					actions.Add(new ActionNavigate(PlayerID.System, firstPath.world));
				}
				if (hasSecondSegment)
				{
					actions.Add(new ActionPause(MultiCrewVehicleHelper.GetAmbientPauseDelay(template, betweenSegments: true)));
					actions.Add(new ActionNavigate(PlayerID.System, secondPath.world));
				}
				actions.Add(new ActionPause(0.75f));
				actions.Add(new ActionDestroySelf());
				if (actions.Count <= 2)
				{
					MultiCrewVehicleHelper.UnmarkAmbientTrafficVehicle(entity.Id);
					__instance.DespawnCar(entity.Id, false);
					__result = null;
					MultiCrewVehicleHelper.LogVehicleAuthority("ambient-spawn-skip", $"{entity.Id.id}:no-actions", $"ambient-spawn-skip reason=no-usable-actions vehicle={entity.Id.id} startNode={routeStart.id} midNode={routeMid.id} endNode={routeEnd.id}", dedupe: false);
					return false;
				}
				entity.components.script.queue.Run(new GameScript(GameScriptType.CarNavigation, actions));
				__result = entity;
				GameplayTweaksPlugin.VerificationLog("AmbientTraffic", $"spawn vehicle={entity.Id.id} template={template} count={MultiCrewVehicleHelper.CountLiveAmbientTrafficVehicles()} floor={MultiCrewVehicleHelper.GetAmbientVisibleTrafficFloor()} territoryCap={MultiCrewVehicleHelper.GetAmbientTerritoryCap():0} reason={spawnReason} routeSource={(usingCitywideRoute ? "citywide" : "vanilla")} startNode={routeStart.id} midNode={routeMid.id} endNode={routeEnd.id} firstSegment={hasFirstSegment} secondSegment={hasSecondSegment}");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] AmbientTrafficSpawnPatch: " + ex.Message);
				return true;
			}
		}

		private static PathData BuildRoadPath(PlayerID pid, WorldPos source, WorldPos target)
		{
			PathData path = new PathData();
			RoadPathContext ctx = new RoadPathContext(global::Game.Game.ctx.board);
			global::Game.Game.ctx.board.GetGridPath(pid, EntityID.INVALID, source, target, ctx, delegate(Pathfinding.Result result)
			{
				result.PopulatePath(path, source, SomaSim.Util.Fixnum.MAX_VALUE);
			});
			return path;
		}
	}

	internal static class AmbientTrafficDespawnPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(EntityID car)
		{
			MultiCrewVehicleHelper.UnmarkAmbientTrafficVehicle(car);
		}
	}

	internal static class AmbientTrafficActivationPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(MobileComponent __instance)
		{
			try
			{
				Entity entity = Traverse.Create(__instance).Field("_entity").GetValue<Entity>();
				if (entity == null || !MultiCrewVehicleHelper.IsAmbientTrafficVehicle(entity.Id) || !__instance.IsActivated)
				{
					return true;
				}

				global::Game.Game.serv.input.Replace(new DefaultInputMode());
				global::Game.Game.ctx.selection.ClearActive();
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] AmbientTrafficActivationPatch: " + ex.Message);
				return true;
			}
		}
	}

	/// <summary>After ButtonStatus.Update: only allow "Add to vehicle" when at least one vehicle is at an owned building.</summary>
	internal static class CrewMgmtButtonStatusPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(object __instance, object e)
		{
			try
			{
				if (__instance == null)
					return;
				PlayerCrew crew = G.GetHumanCrew();
				if (crew == null)
					return;
				bool hasAtOwned = MultiCrewVehicleHelper.GetVehiclesWithSpaceAtOwnedBuilding(crew).Any();
				var field = __instance.GetType().GetField("canAddToVehicle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				bool canAddToVehicle = field != null && (bool)field.GetValue(__instance) && hasAtOwned;
				if (canAddToVehicle && e != null)
				{
					FieldInfo crewField = e.GetType().GetField("crew", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
					if (crewField != null)
					{
						CrewAssignment selectedCrew = (CrewAssignment)crewField.GetValue(e);
						if (selectedCrew.IsValid && selectedCrew.peepId.IsValid && MultiCrewVehicleHelper.IsCrewBlockedFromVehicleAssignment(selectedCrew.peepId, out _))
						{
							canAddToVehicle = false;
						}
					}
				}
				if (field != null)
					field.SetValue(__instance, canAddToVehicle);

				var buildingField = __instance.GetType().GetField("canAddToBuilding", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (buildingField != null)
				{
					bool canAddToBuilding = (bool)buildingField.GetValue(__instance);
					if (canAddToBuilding && e != null)
					{
						FieldInfo crewField = e.GetType().GetField("crew", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						if (crewField != null)
						{
							CrewAssignment selectedCrew = (CrewAssignment)crewField.GetValue(e);
							if (selectedCrew.IsValid && selectedCrew.peepId.IsValid && MultiCrewVehicleHelper.IsCrewBlockedFromVehicleAssignment(selectedCrew.peepId, out _))
							{
								canAddToBuilding = false;
							}
						}
					}
					buildingField.SetValue(__instance, canAddToBuilding);
				}
			}
			catch (Exception)
			{
				// Don't let UI throw
			}
		}
	}

	internal static class CrewMgmtPopupJailStatePatch
	{
		private static readonly BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

		private static bool TryGetCrewAssignment(object entry, out CrewAssignment crew)
		{
			crew = CrewAssignment.EMPTY;
			if (entry == null)
				return false;
			FieldInfo crewField = entry.GetType().GetField("crew", InstanceFlags);
			if (crewField == null)
				return false;
			object rawCrew = crewField.GetValue(entry);
			if (rawCrew == null)
				return false;
			crew = (CrewAssignment)rawCrew;
			return crew.IsValid && crew.peepId.IsValid;
		}

		private static bool TryGetJailedCrewStatus(object entry, out string jailStatus)
		{
			jailStatus = null;
			if (!TryGetCrewAssignment(entry, out CrewAssignment crew))
				return false;
			if (!GameplayTweaksPlugin.IsCrewCurrentlyJailed(crew.peepId))
				return false;
			jailStatus = GameplayTweaksPlugin.JailSystem.GetJailStatusString(crew.peepId);
			if (string.IsNullOrWhiteSpace(jailStatus))
				jailStatus = "Arrested";
			return true;
		}

		private static void SetCardText(GameObject card, string childPath, string value)
		{
			if (card == null || string.IsNullOrWhiteSpace(value))
				return;
			Transform target = card.transform.Find(childPath);
			if (target == null)
				return;
			TMP_Text tmp = target.GetComponent<TMP_Text>();
			if (tmp != null)
			{
				tmp.text = value;
				return;
			}
			Text text = target.GetComponent<Text>();
			if (text != null)
				text.text = value;
		}

		private static void SetCardButtonActive(GameObject card, string childPath, bool active)
		{
			if (card == null)
				return;
			Transform target = card.transform.Find(childPath);
			if (target != null)
				target.gameObject.SetActive(active);
		}

		[HarmonyPostfix]
		internal static void IsArrestedPostfix(object e, ref bool __result)
		{
			if (!__result && TryGetJailedCrewStatus(e, out _))
				__result = true;
		}

		[HarmonyPostfix]
		internal static void IsUnavailablePostfix(object e, ref bool __result)
		{
			if (!__result && TryGetJailedCrewStatus(e, out _))
				__result = true;
		}

		[HarmonyPostfix]
		internal static void IsPeepUnassignedPostfix(object __instance, ref bool __result)
		{
			if (__result && TryGetJailedCrewStatus(__instance, out _))
				__result = false;
		}

		[HarmonyPostfix]
		internal static void SetCardDescPostfix(GameObject __0, object __1)
		{
			if (!TryGetJailedCrewStatus(__1, out string jailStatus))
				return;
			SetCardText(__0, "Description", jailStatus);
		}

		[HarmonyPostfix]
		internal static void SetCardButtonsPostfix(GameObject __0, object __1)
		{
			if (!TryGetJailedCrewStatus(__1, out _))
				return;
			SetCardButtonActive(__0, "Vehicle Button", false);
			SetCardButtonActive(__0, "Building Button", false);
			SetCardButtonActive(__0, "Vehicle Remove", false);
			SetCardButtonActive(__0, "Building Remove", false);
		}
	}

	/// <summary>When assigning crew to vehicle, only show vehicles at owned buildings; show message if none.</summary>
	internal static class AssignPeepToSomeVehiclePrefixPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(CrewManagementPopup __instance, EntityID peepId)
		{
			try
			{
				PlayerCrew crew = G.GetHumanCrew();
				if (crew == null)
					return true;
				if (MultiCrewVehicleHelper.IsCrewBlockedFromVehicleAssignment(peepId, out string custodyReason))
				{
					MultiCrewVehicleHelper.ShowHudMessage(custodyReason);
					return false;
				}
				var filtered = MultiCrewVehicleHelper.GetVehiclesWithSpaceAtOwnedBuilding(crew).ToList();
				if (filtered.Count == 0)
				{
					MultiCrewVehicleHelper.ShowHudMessage("Park at your building to add crew.");
					return false;
				}
				string message = Loc.Get("ui.crewmgmt.select-vehicle");
				var deselectMethod = __instance.GetType().GetMethod("DeselectAndRefresh", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(EntityID) }, null);
				Action<EntityID> onSelect = eid =>
				{
					crew.AssignCrewToVehicle(peepId, eid);
					deselectMethod?.Invoke(__instance, new object[] { peepId });
				};
				EntitySelectionPopup.ShowSelectorWithPassInDescriptor(filtered, message, DescribeVehicleWithOccupancy(crew), onSelect);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AssignPeepToSomeVehicle prefix failed: {ex.Message}");
				return true;
			}
		}

		private static Func<Entity, EntitySelectionPopup.EntityDescription> DescribeVehicleWithOccupancy(PlayerCrew crew)
		{
			return vehicle =>
			{
				EntitySelectionPopup.EntityDescription description = new EntitySelectionPopup.EntityDescription
				{
					message = vehicle != null ? Loc.Get(vehicle.config.mobile.locname) : "Vehicle",
					sprite = vehicle != null ? HUDUtil.GetVehicleSprite(vehicle) : null
				};
				if (vehicle?.Id.IsValid == true && crew != null)
				{
					int slots = MultiCrewVehicleHelper.GetVehicleCrewSlots(vehicle.Id);
					int currentCount = MultiCrewVehicleHelper.GetVehicleCrewCount(crew, vehicle.Id);
					description.description = slots > 0
						? $"Crew {currentCount}/{slots}"
						: $"Crew {currentCount}";
				}
				return description;
			};
		}
	}

	internal static class AssignPeepToSomeBuildingPrefixPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(CrewManagementPopup __instance, EntityID peepId)
		{
			try
			{
				if (MultiCrewVehicleHelper.IsCrewBlockedFromVehicleAssignment(peepId, out string custodyReason))
				{
					MultiCrewVehicleHelper.ShowHudMessage(custodyReason);
					return false;
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[GameplayTweaks] AssignPeepToSomeBuilding prefix failed: {ex.Message}");
				return true;
			}
		}
	}

	internal static class OwnedBuildingInteractionSelectionPatch
	{
		private const float OwnedBuildingCornerFallbackDistance = 18f;
		private const float OwnedBuildingLoosePreviewDistance = 42f;
		private static readonly MethodInfo BizStartConversationForCrewMethod = typeof(BizComponent).GetMethod("StartConversation", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(CrewAssignment), typeof(Entity), typeof(bool) }, null);

		private static Entity NormalizeOwnedInteractionBuilding(Entity entity)
		{
			if (entity == null)
			{
				return null;
			}

			try
			{
				Entity resolvedBuilding = BuildingUtil.FindBuildingForBiz(entity);
				if (resolvedBuilding != null)
				{
					return resolvedBuilding;
				}
			}
			catch
			{
			}

			if (IsOwnedInteractionBuilding(entity))
			{
				return entity;
			}

			return entity;
		}

		internal static bool IsOwnedInteractionBuilding(Entity building)
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (building == null || humanPlayer?.territory == null || !building.Id.IsValid)
			{
				return false;
			}
			if (humanPlayer.territory.Safehouse == building.Id)
			{
				return true;
			}

			try
			{
				if (building.components?.building == null)
				{
					return false;
				}
				return humanPlayer.territory.IsControlled(building);
			}
			catch (Exception ex)
			{
				GameplayTweaksPlugin.VerificationLog("VehicleNodeAuthority", $"owned-building-check-skip entity={building.Id.id} reason={ex.GetType().Name}");
				return false;
			}
		}

		internal static bool IsHumanSafehouseInteractionBuilding(Entity building)
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (building == null || humanPlayer?.territory == null || humanPlayer.territory.Safehouse.IsNotValid)
			{
				return false;
			}

			if (humanPlayer.territory.Safehouse == building.Id)
			{
				return true;
			}

			try
			{
				Entity resolvedBuilding = BuildingUtil.FindBuildingForBiz(building);
				return resolvedBuilding != null && humanPlayer.territory.Safehouse == resolvedBuilding.Id;
			}
			catch
			{
				return false;
			}
		}

		private static bool TryGetCurrentSelectedHumanVehicle(PlayerCrew humanCrew, out EntityID vehicleId, out string sourceTag)
		{
			vehicleId = EntityID.INVALID;
			sourceTag = "none";
			if (humanCrew == null || !humanCrew.PID.IsHumanPlayer)
			{
				return false;
			}

			Entity currentActive = global::Game.Game.ctx?.selection?.CurrentActive;
			if (currentActive == null)
			{
				return false;
			}

			if (currentActive.components?.mobile != null && currentActive.data?.mobile?.pid == humanCrew.PID && currentActive.Id.IsValid)
			{
				vehicleId = currentActive.Id;
				sourceTag = "current-selection-mobile";
				return true;
			}

			CrewAssignment selectedCrew = humanCrew.GetCrewForPeep(currentActive.Id);
			if (selectedCrew.IsValid && selectedCrew.IsInVehicle && selectedCrew.VehicleID.IsValid)
			{
				vehicleId = selectedCrew.VehicleID;
				sourceTag = "current-selection-crew";
				return true;
			}

			return false;
		}

		internal static void AddOwnedBuildingFrontageComparisonNodes(Entity building, List<NodeID> comparisonNodeIds)
		{
			if (building?.data?.board == null || comparisonNodeIds == null)
			{
				return;
			}
			if (IsHumanSafehouseInteractionBuilding(building))
			{
				return;
			}

			void AddNode(NodeID nodeId)
			{
				if (nodeId.IsValid && !comparisonNodeIds.Contains(nodeId))
				{
					comparisonNodeIds.Add(nodeId);
				}
			}

			try
			{
				AddNode(building.data.board.bead.nodeId);
				NodeEdgeID edgeId = building.data.board.bead.edgeId;
				NodeEdge edge = edgeId.IsValid ? edgeId.FindEdge() : null;
				if (edge != null)
				{
					AddNode(edge.a);
					AddNode(edge.b);
				}
			}
			catch
			{
			}
		}

		private static bool AreNodesWithinOwnedInteractionCornerRange(NodeID previewNodeId, NodeID comparisonNodeId)
		{
			if (!previewNodeId.IsValid || !comparisonNodeId.IsValid)
			{
				return false;
			}

			if (previewNodeId == comparisonNodeId || MultiCrewVehicleHelper.AreNodesDirectlyAdjacent(previewNodeId, comparisonNodeId))
			{
				return true;
			}

			Node previewNode = previewNodeId.FindNode();
			Node comparisonNode = comparisonNodeId.FindNode();
			if (previewNode == null || comparisonNode == null)
			{
				return false;
			}

			if ((previewNode.pos - comparisonNode.pos).Magnitude <= OwnedBuildingCornerFallbackDistance)
			{
				return true;
			}

			if (previewNode.edges == null)
			{
				return false;
			}

			foreach (NodeEdgeID edgeId in previewNode.edges)
			{
				NodeEdge edge = edgeId.IsValid ? edgeId.FindEdge() : null;
				NodeID adjacentNodeId = edge?.GetOtherNodeID(previewNodeId) ?? NodeID.INVALID;
				if (!adjacentNodeId.IsValid)
				{
					continue;
				}

				if (adjacentNodeId == comparisonNodeId || MultiCrewVehicleHelper.AreNodesDirectlyAdjacent(adjacentNodeId, comparisonNodeId))
				{
					return true;
				}

				Node adjacentNode = adjacentNodeId.FindNode();
				if (adjacentNode != null && (adjacentNode.pos - comparisonNode.pos).Magnitude <= OwnedBuildingCornerFallbackDistance)
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsPreviewNodeWithinOwnedBuildingLooseRange(NodeID previewNodeId, WorldPos buildingWorldPos)
		{
			if (!previewNodeId.IsValid || buildingWorldPos.IsZero)
			{
				return false;
			}

			Node previewNode = previewNodeId.FindNode();
			if (previewNode != null && (previewNode.pos - buildingWorldPos).Magnitude <= OwnedBuildingLoosePreviewDistance)
			{
				return true;
			}

			if (previewNode?.edges == null)
			{
				return false;
			}

			foreach (NodeEdgeID edgeId in previewNode.edges)
			{
				NodeEdge edge = edgeId.IsValid ? edgeId.FindEdge() : null;
				NodeID adjacentNodeId = edge?.GetOtherNodeID(previewNodeId) ?? NodeID.INVALID;
				Node adjacentNode = adjacentNodeId.IsValid ? adjacentNodeId.FindNode() : null;
				if (adjacentNode != null && (adjacentNode.pos - buildingWorldPos).Magnitude <= OwnedBuildingLoosePreviewDistance)
				{
					return true;
				}
			}

			return false;
		}

		internal static bool TryMatchOwnedBuildingPreviewNode(Entity building, NodeID previewNodeId, IEnumerable<NodeID> comparisonNodeIds, out NodeID matchedNodeId)
		{
			matchedNodeId = NodeID.INVALID;
			if (building == null || !previewNodeId.IsValid)
			{
				return false;
			}

			List<NodeID> normalizedComparisonNodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (normalizedComparisonNodeIds.Count <= 0)
			{
				return false;
			}

			if (normalizedComparisonNodeIds.Contains(previewNodeId))
			{
				matchedNodeId = previewNodeId;
				return true;
			}

			WorldPos buildingWorldPos = building.data?.board?.worldpos ?? WorldPos.Zero;
			if (CommandButtonScopeOutPatch.TryMatchScopePreviewNodeToBuilding(building, previewNodeId, out _, out _, out NodeID frontageMatchedNodeId, out _)
				&& frontageMatchedNodeId.IsValid)
			{
				matchedNodeId = frontageMatchedNodeId;
				return true;
			}

			if (IsPreviewNodeWithinOwnedBuildingLooseRange(previewNodeId, buildingWorldPos))
			{
				matchedNodeId = previewNodeId;
				return true;
			}

			if (MultiCrewVehicleHelper.TryResolveScopeFrontageClusterMatch(previewNodeId, normalizedComparisonNodeIds, buildingWorldPos, out NodeID clusteredMatchNodeId, out _)
				&& clusteredMatchNodeId.IsValid)
			{
				matchedNodeId = clusteredMatchNodeId;
				return true;
			}

			foreach (NodeID comparisonNodeId in normalizedComparisonNodeIds)
			{
				if (!AreNodesWithinOwnedInteractionCornerRange(previewNodeId, comparisonNodeId))
				{
					continue;
				}

				Node previewNode = previewNodeId.FindNode();
				if (!buildingWorldPos.IsZero && previewNode != null && (previewNode.pos - buildingWorldPos).Magnitude > OwnedBuildingCornerFallbackDistance * 2f)
				{
					continue;
				}

				matchedNodeId = comparisonNodeId;
				return true;
			}

			return false;
		}

		internal static bool TryResolveOwnedBuildingPreviewVehicleMatches(PlayerCrew humanCrew, Entity building, out List<CrewAssignment> matches)
		{
			matches = new List<CrewAssignment>();
			if (humanCrew == null || building == null || !IsOwnedInteractionBuilding(building))
			{
				return false;
			}

			return TryResolveOwnedBuildingVehicleMatches(humanCrew, building, out matches, allowAnyHumanVehicle: false, requireLiveVehicleNode: false);
		}

		internal static bool TryResolveSelectedDirectedOwnedBuildingVehicleDriver(PlayerCrew humanCrew, Entity building, out CrewAssignment driverMatch, out string matchSource)
		{
			driverMatch = CrewAssignment.EMPTY;
			matchSource = "none";
			if (humanCrew == null || building == null || !IsOwnedInteractionBuilding(building))
			{
				return false;
			}

			bool requireLiveVehicleNode = true;
			EntityID selectedVehicleId = EntityID.INVALID;
			string selectedSource = "none";
			if (requireLiveVehicleNode
				&& !TryGetCurrentSelectedHumanVehicle(humanCrew, out selectedVehicleId, out selectedSource))
			{
				return false;
			}
			if (!requireLiveVehicleNode
				&& !MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out selectedVehicleId, out selectedSource))
			{
				return false;
			}
			if (!selectedVehicleId.IsValid)
			{
				return false;
			}
			if (!selectedVehicleId.IsValid)
			{
				return false;
			}
			NodeID expectedNodeId = NodeID.INVALID;
			NodeID goalNodeId = NodeID.INVALID;
			if (!requireLiveVehicleNode)
			{
				bool routeIsActiveOrQueued = MultiCrewVehicleHelper.IsHumanVehicleTravelActive(selectedVehicleId)
					|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(selectedVehicleId);
				if (!routeIsActiveOrQueued
					|| !MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(selectedVehicleId, out _, out expectedNodeId, out goalNodeId)
					|| (!expectedNodeId.IsValid && !goalNodeId.IsValid))
				{
					return false;
				}
			}

			if (!CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> comparisonNodeIds, out _)
				|| comparisonNodeIds == null)
			{
				comparisonNodeIds = new List<NodeID>();
			}

			comparisonNodeIds = comparisonNodeIds
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList();
			NodeID buildingBoardNodeId = MultiCrewVehicleHelper.TryGetEntityBoardNodeId(building, out NodeID resolvedBuildingNodeId)
				? resolvedBuildingNodeId
				: NodeID.INVALID;
			if (buildingBoardNodeId.IsValid && !comparisonNodeIds.Contains(buildingBoardNodeId))
			{
				comparisonNodeIds.Add(buildingBoardNodeId);
			}
			AddOwnedBuildingFrontageComparisonNodes(building, comparisonNodeIds);
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			if (requireLiveVehicleNode)
			{
				if (!TryGetVehicleLiveOwnedBuildingAccessNode(selectedVehicleId, building, comparisonNodeIds, out NodeID liveVehicleNodeId, out NodeID matchedLiveNodeId, out string liveSource)
					|| !TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out driverMatch))
				{
					return false;
				}

				matchSource = "live-owned-" + (string.IsNullOrWhiteSpace(selectedSource) ? liveSource : selectedSource + "-" + liveSource);
				GameplayTweaksPlugin.VerificationLog(
					"VehicleNodeAuthority",
					$"owned-building-directed-selected-live-match building={building.Id.id} vehicle={selectedVehicleId.id} node={liveVehicleNodeId} source={matchSource} matchedNode={matchedLiveNodeId} crew={driverMatch.peepId.id}");
				return true;
			}

			NodeID previewNodeId = NodeID.INVALID;
			NodeID matchedNodeId = NodeID.INVALID;
			string nodeSource = "none";
			if (goalNodeId.IsValid && TryMatchOwnedBuildingPreviewNode(building, goalNodeId, comparisonNodeIds, out matchedNodeId))
			{
				previewNodeId = goalNodeId;
				nodeSource = "pending-final-goal";
			}
			else if (expectedNodeId.IsValid && TryMatchOwnedBuildingPreviewNode(building, expectedNodeId, comparisonNodeIds, out matchedNodeId))
			{
				previewNodeId = expectedNodeId;
				nodeSource = "pending-expected";
			}

			if (!previewNodeId.IsValid || !TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out driverMatch))
			{
				return false;
			}

			matchSource = nodeSource + "-" + (string.IsNullOrWhiteSpace(selectedSource) ? "selected" : selectedSource);
			GameplayTweaksPlugin.VerificationLog(
				"VehicleNodeAuthority",
				$"owned-building-directed-selected-match building={building.Id.id} vehicle={selectedVehicleId.id} node={previewNodeId} source={matchSource} matchedNode={matchedNodeId} crew={driverMatch.peepId.id}");
			return true;
		}

		internal static bool TryResolveOwnedBuildingVehicleMatches(PlayerCrew humanCrew, Entity building, out List<CrewAssignment> matches, bool allowAnyHumanVehicle, bool requireLiveVehicleNode = false)
		{
			matches = new List<CrewAssignment>();
			if (humanCrew == null || building == null || !IsOwnedInteractionBuilding(building))
			{
				return false;
			}

			if (!CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> comparisonNodeIds, out _)
				|| comparisonNodeIds == null)
			{
				comparisonNodeIds = new List<NodeID>();
			}

			comparisonNodeIds = comparisonNodeIds
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList();
			NodeID buildingBoardNodeId = MultiCrewVehicleHelper.TryGetEntityBoardNodeId(building, out NodeID resolvedBuildingNodeId)
				? resolvedBuildingNodeId
				: NodeID.INVALID;
			if (buildingBoardNodeId.IsValid && !comparisonNodeIds.Contains(buildingBoardNodeId))
			{
				comparisonNodeIds.Add(buildingBoardNodeId);
			}
			AddOwnedBuildingFrontageComparisonNodes(building, comparisonNodeIds);
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			requireLiveVehicleNode = requireLiveVehicleNode || IsHumanSafehouseInteractionBuilding(building);
			List<KeyValuePair<EntityID, string>> candidateVehicles;
			candidateVehicles = requireLiveVehicleNode
				? GetOwnedBuildingCandidateVehicles(humanCrew, allowAnyHumanVehicle: true)
				: GetOwnedBuildingCandidateVehicles(humanCrew, allowAnyHumanVehicle);
			if (candidateVehicles.Count <= 0)
			{
				return false;
			}

			NodeID previewNodeId = NodeID.INVALID;
			NodeID matchedNodeId = NodeID.INVALID;
			string previewSource = "none";
			EntityID matchedVehicleId = EntityID.INVALID;
			List<string> missCandidates = new List<string>();
			foreach (KeyValuePair<EntityID, string> vehicleCandidate in candidateVehicles)
			{
				if (!vehicleCandidate.Key.IsValid
					|| !TryGetOwnedBuildingPreviewCandidateNodes(vehicleCandidate.Key, out List<KeyValuePair<NodeID, string>> previewCandidates, requireLiveVehicleNode)
					|| previewCandidates.Count <= 0)
				{
					continue;
				}

				foreach (KeyValuePair<NodeID, string> candidate in previewCandidates)
				{
					if (!candidate.Key.IsValid)
					{
						continue;
					}
					missCandidates.Add($"{vehicleCandidate.Key.id}:{candidate.Key}:{candidate.Value}:{vehicleCandidate.Value}");
					bool matched = requireLiveVehicleNode
						? TryGetVehicleLiveOwnedBuildingAccessNode(vehicleCandidate.Key, building, comparisonNodeIds, out NodeID liveVehicleNodeId, out matchedNodeId, out _)
							&& liveVehicleNodeId == candidate.Key
						: TryMatchOwnedBuildingPreviewNode(building, candidate.Key, comparisonNodeIds, out matchedNodeId);
					if (matched)
					{
						matchedVehicleId = vehicleCandidate.Key;
						previewNodeId = candidate.Key;
						if (!matchedNodeId.IsValid)
						{
							matchedNodeId = candidate.Key;
						}
						previewSource = candidate.Value + "-" + vehicleCandidate.Value;
						break;
					}
				}
				if (matchedVehicleId.IsValid)
				{
					break;
				}
			}
			if (!previewNodeId.IsValid)
			{
				GameplayTweaksPlugin.VerificationLog(
					"VehicleNodeAuthority",
					$"owned-building-preview-fallback-miss building={building.Id.id} candidates={string.Join(",", missCandidates)} comparisonNodes={string.Join(",", comparisonNodeIds)} allowAny={allowAnyHumanVehicle} liveOnly={requireLiveVehicleNode}");
				return false;
			}

			if (!TryGetSelectedVehicleDriverInteractionMatch(humanCrew, matchedVehicleId, out CrewAssignment driverMatch))
			{
				return false;
			}
			matches.Add(driverMatch);

			GameplayTweaksPlugin.VerificationLog(
				"VehicleNodeAuthority",
				$"owned-building-preview-fallback building={building.Id.id} vehicle={matchedVehicleId.id} node={previewNodeId} source={previewSource} matchedNode={matchedNodeId} crew={string.Join(",", matches.Select(match => match.peepId.id.ToString()))} allowAny={allowAnyHumanVehicle} liveOnly={requireLiveVehicleNode}");
			return true;
		}

		private static bool IsOwnedBuildingVehicleRouteActive(EntityID vehicleId)
		{
			return vehicleId.IsValid
				&& (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId)
					|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId)
					|| MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(vehicleId, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
						&& (expectedNodeId.IsValid || goalNodeId.IsValid));
		}

		internal static bool TryGetVehicleLiveOwnedBuildingAccessNode(EntityID vehicleId, Entity building, List<NodeID> comparisonNodeIds, out NodeID liveNodeId, out NodeID matchedNodeId, out string source)
		{
			liveNodeId = NodeID.INVALID;
			matchedNodeId = NodeID.INVALID;
			source = "none";
			if (!vehicleId.IsValid || building == null)
			{
				return false;
			}
			bool routeActive = IsOwnedBuildingVehicleRouteActive(vehicleId);
			bool safehouseBuilding = IsHumanSafehouseInteractionBuilding(building);

			List<NodeID> normalizedComparisonNodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (normalizedComparisonNodeIds.Count <= 0)
			{
				return false;
			}

			if (!TryGetOwnedBuildingPreviewCandidateNodes(vehicleId, out List<KeyValuePair<NodeID, string>> liveCandidates, liveOnly: true)
				|| liveCandidates.Count <= 0)
			{
				return false;
			}

			foreach (KeyValuePair<NodeID, string> candidate in liveCandidates)
			{
				if (!candidate.Key.IsValid)
				{
					continue;
				}

				bool exactNodeMatch = normalizedComparisonNodeIds.Contains(candidate.Key);
				NodeID candidateMatchedNodeId = candidate.Key;
				if (safehouseBuilding && !exactNodeMatch)
				{
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"safehouse-live-frontage-blocked building={building.Id.id} vehicle={vehicleId.id} node={candidate.Key} source={candidate.Value} comparisonNodes={string.Join(",", normalizedComparisonNodeIds)}");
					continue;
				}
				if (!exactNodeMatch
					&& !TryMatchOwnedBuildingPreviewNode(building, candidate.Key, normalizedComparisonNodeIds, out candidateMatchedNodeId))
				{
					continue;
				}

				bool physicallyAtCandidate = MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(vehicleId, candidate.Key);
				string accessSource = physicallyAtCandidate ? "physical" : "none";
				if (!physicallyAtCandidate)
				{
					if (routeActive)
					{
						GameplayTweaksPlugin.VerificationLog(
							"VehicleNodeAuthority",
							$"owned-building-live-route-preview-blocked building={building.Id.id} vehicle={vehicleId.id} node={candidate.Key} source={candidate.Value} comparisonNodes={string.Join(",", normalizedComparisonNodeIds)}");
						continue;
					}
					if (!MultiCrewVehicleHelper.IsHumanVehicleAtOwnedBuildingAccessNode(vehicleId, candidate.Key, out accessSource))
					{
						continue;
					}
				}
				else if (routeActive)
				{
					accessSource = "physical-route-active";
				}

				liveNodeId = candidate.Key;
				matchedNodeId = candidateMatchedNodeId.IsValid ? candidateMatchedNodeId : candidate.Key;
				string candidateSource = string.IsNullOrWhiteSpace(candidate.Value) ? accessSource : candidate.Value;
				source = exactNodeMatch ? candidateSource : candidateSource + "-frontage";
				return true;
			}

			return false;
		}

		private static List<KeyValuePair<EntityID, string>> GetOwnedBuildingCandidateVehicles(PlayerCrew humanCrew, bool allowAnyHumanVehicle)
		{
			List<KeyValuePair<EntityID, string>> vehicles = new List<KeyValuePair<EntityID, string>>();
			if (humanCrew == null)
			{
				return vehicles;
			}

			if (MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out string selectedSource)
				&& selectedVehicleId.IsValid)
			{
				AddOwnedBuildingCandidateVehicle(vehicles, selectedVehicleId, string.IsNullOrWhiteSpace(selectedSource) ? "selected" : selectedSource);
			}

			if (allowAnyHumanVehicle)
			{
				foreach (CrewAssignment assignment in humanCrew.GetLiving())
				{
					if (!assignment.IsValid
						|| !assignment.IsInVehicle
						|| !assignment.VehicleID.IsValid
						|| !assignment.peepId.IsValid
						|| !humanCrew.IsOnBoard(assignment.peepId)
						|| GameplayTweaksPlugin.IsCrewCurrentlyJailed(assignment.peepId))
					{
						continue;
					}
					AddOwnedBuildingCandidateVehicle(vehicles, assignment.VehicleID, "any-human-vehicle");
				}
			}

			return vehicles;
		}

		private static void AddOwnedBuildingCandidateVehicle(List<KeyValuePair<EntityID, string>> vehicles, EntityID vehicleId, string source)
		{
			if (!vehicleId.IsValid || vehicles.Any(candidate => candidate.Key == vehicleId))
			{
				return;
			}
			vehicles.Add(new KeyValuePair<EntityID, string>(vehicleId, string.IsNullOrWhiteSpace(source) ? "unknown" : source));
		}

		private static bool TryGetOwnedBuildingPreviewCandidateNodes(EntityID selectedVehicleId, out List<KeyValuePair<NodeID, string>> candidates, bool liveOnly = false)
		{
			candidates = new List<KeyValuePair<NodeID, string>>();
			if (!selectedVehicleId.IsValid)
			{
				return false;
			}

			if (liveOnly)
			{
				if (MultiCrewVehicleHelper.TryGetVehicleLiveAuthorityNodeId(selectedVehicleId, out NodeID liveNodeId, out string liveSource))
				{
					AddOwnedBuildingPreviewCandidate(candidates, liveNodeId, string.IsNullOrWhiteSpace(liveSource) ? "live" : "live-" + liveSource);
				}
				return candidates.Count > 0;
			}

			if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(selectedVehicleId, out _, out NodeID pendingExpectedNodeId, out NodeID pendingGoalNodeId))
			{
				AddOwnedBuildingPreviewCandidate(candidates, pendingGoalNodeId, "pending-final-goal");
				AddOwnedBuildingPreviewCandidate(candidates, pendingExpectedNodeId, "pending-expected");
			}
			if (GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(selectedVehicleId, out NodeID selectedFinalNodeId))
			{
				AddOwnedBuildingPreviewCandidate(candidates, selectedFinalNodeId, "selected-final-goal");
			}
			if (MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out NodeID previewNodeId, out string previewSource, "owned-building-preview"))
			{
				AddOwnedBuildingPreviewCandidate(candidates, previewNodeId, string.IsNullOrWhiteSpace(previewSource) ? "scope-preview" : previewSource);
			}
			if (MultiCrewVehicleHelper.TryGetFinalizedVehicleNodeId(selectedVehicleId, out NodeID finalizedNodeId, out string finalizedSource))
			{
				AddOwnedBuildingPreviewCandidate(candidates, finalizedNodeId, string.IsNullOrWhiteSpace(finalizedSource) ? "finalized" : finalizedSource);
			}
			if (MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(selectedVehicleId, out NodeID authoritativeNodeId, out string authoritativeSource))
			{
				AddOwnedBuildingPreviewCandidate(candidates, authoritativeNodeId, string.IsNullOrWhiteSpace(authoritativeSource) ? "authoritative" : authoritativeSource);
			}

			return candidates.Count > 0;
		}

		private static void AddOwnedBuildingPreviewCandidate(List<KeyValuePair<NodeID, string>> candidates, NodeID nodeId, string source)
		{
			if (!nodeId.IsValid || candidates.Any(candidate => candidate.Key == nodeId))
			{
				return;
			}
			candidates.Add(new KeyValuePair<NodeID, string>(nodeId, string.IsNullOrWhiteSpace(source) ? "unknown" : source));
		}

		internal static bool TryGetSelectedVehicleDriverInteractionMatch(PlayerCrew humanCrew, EntityID vehicleId, out CrewAssignment driverMatch)
		{
			driverMatch = CrewAssignment.EMPTY;
			if (humanCrew == null || !vehicleId.IsValid)
			{
				return false;
			}

			EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, vehicleId);
			if (!driverPeepId.IsValid)
			{
				return false;
			}

			driverMatch = humanCrew.GetCrewForPeep(driverPeepId);
			if (!driverMatch.IsValid
				|| !driverMatch.peepId.IsValid
				|| !driverMatch.IsInVehicle
				|| driverMatch.VehicleID != vehicleId
				|| !driverMatch.IsNotDead
				|| !humanCrew.IsOnBoard(driverMatch.peepId)
				|| GameplayTweaksPlugin.IsCrewCurrentlyJailed(driverMatch.peepId))
			{
				driverMatch = CrewAssignment.EMPTY;
				return false;
			}

			return true;
		}

		private static List<CrewAssignment> NormalizeInteractionMatches(List<CrewAssignment> matches)
		{
			return matches?
				.Where(match => match.IsValid && match.peepId.IsValid)
				.GroupBy(match => match.peepId.id)
				.Select(group => group.First())
				.ToList() ?? new List<CrewAssignment>();
		}

		private static bool TryCollapseMatchesToVehicleDriver(PlayerCrew humanCrew, List<CrewAssignment> matches, out List<CrewAssignment> collapsedMatches, out string collapseSource)
		{
			collapsedMatches = new List<CrewAssignment>();
			collapseSource = "none";
			if (humanCrew == null)
			{
				return false;
			}

			List<CrewAssignment> normalizedMatches = NormalizeInteractionMatches(matches);
			if (normalizedMatches.Count <= 0)
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
				&& selectedVehicleId.IsValid)
			{
				List<CrewAssignment> selectedVehicleMatches = normalizedMatches
					.Where(match => match.IsInVehicle && match.VehicleID == selectedVehicleId)
					.ToList();
				if (selectedVehicleMatches.Count > 0)
				{
					EntityID selectedDriverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, selectedVehicleId);
					CrewAssignment selectedDriverMatch = selectedVehicleMatches.FirstOrDefault(match => match.peepId == selectedDriverPeepId);
					if (selectedDriverMatch.IsValid)
					{
						collapsedMatches.Add(selectedDriverMatch);
						collapseSource = "selected-vehicle-driver";
						return true;
					}
				}
			}

			List<CrewAssignment> vehicleMatches = normalizedMatches
				.Where(match => match.IsInVehicle && match.VehicleID.IsValid)
				.ToList();
			if (vehicleMatches.Count != normalizedMatches.Count)
			{
				return false;
			}

			EntityID vehicleId = vehicleMatches[0].VehicleID;
			if (!vehicleId.IsValid || vehicleMatches.Any(match => match.VehicleID != vehicleId))
			{
				return false;
			}

			EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, vehicleId);
			CrewAssignment driverMatch = vehicleMatches.FirstOrDefault(match => match.peepId == driverPeepId);
			if (!driverMatch.IsValid)
			{
				return false;
			}

			collapsedMatches.Add(driverMatch);
			collapseSource = "single-vehicle-driver";
			return true;
		}

		private static bool TryResolveBuildingInteractionMatches(Entity building, out List<CrewAssignment> matches)
		{
			matches = new List<CrewAssignment>();
			PlayerCrew humanCrew = G.GetHumanCrew();
			building = NormalizeOwnedInteractionBuilding(building);
			if (humanCrew == null || building == null)
			{
				return false;
			}

			if (IsOwnedInteractionBuilding(building))
			{
				if (!TryResolveOwnedBuildingVehicleMatches(humanCrew, building, out List<CrewAssignment> liveOwnedMatches, allowAnyHumanVehicle: true, requireLiveVehicleNode: true)
					|| liveOwnedMatches == null
					|| liveOwnedMatches.Count <= 0)
				{
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"owned-building-physical-required building={building.Id.id} reason=no-live-vehicle-at-building");
					return false;
				}

				matches = NormalizeInteractionMatches(liveOwnedMatches);
				if (TryCollapseMatchesToVehicleDriver(humanCrew, matches, out List<CrewAssignment> collapsedLiveOwnedMatches, out string liveCollapseSource))
				{
					matches = collapsedLiveOwnedMatches;
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"owned-building-live-driver-collapse building={building.Id.id} source={liveCollapseSource} crew={string.Join(",", matches.Select(match => match.peepId.id.ToString()))}");
				}

				GameplayTweaksPlugin.VerificationLog(
					"VehicleNodeAuthority",
					$"owned-building-physical-access building={building.Id.id} crew={string.Join(",", matches.Where(match => match.IsValid && match.peepId.IsValid).Select(match => match.peepId.id.ToString()))}");
				return matches.Count > 0;
			}

			if (!CommandButtonScopeOutPatch.TryGetCommittedScopeMatchesForBuilding(humanCrew, building, out List<CrewAssignment> committedMatches, out _, out _, out _)
				|| committedMatches == null
				|| committedMatches.Count <= 0)
			{
				if (!TryResolveOwnedBuildingPreviewVehicleMatches(humanCrew, building, out matches))
				{
					return false;
				}

				matches = NormalizeInteractionMatches(matches);
				if (TryCollapseMatchesToVehicleDriver(humanCrew, matches, out List<CrewAssignment> collapsedPreviewMatches, out string previewCollapseSource))
				{
					matches = collapsedPreviewMatches;
					GameplayTweaksPlugin.VerificationLog(
						"VehicleNodeAuthority",
						$"owned-building-preview-driver-collapse building={building.Id.id} source={previewCollapseSource} crew={string.Join(",", matches.Select(match => match.peepId.id.ToString()))}");
				}
				return matches.Count > 0;
			}

			matches = NormalizeInteractionMatches(committedMatches);
			if (TryCollapseMatchesToVehicleDriver(humanCrew, matches, out List<CrewAssignment> collapsedMatches, out string collapseSource))
			{
				matches = collapsedMatches;
				GameplayTweaksPlugin.VerificationLog(
					"VehicleNodeAuthority",
					$"owned-building-driver-collapse building={building.Id.id} source={collapseSource} crew={string.Join(",", matches.Select(match => match.peepId.id.ToString()))}");
			}
			return matches.Count > 0;
		}

		internal static EntityID ResolveDefaultInteractionCrew(PlayerCrew humanCrew, List<CrewAssignment> matches)
		{
			if (humanCrew == null || matches == null || matches.Count <= 0)
			{
				return EntityID.INVALID;
			}

			if (TryCollapseMatchesToVehicleDriver(humanCrew, matches, out List<CrewAssignment> collapsedMatches, out _))
			{
				EntityID collapsedPeepId = collapsedMatches.FirstOrDefault().peepId;
				if (collapsedPeepId.IsValid)
				{
					return collapsedPeepId;
				}
			}

			CrewAssignment bossCrew = humanCrew.GetCrewForPlayerPeep();
			CrewAssignment bossMatch = matches.FirstOrDefault(match => match.IsValid && match.peepId == bossCrew.peepId);
			if (bossMatch.IsValid)
			{
				return bossMatch.peepId;
			}

			CrewAssignment vehicleMatch = matches.FirstOrDefault(match => match.IsValid && match.IsInVehicle && match.VehicleID.IsValid);
			if (vehicleMatch.IsValid)
			{
				EntityID driverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(humanCrew, vehicleMatch.VehicleID);
				CrewAssignment driverMatch = matches.FirstOrDefault(match => match.IsValid && match.peepId == driverPeepId);
				if (driverMatch.IsValid)
				{
					return driverMatch.peepId;
				}
			}

			return matches.FirstOrDefault(match => match.IsValid && match.peepId.IsValid).peepId;
		}

		internal static bool TryResolveSingleVehicleInteractionCrew(PlayerCrew humanCrew, List<CrewAssignment> matches, out EntityID peepId)
		{
			peepId = EntityID.INVALID;
			if (humanCrew == null || matches == null || matches.Count <= 0)
			{
				return false;
			}

			List<CrewAssignment> normalizedMatches = NormalizeInteractionMatches(matches);
			if (normalizedMatches.Count <= 1)
			{
				peepId = normalizedMatches.FirstOrDefault().peepId;
				return peepId.IsValid;
			}

			if (TryCollapseMatchesToVehicleDriver(humanCrew, normalizedMatches, out List<CrewAssignment> collapsedMatches, out _))
			{
				peepId = collapsedMatches.FirstOrDefault().peepId;
				return peepId.IsValid;
			}

			List<CrewAssignment> vehicleMatches = normalizedMatches
				.Where(match => match.IsInVehicle && match.VehicleID.IsValid)
				.ToList();
			if (vehicleMatches.Count != normalizedMatches.Count)
			{
				return false;
			}

			EntityID vehicleId = vehicleMatches[0].VehicleID;
			if (!vehicleId.IsValid || vehicleMatches.Any(match => match.VehicleID != vehicleId))
			{
				return false;
			}

			peepId = ResolveDefaultInteractionCrew(humanCrew, normalizedMatches);
			return peepId.IsValid;
		}

		[HarmonyPrefix]
		internal static bool StartOwnedBuildingInteractionPrefix(Entity building, Action<EntityID, Entity> posCallback)
		{
			try
			{
				PlayerCrew humanCrew = G.GetHumanCrew();
				if (humanCrew == null || building == null || posCallback == null)
				{
					return true;
				}

				Entity normalizedBuilding = NormalizeOwnedInteractionBuilding(building);
				if (!TryResolveBuildingInteractionMatches(building, out List<CrewAssignment> matches))
				{
					if (IsOwnedInteractionBuilding(normalizedBuilding))
					{
						MultiCrewVehicleHelper.ShowHudMessage("Vehicle must be at that building.");
						GameplayTweaksPlugin.VerificationLog(
							"VehicleNodeAuthority",
							$"owned-building-interaction-blocked building={normalizedBuilding?.Id.id ?? 0UL} reason=vehicle-not-physical");
						return false;
					}
					return true;
				}

				List<EntityID> peeps = matches
					.Select(match => match.peepId)
					.Where(peepId => peepId.IsValid)
					.Distinct()
					.ToList();
				if (peeps.Count <= 0)
				{
					return true;
				}

				if (KeyUtil.IsShiftDown)
				{
					EntityID quickTargetId = ResolveDefaultInteractionCrew(humanCrew, matches);
					if (quickTargetId.IsValid)
					{
						MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("OwnedBuildingInteractionSelectionPatch.StartOwnedBuildingInteraction");
						try
						{
							posCallback(quickTargetId, building);
						}
						finally
						{
							MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
						}
						return false;
					}
					return true;
				}

				if (peeps.Count == 1)
				{
					MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("OwnedBuildingInteractionSelectionPatch.StartOwnedBuildingInteraction");
					try
					{
						posCallback(peeps[0], building);
					}
					finally
					{
						MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
					}
					return false;
				}

				if (TryResolveSingleVehicleInteractionCrew(humanCrew, matches, out EntityID singleVehiclePeepId))
				{
					MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("OwnedBuildingInteractionSelectionPatch.StartOwnedBuildingInteraction");
					try
					{
						posCallback(singleVehiclePeepId, building);
					}
					finally
					{
						MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
					}
					return false;
				}

				if (MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
					&& TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out CrewAssignment selectedDriver)
					&& selectedDriver.peepId.IsValid)
				{
					MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("OwnedBuildingInteractionSelectionPatch.StartOwnedBuildingInteraction");
					try
					{
						posCallback(selectedDriver.peepId, building);
					}
					finally
					{
						MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
					}
					return false;
				}

				EntitySelectionPopup.ShowCrewSelector(peeps, Loc.Get("ui.crewinfo.pickone.safehouse"), peepId =>
				{
					posCallback(peepId, building);
				}, delegate
				{
					BuildingUtil.DeselectOnNextFrame();
				});
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] OwnedBuildingInteractionSelectionPatch.StartOwnedBuildingInteraction: " + ex.Message);
				return true;
			}
		}

		private static bool TryHandleOwnedConversationSelection(Entity selectionEntity, Entity conversationEntity, string selectionLocKey, Func<CrewAssignment, Entity, bool> startConversation)
		{
			string phase = "start";
			Entity interactionBuilding = null;
			Entity conversationTarget = null;
			try
			{
				phase = "crew";
				PlayerCrew humanCrew = G.GetHumanCrew();
				phase = "normalize-building";
				interactionBuilding = NormalizeOwnedInteractionBuilding(selectionEntity);
				conversationTarget = conversationEntity ?? interactionBuilding;
				if (humanCrew == null || selectionEntity == null || interactionBuilding == null || conversationTarget == null || startConversation == null)
				{
					return true;
				}

				phase = "resolve-matches";
				if (!TryResolveBuildingInteractionMatches(interactionBuilding, out List<CrewAssignment> matches))
				{
					if (IsOwnedInteractionBuilding(interactionBuilding))
					{
						MultiCrewVehicleHelper.ShowHudMessage("Vehicle must be at that building.");
						GameplayTweaksPlugin.VerificationLog(
							"VehicleNodeAuthority",
							$"owned-building-conversation-blocked building={interactionBuilding?.Id.id ?? 0UL} target={conversationTarget?.Id.id ?? 0UL} reason=vehicle-not-physical");
						return false;
					}
					return true;
				}

				phase = "quick";
				if (KeyUtil.IsShiftDown)
				{
					EntityID quickTargetId = ResolveDefaultInteractionCrew(humanCrew, matches);
					if (!quickTargetId.IsValid)
					{
						return true;
					}

					CrewAssignment quickTarget = humanCrew.GetCrewForPeep(quickTargetId);
					if (!quickTarget.IsValid || !TryStartOwnedConversationWithScope(quickTarget, interactionBuilding, conversationTarget, startConversation, "quick"))
					{
						return true;
					}
					return false;
				}

				phase = "build-peeps";
				List<EntityID> peeps = matches
					.Select(match => match.peepId)
					.Where(peepId => peepId.IsValid)
					.Distinct()
					.ToList();
				if (peeps.Count <= 0)
				{
					return true;
				}

				if (peeps.Count == 1)
				{
					phase = "single";
					CrewAssignment selectedCrew = humanCrew.GetCrewForPeep(peeps[0]);
					if (selectedCrew.IsValid && TryStartOwnedConversationWithScope(selectedCrew, interactionBuilding, conversationTarget, startConversation, "single"))
					{
						return false;
					}
					return true;
				}

				phase = "single-vehicle";
				if (TryResolveSingleVehicleInteractionCrew(humanCrew, matches, out EntityID singleVehiclePeepId))
				{
					CrewAssignment selectedCrew = humanCrew.GetCrewForPeep(singleVehiclePeepId);
					if (selectedCrew.IsValid && TryStartOwnedConversationWithScope(selectedCrew, interactionBuilding, conversationTarget, startConversation, "single-vehicle"))
					{
						return false;
					}
				}

				phase = "selected-vehicle";
				if (MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
					&& TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out CrewAssignment selectedDriver)
					&& TryStartOwnedConversationWithScope(selectedDriver, interactionBuilding, conversationTarget, startConversation, "selected-vehicle"))
				{
					return false;
				}

				phase = "selector";
				EntitySelectionPopup.ShowCrewSelector(peeps, Loc.Get(selectionLocKey), peepId =>
				{
					CrewAssignment selectedCrew = humanCrew.GetCrewForPeep(peepId);
					if (selectedCrew.IsValid)
					{
						TryStartOwnedConversationWithScope(selectedCrew, interactionBuilding, conversationTarget, startConversation, "selector");
					}
				}, delegate
				{
					BuildingUtil.DeselectOnNextFrame();
				});
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning(
					$"[GameplayTweaks] OwnedBuildingInteractionSelectionPatch.TryHandleOwnedConversationSelection phase={phase} selection={selectionEntity?.Id.id ?? 0UL} building={interactionBuilding?.Id.id ?? 0UL} target={conversationTarget?.Id.id ?? 0UL}: {ex}");
				return false;
			}
		}

		private static bool TryStartOwnedConversationWithScope(CrewAssignment selectedCrew, Entity interactionBuilding, Entity conversationTarget, Func<CrewAssignment, Entity, bool> startConversation, string source)
		{
			if (!selectedCrew.IsValid || interactionBuilding == null || conversationTarget == null || startConversation == null)
			{
				return false;
			}

			MultiCrewVehicleHelper.PushPendingBuildingInteractionScope("OwnedBuildingInteractionSelectionPatch.TryHandleOwnedConversationSelection");
			try
			{
				return startConversation(selectedCrew, conversationTarget);
			}
			catch (Exception ex)
			{
				Debug.LogWarning(
					$"[GameplayTweaks] OwnedBuildingInteractionSelectionPatch.StartConversation failed source={source} crew={selectedCrew.peepId.id} building={interactionBuilding.Id.id} target={conversationTarget.Id.id}: {ex}");
				return false;
			}
			finally
			{
				MultiCrewVehicleHelper.PopPendingBuildingInteractionScope();
			}
		}

		private static bool TryStartBizConversation(CrewAssignment selectedCrew, Entity biz, bool fromBizDialog)
		{
			if (!selectedCrew.IsValid || biz == null || BizStartConversationForCrewMethod == null)
			{
				return false;
			}

			BizStartConversationForCrewMethod.Invoke(null, new object[] { selectedCrew, biz, fromBizDialog });
			return true;
		}

		[HarmonyPrefix]
		internal static bool StartBizConversationPrefix(Entity biz, bool fromBizDialog)
		{
			return TryHandleOwnedConversationSelection(biz, biz, "ui.crewinfo.pickone.biz", (selectedCrew, conversationBiz) => TryStartBizConversation(selectedCrew, conversationBiz, fromBizDialog));
		}

		[HarmonyPrefix]
		internal static bool StartBizVisitConversationPrefix(VisitState visit, bool fromBizDialog)
		{
			Entity selectionBuilding = visit?.building ?? visit?.biz;
			Entity conversationBiz = visit?.biz;
			return TryHandleOwnedConversationSelection(selectionBuilding, conversationBiz, "ui.crewinfo.pickone.biz", (selectedCrew, targetBiz) => TryStartBizConversation(selectedCrew, targetBiz, fromBizDialog));
		}

		[HarmonyPrefix]
		internal static bool StartCasinoConversationPrefix(Entity building)
		{
			return TryHandleOwnedConversationSelection(building, building, "ui.crewinfo.pickone.biz", (selectedCrew, interactionBuilding) =>
			{
				if (!selectedCrew.IsValid || interactionBuilding == null)
				{
					return false;
				}

				ResidenceComponent.StartCasinoConversation(selectedCrew, interactionBuilding, fromGamblingDialog: false);
				return true;
			});
		}

		[HarmonyPrefix]
		internal static bool StartCasinoVisitConversationPrefix(VisitState visit, bool fromGamblingDialog)
		{
			Entity building = visit?.building;
			return TryHandleOwnedConversationSelection(building, building, "ui.crewinfo.pickone.biz", (selectedCrew, interactionBuilding) =>
			{
				if (!selectedCrew.IsValid || interactionBuilding == null)
				{
					return false;
				}

				ResidenceComponent.StartCasinoConversation(selectedCrew, interactionBuilding, fromGamblingDialog);
				return true;
			});
		}
	}

	internal static class StartupFounderVehicleCreatePatch
	{
		[HarmonyPrefix]
		internal static void Prefix(PlayerCrew __instance, Entity peep, bool isBoss)
		{
			if (__instance == null || peep == null || !isBoss || !MultiCrewVehicleHelper.IsFreshHumanStartup(__instance.PID))
				return;
			MultiCrewVehicleHelper.BeginStartupFounderVehicleAssignment(__instance, peep.Id);
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerCrew __instance, Entity peep, bool isBoss, Entity __result)
		{
			if (__instance == null || peep == null || __result == null || !isBoss || !MultiCrewVehicleHelper.IsFreshHumanStartup(__instance.PID))
				return;
			MultiCrewVehicleHelper.RecordStartupCanonicalVehicle(__instance, peep, __result);
		}

		[HarmonyFinalizer]
		internal static Exception Finalizer(PlayerCrew __instance, bool isBoss, Exception __exception)
		{
			if (__instance != null && isBoss)
				MultiCrewVehicleHelper.EndStartupFounderVehicleAssignment(__instance);
			return __exception;
		}
	}

	internal static class GrantStarterPacksFounderVehiclePatch
	{
		[HarmonyPrefix]
		internal static void Prefix(PlayerInfo player)
		{
			try
			{
				if (player == null || !player.IsHuman || !MultiCrewVehicleHelper.IsFreshHumanStartup(player.PID))
					return;
				MultiCrewVehicleHelper.EnsureFounderStartsInCanonicalVehicle(player);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GrantStarterPacksFounderVehiclePatch: " + ex.Message);
			}
		}
	}

	internal static class VisitGrantListStartupVehiclePatch
	{
		private static readonly GrantReq[] AllRequirements = Enum.GetValues(typeof(GrantReq)).Cast<GrantReq>().ToArray();

		[HarmonyPrefix]
		internal static bool Prefix(VisitGrantList __instance, GrantContext ctx)
		{
			if (!ShouldHandleStartupVehicleFlow(__instance, ctx))
				return true;

			try
			{
				GrantContext workingCtx = ctx;
				PlayerInfo player = workingCtx.GetPlayer();
				PlayerCrew humanCrew = player?.crew;
				if (player == null || humanCrew == null)
					return true;

				if (workingCtx.visit != null && workingCtx.visit.vehicle == null)
					workingCtx.visit.vehicle = MultiCrewVehicleHelper.ResolveStartupGrantVehicle(player);

				foreach (VisitGrant grant in __instance)
				{
					GrantReq failed = FindFailedRequirements(grant, workingCtx.FindFulfilledReqs());
					if (failed != GrantReq.Nothing && !(grant is GrantVehicle))
					{
						Debug.LogWarning($"Grant {grant} did not receive required context data: {failed}");
						continue;
					}

					if (grant is GrantVehicle grantVehicle)
					{
						ApplyGrantVehicleWithMutableContext(grantVehicle, ref workingCtx, player);
						continue;
					}

					grant.Apply(workingCtx);
				}

				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] VisitGrantListStartupVehiclePatch: " + ex.Message);
				return true;
			}
		}

		private static bool ShouldHandleStartupVehicleFlow(VisitGrantList grants, GrantContext ctx)
		{
			if (grants == null || ctx.pid.IsNotValid || !ctx.pid.IsHumanPlayer)
				return false;
			if (global::Game.Game.ctx == null || global::Game.Game.ctx.HasSaveFile)
				return false;
			if (ctx.visit == null || !ctx.visit.crew.IsValid)
				return false;
			MultiCrewVehicleHelper.ResetFreshStartupPendingTravelPrune(ctx.pid);
			return grants.Any(grant => grant is GrantVehicle || (grant.RequiredContext & GrantReq.VisitVehicle) == GrantReq.VisitVehicle);
		}

		private static GrantReq FindFailedRequirements(VisitGrant grant, GrantReq fulfilled)
		{
			GrantReq requiredContext = grant.RequiredContext;
			if (requiredContext == GrantReq.Nothing)
				return GrantReq.Nothing;

			GrantReq failed = GrantReq.Nothing;
			foreach (GrantReq requirement in AllRequirements)
			{
				if (requirement == GrantReq.Nothing)
					continue;
				bool required = (requiredContext & requirement) == requirement;
				bool hasValue = (fulfilled & requirement) == requirement;
				if (required && !hasValue)
					failed |= requirement;
			}
			return failed;
		}

		private static Entity ApplyGrantVehicleWithMutableContext(GrantVehicle grantVehicle, ref GrantContext workingCtx, PlayerInfo player)
		{
			if (grantVehicle == null || player?.crew == null)
				return null;

			Node spawnNode = workingCtx.visit?.GetCrewNode();
			if (spawnNode == null && workingCtx.visit?.crew.IsValid == true)
				MultiCrewVehicleHelper.TryGetCrewCommandNode(workingCtx.visit.crew, out spawnNode);
			if (spawnNode == null)
			{
				CrewAssignment founder = player.crew.GetCrewForPlayerPeep();
				MultiCrewVehicleHelper.TryGetCrewCommandNode(founder, out spawnNode);
			}
			if (spawnNode == null)
				spawnNode = player.territory?.GetHeadquartersNode();
			if (spawnNode == null)
			{
				Debug.LogWarning("[GameplayTweaks] GrantVehicle startup flow missing fallback node.");
				return null;
			}

			Entity vehicle = player.crew.CreateAndTrackVehicle(grantVehicle.id, spawnNode.pos);
			if (workingCtx.pid.IsHumanPlayer)
				PlayerMeetings.RevealAllUnitsOfPlayer(workingCtx.pid);
				if (vehicle != null)
				{
					var healthFraction = SomaSim.Util.Fixnum.Clamp(grantVehicle.healthfraction, 0, 1);
					var maxHealth = vehicle.components.mobile.MaxHealth();
					vehicle.components.mobile.SetHealth(CrewAssignment.EMPTY, healthFraction * maxHealth);
					workingCtx.vehicleTarget = vehicle.Id;
					if (workingCtx.visit != null)
						workingCtx.visit.vehicle = vehicle;
				}
			return vehicle;
		}
	}

	internal static class BuildingPickScopeOutPatch
	{
		private static bool TryResolveExclusiveScopeCrew(PlayerCrew humanCrew, List<CrewAssignment> committedMatches, out EntityID peepId, out string source)
		{
			peepId = EntityID.INVALID;
			source = "none";
			if (committedMatches == null || committedMatches.Count <= 0)
			{
				return false;
			}

			if (committedMatches.Count == 1 && committedMatches[0].peepId.IsValid)
			{
				peepId = committedMatches[0].peepId;
				source = committedMatches[0].IsInVehicle ? "vehicle-driver" : "single-crew";
				return true;
			}

			return false;
		}

		private static void ExpandVehicleScopeCrewChoices(PlayerCrew humanCrew, List<CrewAssignment> committedMatches, List<EntityID> presentCrew)
		{
			if (humanCrew == null || committedMatches == null || presentCrew == null || committedMatches.Count != 1 || !committedMatches[0].IsValid || !committedMatches[0].IsInVehicle || !committedMatches[0].VehicleID.IsValid)
			{
				return;
			}
			List<EntityID> list = MultiCrewVehicleHelper.GetAllCrewInVehicle(humanCrew, committedMatches[0].VehicleID)
				.Where(match => match.IsValid && match.peepId.IsValid && humanCrew.IsOnBoard(match.peepId) && !GameplayTweaksPlugin.IsCrewCurrentlyJailed(match.peepId))
				.Select(match => match.peepId)
				.Distinct()
				.ToList();
			if (list.Count <= presentCrew.Count)
			{
				return;
			}
			presentCrew.Clear();
			presentCrew.AddRange(list);
		}

		[HarmonyPrefix]
		internal static bool Prefix(Entity __0)
		{
			List<NodeID> expectedComparisonNodeIds = new List<NodeID>();
			try
			{
				if (!CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(__0, out Node node, out List<NodeID> comparisonNodeIds, out _) || node == null || comparisonNodeIds == null || comparisonNodeIds.Count <= 0)
					return true;
				if (G.GetHumanPlayer()?.territory?.IsScoped(__0) == true)
				{
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-selector-scoped-fallthrough building={__0?.Id.id ?? 0UL} node={node.id}");
					return true;
				}
				PlayerCrew humanCrew = global::Game.Game.ctx?.players?.Human?.crew;
				expectedComparisonNodeIds = comparisonNodeIds.ToList();
				List<EntityID> presentCrew = CommandButtonScopeOutPatch.TryGetCommittedScopeMatchesForBuilding(humanCrew, __0, out List<CrewAssignment> committedMatches, out _, out _, out _)
					? committedMatches
					.Select(assignment => assignment.peepId)
					.Where(peepId => peepId.IsValid)
					.ToList()
					: new List<EntityID>();
				ExpandVehicleScopeCrewChoices(humanCrew, committedMatches, presentCrew);
				EntityID buildingId = __0?.Id ?? EntityID.INVALID;
				EntityID selectedScopeVehicleId = EntityID.INVALID;
				bool recentlyRejectedWrongCorner = humanCrew != null
					&& MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out selectedScopeVehicleId, out _)
					&& selectedScopeVehicleId.IsValid
					&& CommandButtonScopeOutPatch.IsRecentlyRejectedWrongCornerScopeBuilding(__0, selectedScopeVehicleId);
				if (recentlyRejectedWrongCorner)
				{
					committedMatches = new List<CrewAssignment>();
					presentCrew.Clear();
					GameplayTweaksPlugin.VerificationLog(
						"ScopeOut",
						$"scope-selector-rejected-suppressed vehicle={selectedScopeVehicleId.id} building={buildingId.id}");
				}
				if (presentCrew.Count == 0
					&& humanCrew != null
					&& !OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(__0)
					&& !recentlyRejectedWrongCorner
					&& MultiCrewVehicleHelper.TryGetHumanCrewUsableForScopePreviewAtBuilding(humanCrew, __0, out List<CrewAssignment> previewMatches, out string previewSource)
					&& previewMatches.Count > 0)
				{
					committedMatches = previewMatches;
					presentCrew = previewMatches
						.Select(assignment => assignment.peepId)
						.Where(peepId => peepId.IsValid)
						.Distinct()
						.ToList();
					ExpandVehicleScopeCrewChoices(humanCrew, committedMatches, presentCrew);
					GameplayTweaksPlugin.VerificationLog(
						"ScopeOut",
						$"scope-selector-final-goal-preview building={buildingId.id} crew={string.Join(",", presentCrew.Select(peepId => peepId.id.ToString()))} source={previewSource}");
				}
				if (presentCrew.Count == 0)
				{
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-selector-empty node={node.id} building={buildingId.id}");
					MultiCrewVehicleHelper.ShowHudMessage(WrongCornerText);
					return false;
				}
				if (TryResolveExclusiveScopeCrew(humanCrew, committedMatches, out EntityID exclusivePeepId, out string exclusiveSource))
				{
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-selector-bypassed building={buildingId.id} crew={exclusivePeepId.id} source={exclusiveSource}");
					Continue(exclusivePeepId, buildingId);
					return false;
				}
				EntitySelectionPopup.ShowCrewSelector(presentCrew, Loc.Get("ui.entityselection.scopeout"), delegate(EntityID peepId)
				{
					Continue(peepId, buildingId);
				});
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] BuildingPickScopeOutPatch: " + ex.Message);
				return true;
			}

			void Continue(EntityID peepId, EntityID targetBuildingId)
			{
				PlayerCrew humanCrew = global::Game.Game.ctx.players.Human?.crew;
				CrewAssignment crew = humanCrew?.GetCrewForPeep(peepId) ?? CrewAssignment.EMPTY;
				Entity building = targetBuildingId.FindEntity();
				if (!crew.IsValid || !crew.peepId.IsValid)
				{
					LogScopeMapDisabled("invalid-crew", peepId, targetBuildingId);
					MultiCrewVehicleHelper.ShowHudMessage("Selected crew is no longer available.");
					return;
				}
				if (building == null
					|| !CommandButtonScopeOutPatch.TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> currentComparisonNodeIds, out _)
					|| currentComparisonNodeIds == null
					|| currentComparisonNodeIds.Count <= 0
					|| !expectedComparisonNodeIds.Any(currentComparisonNodeIds.Contains))
				{
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-map-stale-target building={targetBuildingId.id} crew={crew.peepId.id}");
					MultiCrewVehicleHelper.ShowHudMessage("That building is no longer available.");
					return;
				}
				CommandButtonScopeOut commandButtonScopeOut = HumanCommandValidator.FindValidator(CommandType.ScopeOut) as CommandButtonScopeOut;
				if (commandButtonScopeOut == null)
					return;
				CommandStatus commandStatus = CommandButtonScopeOutPatch.BuildScopeStatus(PlayerID.HumanPlayer, crew, building, out _);
				if (commandStatus.IsEnabled)
				{
					commandButtonScopeOut.OnHumanButtonClick(crew, building);
					return;
				}
				if (CommandButtonScopeOutPatch.ShouldSilentlyIgnoreScopeTarget(building))
					return;
				string mouseover = string.IsNullOrWhiteSpace(commandStatus.mouseover) ? "Scope unavailable." : commandStatus.mouseover;
				if (commandStatus.status == CommandEnabledStatus.DisabledOther && mouseover == Loc.Get("ui.command.fail.points"))
				{
					string text = TextUtil.ColorWrap(Loc.Get("ui.actioncost.fail"), ColorConstants.TEXT_HEX_RED);
					global::Game.Game.ctx.hud.flyouts.MakeSimpleTextFlyout(building.data.board.worldpos, text, 3f);
					global::Game.Game.ctx.sfx.PlayOutOfPoints(moves: false, actions: true);
					global::Game.Game.ctx.events.EnqueueOnce(SessionEventType.UIInsufficientActionPoints, PlayerID.HumanPlayer);
					return;
				}
				string disabledReason = CommandButtonScopeOutPatch.DescribeScopeDisabledReason(commandStatus);
				LogScopeMapDisabled(disabledReason, crew.peepId, building.Id);
				if (string.Equals(disabledReason, "wrong-corner", StringComparison.Ordinal))
				{
					CommandButtonScopeOutPatch.RecordWrongCornerScopeRejection(building, crew.VehicleID, crew.peepId);
					BuildingUtil.DeselectOnNextFrame();
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-wrong-corner-selection-cleared crew={crew.peepId.id} building={building.Id.id}");
				}
				MultiCrewVehicleHelper.ShowHudMessage(mouseover);
			}

			void LogScopeMapDisabled(string reason, EntityID peepId, EntityID buildingId)
			{
				GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-map-disabled reason={reason} crew={peepId.id} building={buildingId.id}");
			}
		}

		private const string WrongCornerText = "Crew must be on that corner.";
	}

	internal static class CommandButtonScopeOutPatch
	{
		private const string AlreadyScopedText = "Already scoped out";
		private const string WrongCornerText = "Crew must be on that corner.";
		private const string DriverOnlyText = "Only the driver can scope out from a vehicle.";
		private const string ChooseDestinationBusinessText = "Choose a business on the destination corner.";
		private const int WrongCornerScopeRejectTtlFrames = 180;
		private static readonly Dictionary<ulong, int> WrongCornerScopeRejectFrameByVehicleBuilding = new Dictionary<ulong, int>();

		internal static void RecordWrongCornerScopeRejection(Entity building, EntityID vehicleId, EntityID crewId)
		{
			if (building == null || !building.Id.IsValid || !vehicleId.IsValid)
			{
				return;
			}

			WrongCornerScopeRejectFrameByVehicleBuilding[MakeWrongCornerScopeRejectKey(building.Id, vehicleId)] = Time.frameCount;
			GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-wrong-corner-preview-suppressed vehicle={vehicleId.id} crew={crewId.id} building={building.Id.id} frame={Time.frameCount}");
			RefreshScopedBuildingPicks(building, vehicleId, "scope-wrong-corner-rejected");
		}

		internal static bool IsRecentlyRejectedWrongCornerScopeBuilding(Entity building, EntityID vehicleId)
		{
			if (building == null || !building.Id.IsValid || !vehicleId.IsValid)
			{
				return false;
			}

			ulong key = MakeWrongCornerScopeRejectKey(building.Id, vehicleId);
			if (!WrongCornerScopeRejectFrameByVehicleBuilding.TryGetValue(key, out int frame))
			{
				return false;
			}

			if (Time.frameCount - frame > WrongCornerScopeRejectTtlFrames)
			{
				WrongCornerScopeRejectFrameByVehicleBuilding.Remove(key);
				return false;
			}

			GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-wrong-corner-presence-blocked vehicle={vehicleId.id} building={building.Id.id} frame={Time.frameCount} rejectedFrame={frame}");
			return true;
		}

		private static ulong MakeWrongCornerScopeRejectKey(EntityID buildingId, EntityID vehicleId)
		{
			return buildingId.id ^ (vehicleId.id * 1099511628211UL);
		}

		private static bool IsDirectPreviewScopeSource(string source)
		{
			if (string.IsNullOrWhiteSpace(source))
			{
				return false;
			}

			return source.StartsWith("selected-", StringComparison.Ordinal)
				|| source.StartsWith("selection-", StringComparison.Ordinal)
				|| source.StartsWith("route-final-goal", StringComparison.Ordinal)
				|| source.StartsWith("queued-final-goal", StringComparison.Ordinal)
				|| string.Equals(source, "queued-resume-goal", StringComparison.Ordinal);
		}

		private static bool TryResolveAuthorizedScopeStatusNode(PlayerCrew humanCrew, CrewAssignment crew, Entity building, out Node node, out string sourceTag)
		{
			node = null;
			sourceTag = "none";
			if (humanCrew == null
				|| !crew.IsValid
				|| !crew.peepId.IsValid
				|| building == null)
			{
				return false;
			}

			bool hasAuthorizedMatch = TryGetCommittedScopeMatchesForBuilding(humanCrew, building, out List<CrewAssignment> matches, out sourceTag, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId)
				&& matches != null
				&& matches.Any(match => match.IsValid && match.peepId == crew.peepId);
			if (!hasAuthorizedMatch
				&& MultiCrewVehicleHelper.TryGetHumanCrewUsableForScopePreviewAtBuilding(humanCrew, building, out List<CrewAssignment> previewMatches, out string previewSource)
				&& previewMatches != null
				&& previewMatches.Any(match => match.IsValid && match.peepId == crew.peepId))
			{
				matches = previewMatches;
				sourceTag = previewSource;
				if ((comparisonNodeIds == null || comparisonNodeIds.Count <= 0)
					&& TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> resolvedComparisonNodeIds, out NodeID resolvedComparisonNodeId))
				{
					comparisonNodeIds = resolvedComparisonNodeIds;
					comparisonNodeId = resolvedComparisonNodeId;
				}

				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-status-preview-match-fallback vehicle={(crew.VehicleID.IsValid ? crew.VehicleID.id : 0UL)} crew={crew.peepId.id} building={building.Id.id} source={sourceTag}");
				hasAuthorizedMatch = true;
			}
			if (!hasAuthorizedMatch)
			{
				return false;
			}

			NodeID matchedNodeId = NodeID.INVALID;
			if (crew.IsInVehicle && crew.VehicleID.IsValid)
			{
				if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(crew.VehicleID, out _, out NodeID expectedNodeId, out NodeID goalNodeId))
				{
					if (goalNodeId.IsValid
						&& TryMatchScopePreviewNodeToBuilding(building, goalNodeId, out _, out _, out NodeID matchedGoalNodeId, out _))
					{
						matchedNodeId = matchedGoalNodeId;
					}
					else if (expectedNodeId.IsValid
						&& TryMatchScopePreviewNodeToBuilding(building, expectedNodeId, out _, out _, out NodeID matchedExpectedNodeId, out _))
					{
						matchedNodeId = matchedExpectedNodeId;
					}
				}

				if (!matchedNodeId.IsValid
					&& MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(crew.VehicleID, out NodeID previewNodeId, out _, "scope-building")
					&& previewNodeId.IsValid
					&& TryMatchScopePreviewNodeToBuilding(building, previewNodeId, out _, out _, out NodeID matchedPreviewNodeId, out _))
				{
					matchedNodeId = matchedPreviewNodeId;
				}

				if (!matchedNodeId.IsValid
					&& MultiCrewVehicleHelper.TryGetBuildingHumanVehicleInteractiveNodeId(crew.VehicleID, out NodeID interactiveNodeId, out _)
					&& interactiveNodeId.IsValid
					&& TryMatchScopePreviewNodeToBuilding(building, interactiveNodeId, out _, out _, out NodeID matchedInteractiveNodeId, out _))
				{
					matchedNodeId = matchedInteractiveNodeId;
				}
			}

			bool sourceIsPreviewAuthority = IsDirectPreviewScopeSource(sourceTag);
			if (!matchedNodeId.IsValid && sourceIsPreviewAuthority)
			{
				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-status-preview-rejected vehicle={(crew.VehicleID.IsValid ? crew.VehicleID.id : 0UL)} crew={crew.peepId.id} building={building.Id.id} source={sourceTag} reason=preview-node-mismatch");
				return false;
			}

			if (!matchedNodeId.IsValid)
			{
				matchedNodeId = comparisonNodeId.IsValid
				? comparisonNodeId
					: comparisonNodeIds?.FirstOrDefault(candidate => candidate.IsValid) ?? NodeID.INVALID;
			}

			bool physicallyAtMatchedNode = crew.IsInVehicle
				&& crew.VehicleID.IsValid
				&& matchedNodeId.IsValid
				&& MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(crew.VehicleID, matchedNodeId);
			if (!sourceIsPreviewAuthority && !physicallyAtMatchedNode)
			{
				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-status-preview-rejected vehicle={(crew.VehicleID.IsValid ? crew.VehicleID.id : 0UL)} crew={crew.peepId.id} building={building.Id.id} matchedNode={matchedNodeId} source={sourceTag} reason=not-preview-or-physical");
				return false;
			}

			node = matchedNodeId.FindNode();
			if (node == null && TryResolveScopeBuildingNode(building, out Node buildingNode))
			{
				node = buildingNode;
			}

			return node != null;
		}

		[HarmonyPrefix]
		internal static bool ValidatePrefix(PlayerID pid, CrewAssignment crew, ref CommandStatus __result)
		{
			try
			{
				__result = BuildScopeStatus(pid, crew, null, out _);
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonScopeOutPatch.Validate: " + ex.Message);
				return true;
			}
		}

		[HarmonyPrefix]
		internal static bool PostCommandAsHumanPrefix(CrewAssignment crew)
		{
			try
			{
				CommandStatus commandStatus = BuildScopeStatus(PlayerID.HumanPlayer, crew, null, out Node node);
				if (!commandStatus.IsEnabled || node == null)
					return false;
				HumanCommandValidator.PostCommandHelper(new CommandScopeOut(PlayerID.HumanPlayer, crew.peepId, node));
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonScopeOutPatch.PostCommandAsHuman: " + ex.Message);
				return true;
			}
		}

		[HarmonyPrefix]
		internal static bool OnHumanButtonClickPrefix(CrewAssignment crew, Entity building)
		{
			try
			{
				CommandStatus commandStatus = BuildScopeStatus(PlayerID.HumanPlayer, crew, building, out Node node);
				if (!commandStatus.IsEnabled || node == null)
					return false;
				if (crew.IsInVehicle && TryExecutePreviewScopeOut(crew, building))
				{
					return false;
				}
				HumanCommandValidator.PostCommandHelper(new CommandScopeOut(PlayerID.HumanPlayer, crew.peepId, node, building));
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandButtonScopeOutPatch.OnHumanButtonClick: " + ex.Message);
				return true;
			}
		}

		internal static CommandStatus BuildScopeStatus(PlayerID pid, CrewAssignment crew, Entity building, out Node node)
		{
			CommandStatus commandStatus = new CommandStatus(CommandType.ScopeOut, Loc.Get("ui.command.scope.icon"));
			node = null;
			if (!crew.IsValid || !crew.peepId.IsValid)
				return commandStatus.Set(CommandEnabledStatus.DisabledOther, "Selected crew is no longer available.");
			if (!TryValidateScopeActor(crew, out string actorError))
				return commandStatus.Set(CommandEnabledStatus.DisabledOther, actorError);
			if (!TryResolveScopeNode(crew, building, out node) || node == null)
				return commandStatus;
			PlayerInfo playerInfo = G.FindPlayerById(pid.id);
			if (building != null)
			{
				if (TryResolveCrewLiveNode(crew, out Node crewNode) && crewNode != null)
				{
					if (TryMatchScopePreviewNodeToBuilding(building, crewNode.id, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId, out NodeID matchedNodeId, out bool clusterMatched))
					{
						if (clusterMatched || (comparisonNodeIds.Count > 1 && matchedNodeId.IsValid && matchedNodeId != comparisonNodeId))
						{
							MultiCrewVehicleHelper.LogVehicleAuthority("scope-preview-edge-normalized", $"{crew.VehicleID.id}:{comparisonNodeId}:{matchedNodeId}:{building.Id.id}:crewhud", $"scope-preview-edge-normalized vehicle={crew.VehicleID.id} requestedNode={comparisonNodeId} matchedNode={matchedNodeId} normalizedNodes={string.Join(",", comparisonNodeIds.Select(nodeId => nodeId.ToString()))} building={building.Id.id} source=crewhud-live clusterMatched={clusterMatched}", dedupe: false);
						}
					}
					else
					{
						PlayerCrew humanCrew = G.GetHumanCrew();
						if (TryResolveAuthorizedScopeStatusNode(humanCrew, crew, building, out Node authorizedNode, out string authorizedSource))
						{
							node = authorizedNode;
							MultiCrewVehicleHelper.LogVehicleAuthority("scope-status-preview-authorized", $"{crew.VehicleID.id}:{node.id}:{crewNode.id}:{building.Id.id}:{authorizedSource}", $"scope-status-preview-authorized vehicle={crew.VehicleID.id} requestedNode={node.id} staleNode={crewNode.id} building={building.Id.id} source={authorizedSource}", dedupe: false);
						}
						else
						{
							MultiCrewVehicleHelper.LogVehicleAuthority("scope-preview-edge-blocked", $"{crew.VehicleID.id}:{node?.id ?? NodeID.INVALID}:{crewNode.id}:{building.Id.id}:crewhud", $"scope-preview-edge-blocked vehicle={crew.VehicleID.id} requestedNode={node?.id ?? NodeID.INVALID} resolvedNode={crewNode.id} building={building.Id.id} reason=live-node-mismatch", dedupe: false);
							return commandStatus.Set(CommandEnabledStatus.DisabledOther, WrongCornerText);
						}
					}
				}
				if (building.components?.building == null || !building.components.building.CanBeScopedOutByPlayer(pid))
					return commandStatus;
				if (playerInfo?.territory != null)
				{
					if (playerInfo.territory.IsScoped(building) || playerInfo.territory.ScopeOutReserved(building.Id))
						return commandStatus.Set(CommandEnabledStatus.DisabledOther, AlreadyScopedText);
				}
			}
			else
			{
				if (crew.IsInVehicle
					&& crew.VehicleID.IsValid
					&& (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(crew.VehicleID)
						|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(crew.VehicleID))
					&& TryResolveCrewScopePreviewNode(crew, out Node previewNode)
					&& previewNode != null)
				{
					List<EntityID> previewCandidates = previewNode.MakeListOfScopeOutCandidates(pid);
					if (previewCandidates.Count > 1)
						return commandStatus.Set(CommandEnabledStatus.DisabledOther, ChooseDestinationBusinessText);
				}
				if (!node.CanBeScopedOut(pid))
					return commandStatus;
				if (playerInfo?.territory != null && playerInfo.territory.AreAllScoped(node))
					return commandStatus.Set(CommandEnabledStatus.DisabledOther, AlreadyScopedText);
			}
			Entity peep = crew.GetPeep();
			if (peep?.components?.agent == null)
				return commandStatus;
			if (peep.components.agent.IsInjured())
				return commandStatus.Set(CommandEnabledStatus.DisabledInjury, Loc.Get("ui.command.fail.injury"));
			if (peep.components.agent.WasCrewUnpaidLastTurn())
			{
				GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-salary-bypass crew={crew.peepId.id} building={building?.Id.id ?? 0UL} node={node?.id ?? NodeID.INVALID}");
			}
			if (!peep.components.agent.HasActionsRemaining)
				return commandStatus.Set(CommandEnabledStatus.DisabledOther, Loc.Get("ui.command.fail.points"));
			return commandStatus.Set(CommandEnabledStatus.Enabled, Loc.Get("ui.command.scope.mo"));
		}

		internal static string DescribeScopeDisabledReason(CommandStatus status)
		{
			string mouseover = status.mouseover ?? string.Empty;
			if (string.Equals(mouseover, AlreadyScopedText, StringComparison.Ordinal))
				return "already-scoped";
			if (string.Equals(mouseover, WrongCornerText, StringComparison.Ordinal))
				return "wrong-corner";
			if (string.Equals(mouseover, DriverOnlyText, StringComparison.Ordinal))
				return "driver-only";
			if (string.Equals(mouseover, ChooseDestinationBusinessText, StringComparison.Ordinal))
				return "choose-destination-business";
			if (string.Equals(mouseover, Loc.Get("ui.command.fail.injury"), StringComparison.Ordinal))
				return "injury";
			if (string.Equals(mouseover, Loc.Get("ui.command.fail.salary"), StringComparison.Ordinal))
				return "salary";
			if (string.Equals(mouseover, Loc.Get("ui.command.fail.points"), StringComparison.Ordinal))
				return "points";
			if (string.Equals(mouseover, "Selected crew is no longer available.", StringComparison.Ordinal))
				return "invalid-crew";
			return "other";
		}

		internal static bool ShouldSilentlyIgnoreScopeTarget(Entity building)
		{
			PlayerTerritory territory = G.GetHumanPlayer()?.territory;
			if (territory == null || building == null)
				return false;
			return territory.IsScoped(building) || territory.ScopeOutReserved(building.Id);
		}

		internal static List<CrewAssignment> FilterSelectedVehicleTransientScopeMatches(Entity building, IEnumerable<NodeID> comparisonNodeIds, List<CrewAssignment> matches, string sourceTag)
		{
			List<CrewAssignment> normalizedMatches = matches?
				.Where(match => match.IsValid && match.peepId.IsValid)
				.ToList() ?? new List<CrewAssignment>();
			if (normalizedMatches.Count <= 0)
			{
				return normalizedMatches;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (!MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
				|| !selectedVehicleId.IsValid)
			{
				return normalizedMatches;
			}

			bool routeAuthorityActive = MultiCrewVehicleHelper.IsHumanVehicleTravelActive(selectedVehicleId)
				|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(selectedVehicleId)
				|| MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(selectedVehicleId, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
					&& (expectedNodeId.IsValid || goalNodeId.IsValid);
			if (!routeAuthorityActive)
			{
				return normalizedMatches;
			}

			List<NodeID> nodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (nodeIds.Count <= 0 && TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> resolvedNodeIds, out _))
			{
				nodeIds = resolvedNodeIds
					.Where(nodeId => nodeId.IsValid)
					.Distinct()
					.ToList();
			}

			bool buildingMatchesPreview = MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out NodeID previewNodeId, out string previewSource, "scope-building")
				&& previewNodeId.IsValid
				&& TryMatchScopePreviewNodeToBuilding(building, previewNodeId, out _, out _, out _, out _);
			bool physicallyAtBuildingNode = nodeIds.Any(nodeId => MultiCrewVehicleHelper.IsHumanVehiclePhysicallyAtNode(selectedVehicleId, nodeId));
			if (buildingMatchesPreview || physicallyAtBuildingNode)
			{
				return normalizedMatches;
			}

			List<CrewAssignment> filtered = normalizedMatches
				.Where(match => !match.IsInVehicle || !match.VehicleID.IsValid || match.VehicleID != selectedVehicleId)
				.ToList();
			if (filtered.Count != normalizedMatches.Count)
			{
				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-transient-selected-filtered building={building?.Id.id ?? 0UL} vehicle={selectedVehicleId.id} before={normalizedMatches.Count} after={filtered.Count} source={sourceTag} previewNode={previewNodeId} previewSource={previewSource} nodes={string.Join(",", nodeIds.Select(nodeId => nodeId.ToString()))}");
			}

			return filtered;
		}

		internal static List<CrewAssignment> FilterSafehouseIdleCrewForBuilding(Entity building, IEnumerable<NodeID> comparisonNodeIds, List<CrewAssignment> matches, string sourceTag)
		{
			List<CrewAssignment> normalizedMatches = matches?
				.Where(match => match.IsValid && match.peepId.IsValid)
				.ToList() ?? new List<CrewAssignment>();
			bool safehouseInteractionBuilding = OwnedBuildingInteractionSelectionPatch.IsHumanSafehouseInteractionBuilding(building);
			bool safehouseCornerBuilding = IsHumanSafehouseCornerNonSafehouseBuilding(building, comparisonNodeIds);
			if (normalizedMatches.Count <= 0 || (!safehouseInteractionBuilding && !safehouseCornerBuilding))
			{
				return normalizedMatches;
			}

			List<NodeID> physicalNodeIds = ResolveSafehouseSensitiveComparisonNodeIds(building, comparisonNodeIds);
			List<CrewAssignment> filtered = normalizedMatches
				.Where(match => match.IsInVehicle
					&& match.VehicleID.IsValid
					&& OwnedBuildingInteractionSelectionPatch.TryGetVehicleLiveOwnedBuildingAccessNode(match.VehicleID, building, physicalNodeIds, out _, out _, out _))
				.ToList();
			if (filtered.Count != normalizedMatches.Count)
			{
				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"safehouse-access-crew-filtered building={building?.Id.id ?? 0UL} before={normalizedMatches.Count} after={filtered.Count} source={sourceTag} safehouse={safehouseInteractionBuilding} nodes={string.Join(",", physicalNodeIds.Select(nodeId => nodeId.ToString()))}");
			}
			return filtered;
		}

		private static List<NodeID> ResolveSafehouseSensitiveComparisonNodeIds(Entity building, IEnumerable<NodeID> comparisonNodeIds)
		{
			List<NodeID> nodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (nodeIds.Count <= 0 && TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> resolvedNodeIds, out _))
			{
				nodeIds = resolvedNodeIds
					.Where(nodeId => nodeId.IsValid)
					.Distinct()
					.ToList();
			}

			if (building != null
				&& MultiCrewVehicleHelper.TryGetEntityBoardNodeId(building, out NodeID buildingNodeId)
				&& buildingNodeId.IsValid
				&& !nodeIds.Contains(buildingNodeId))
			{
				nodeIds.Add(buildingNodeId);
			}

			return nodeIds;
		}

		private static bool IsHumanSafehouseCornerNonSafehouseBuilding(Entity building, IEnumerable<NodeID> comparisonNodeIds)
		{
			PlayerInfo humanPlayer = G.GetHumanPlayer();
			if (building == null
				|| humanPlayer?.territory == null
				|| humanPlayer.territory.Safehouse.IsNotValid
				|| OwnedBuildingInteractionSelectionPatch.IsHumanSafehouseInteractionBuilding(building))
			{
				return false;
			}

			Entity safehouse = humanPlayer.territory.Safehouse.FindEntity();
			if (safehouse == null)
			{
				return false;
			}

			NodeID safehouseNodeId = NodeID.INVALID;
			if (!TryResolveScopeBuildingBoardNodeId(safehouse, out safehouseNodeId) || !safehouseNodeId.IsValid)
			{
				safehouseNodeId = safehouse.data?.board?.bead.nodeId ?? NodeID.INVALID;
			}
			if (!safehouseNodeId.IsValid)
			{
				return false;
			}

			List<NodeID> nodeIds = comparisonNodeIds?
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList() ?? new List<NodeID>();
			if (nodeIds.Count <= 0 && TryGetScopeInteractionNodeIds(building, out _, out List<NodeID> resolvedNodeIds, out _))
			{
				nodeIds = resolvedNodeIds
					.Where(nodeId => nodeId.IsValid)
					.Distinct()
					.ToList();
			}

			return nodeIds.Contains(safehouseNodeId);
		}

		internal static bool TryGetCommittedScopeMatchesForBuilding(PlayerCrew humanCrew, Entity building, out List<CrewAssignment> matches, out string sourceTag, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId)
		{
			matches = new List<CrewAssignment>();
			sourceTag = "none";
			comparisonNodeIds = new List<NodeID>();
			comparisonNodeId = NodeID.INVALID;
			if (humanCrew == null
				|| building == null
				|| !TryGetScopeInteractionNodeIds(building, out _, out comparisonNodeIds, out comparisonNodeId)
				|| comparisonNodeIds == null)
			{
				return false;
			}

			comparisonNodeIds = comparisonNodeIds
				.Where(candidate => candidate.IsValid)
				.Distinct()
				.ToList();
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			EntityID selectedVehicleId = EntityID.INVALID;
			NodeID previewNodeId = NodeID.INVALID;
			string previewSource = "none";
			bool hasSelectedVehicle = MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out selectedVehicleId, out _)
				&& selectedVehicleId.IsValid;
			bool hasPreviewNode = hasSelectedVehicle
				&& MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out previewNodeId, out previewSource, "scope-building")
				&& previewNodeId.IsValid;
			bool selectedVehicleHasActiveDestination = hasSelectedVehicle
				&& (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(selectedVehicleId)
					|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(selectedVehicleId)
					|| MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(selectedVehicleId, out _, out NodeID selectedExpectedNodeId, out NodeID selectedGoalNodeId)
						&& (selectedExpectedNodeId.IsValid || selectedGoalNodeId.IsValid));
			string comparisonNodeText = string.Join(",", comparisonNodeIds.Select(candidate => candidate.ToString()));
			bool ownedInteractionBuilding = OwnedBuildingInteractionSelectionPatch.IsOwnedInteractionBuilding(building);
			bool humanSafehouseBuilding = OwnedBuildingInteractionSelectionPatch.IsHumanSafehouseInteractionBuilding(building);
			if (humanSafehouseBuilding)
			{
				if (OwnedBuildingInteractionSelectionPatch.TryResolveOwnedBuildingVehicleMatches(humanCrew, building, out List<CrewAssignment> safehouseMatches, allowAnyHumanVehicle: false, requireLiveVehicleNode: true)
					&& safehouseMatches.Count > 0)
				{
					matches = safehouseMatches;
					sourceTag = "safehouse-live";
					GameplayTweaksPlugin.VerificationLog(
						"ScopeOut",
						$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=true source={sourceTag} safehouseLiveOnly=true");
					return true;
				}

				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=false reason=safehouse-live-only activeDestination={selectedVehicleHasActiveDestination}");
				return false;
			}
			if (ownedInteractionBuilding)
			{
				if (OwnedBuildingInteractionSelectionPatch.TryResolveOwnedBuildingVehicleMatches(humanCrew, building, out List<CrewAssignment> ownedLiveMatches, allowAnyHumanVehicle: false, requireLiveVehicleNode: true)
					&& ownedLiveMatches.Count > 0)
				{
					matches = ownedLiveMatches;
					sourceTag = "owned-building-live";
					GameplayTweaksPlugin.VerificationLog(
						"ScopeOut",
						$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=true source={sourceTag} ownedLiveOnly=true");
					return true;
				}

				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=false reason=owned-building-live-only activeDestination={selectedVehicleHasActiveDestination}");
				return false;
			}
			if (hasSelectedVehicle && hasPreviewNode)
			{
				if (ownedInteractionBuilding
					&& OwnedBuildingInteractionSelectionPatch.TryMatchOwnedBuildingPreviewNode(building, previewNodeId, comparisonNodeIds, out NodeID matchedOwnedPreviewNodeId))
				{
					if (OwnedBuildingInteractionSelectionPatch.TryGetSelectedVehicleDriverInteractionMatch(humanCrew, selectedVehicleId, out CrewAssignment ownedDriverMatch))
					{
						matches.Add(ownedDriverMatch);
						sourceTag = previewSource;
						GameplayTweaksPlugin.VerificationLog(
							"ScopeOut",
							$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} matchedNode={matchedOwnedPreviewNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=true source={sourceTag} clusterMatched=true ownedDriverOnly=true");
						return true;
					}

					GameplayTweaksPlugin.VerificationLog(
						"ScopeOut",
						$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} matchedNode={matchedOwnedPreviewNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=false reason=no-owned-driver-match clusterMatched=true");
				}
				else if (!TryMatchScopePreviewNodeToBuilding(building, previewNodeId, out List<NodeID> previewComparisonNodeIds, out _, out NodeID matchedPreviewNodeId, out bool clusterMatched))
				{
					_ = matchedPreviewNodeId;
					_ = clusterMatched;
				}
				else
				{
					if (MultiCrewVehicleHelper.TryGetHumanCrewUsableForScopePreviewAtBuilding(humanCrew, building, out matches, out sourceTag)
						&& matches.Count > 0)
					{
						GameplayTweaksPlugin.VerificationLog(
							"ScopeOut",
							$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} matchedNode={matchedPreviewNodeId} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={string.Join(",", previewComparisonNodeIds.Select(candidate => candidate.ToString()))} accepted=true source={sourceTag} clusterMatched={clusterMatched}");
						return true;
					}

					_ = previewComparisonNodeIds;
				}

				if (selectedVehicleHasActiveDestination && !ownedInteractionBuilding)
				{
					return false;
				}
			}

			foreach (NodeID comparisonNode in comparisonNodeIds)
			{
				if (!MultiCrewVehicleHelper.TryGetHumanCrewUsableForMapScopeAtNode(humanCrew, comparisonNode, out List<CrewAssignment> comparisonMatches, out string comparisonSourceTag)
					|| comparisonMatches.Count <= 0)
				{
					continue;
				}
				comparisonMatches = FilterSafehouseIdleCrewForBuilding(building, comparisonNodeIds, comparisonMatches, comparisonSourceTag);
				if (comparisonMatches.Count <= 0)
				{
					continue;
				}

				matches = comparisonMatches;
				sourceTag = comparisonSourceTag;
				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-building-node-match building={building.Id.id} requestedNode={comparisonNodeId} matchedNode={comparisonNode} previewNode={previewNodeId} previewSource={previewSource} comparisonNodes={comparisonNodeText} accepted=true source={sourceTag}");
				return true;
			}

			return false;
		}

		private static bool TryResolveScopeNode(CrewAssignment crew, Entity building, out Node node)
		{
			node = null;
			if (TryResolveScopeBuildingNode(building, out node))
				return true;
			if (!crew.IsValid)
				return false;
			if (crew.IsInVehicle)
			{
				if (TryResolveCrewScopePreviewNode(crew, out node))
				{
					return true;
				}
				return MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNode(crew.VehicleID, out node, out _, "crewhud") && node != null;
			}
			Entity peep = crew.GetPeep();
			if (peep == null)
				return false;
			node = HumanCommandValidator.GetNode(peep);
			return node != null;
		}

		internal static bool TryResolveCrewLiveNode(CrewAssignment crew, out Node node)
		{
			node = null;
			if (!crew.IsValid)
				return false;
			if (crew.IsInVehicle)
			{
				if (TryResolveCrewScopePreviewNode(crew, out node))
				{
					return true;
				}
				return MultiCrewVehicleHelper.TryGetActualHumanVehicleInteractionNode(crew.VehicleID, out node, out _, "crewhud") && node != null;
			}
			Entity peep = crew.GetPeep();
			if (peep == null)
				return false;
			node = HumanCommandValidator.GetNode(peep);
			return node != null;
		}

		private static bool TryResolveCrewScopePreviewNode(CrewAssignment crew, out Node node)
		{
			node = null;
			if (!crew.IsValid || !crew.IsInVehicle || !crew.VehicleID.IsValid)
			{
				return false;
			}

			return MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNode(crew.VehicleID, out node, out _, "crewhud-scope")
				&& node != null;
		}

		internal static bool TryMatchScopePreviewNodeToBuilding(Entity building, NodeID previewNodeId, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId, out NodeID matchedNodeId, out bool clusterMatched)
		{
			comparisonNodeIds = new List<NodeID>();
			comparisonNodeId = NodeID.INVALID;
			matchedNodeId = NodeID.INVALID;
			clusterMatched = false;
			if (building == null || !previewNodeId.IsValid)
			{
				return false;
			}

			if (!TryGetScopeInteractionNodeIds(building, out _, out comparisonNodeIds, out comparisonNodeId) || comparisonNodeIds == null)
			{
				return false;
			}

			comparisonNodeIds = comparisonNodeIds
				.Where(nodeId => nodeId.IsValid)
				.Distinct()
				.ToList();
			if (comparisonNodeIds.Count <= 0)
			{
				return false;
			}

			if (comparisonNodeIds.Contains(previewNodeId))
			{
				matchedNodeId = previewNodeId;
				return true;
			}

			WorldPos buildingWorldPos = building.data?.board?.worldpos ?? WorldPos.Zero;
			if (!MultiCrewVehicleHelper.TryResolveScopeFrontageClusterMatch(previewNodeId, comparisonNodeIds, buildingWorldPos, out NodeID clusterMatchNodeId, out List<NodeID> clusteredNodeIds)
				|| !clusterMatchNodeId.IsValid)
			{
				return false;
			}

			clusterMatched = true;
			matchedNodeId = clusterMatchNodeId;
			if (clusteredNodeIds != null && clusteredNodeIds.Count > 0)
			{
				comparisonNodeIds = clusteredNodeIds
					.Where(nodeId => nodeId.IsValid)
					.Distinct()
					.ToList();
			}

			return true;
		}

		private static bool TryValidateScopeActor(CrewAssignment crew, out string error)
		{
			error = null;
			if (!crew.IsValid || !crew.IsInVehicle)
			{
				return true;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (humanCrew == null)
			{
				error = DriverOnlyText;
				return false;
			}
			if (!MultiCrewVehicleHelper.IsDriver(humanCrew, crew))
			{
				GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-driver-required crew={crew.peepId.id} vehicle={crew.VehicleID.id}");
				error = DriverOnlyText;
				return false;
			}

			return true;
		}

		private static bool TryExecutePreviewScopeOut(CrewAssignment crew, Entity building)
		{
			try
			{
				if (!crew.IsValid
					|| !crew.IsInVehicle
					|| !crew.VehicleID.IsValid
					|| building == null
					|| !building.Id.IsValid)
				{
					return false;
				}

				if (!MultiCrewVehicleHelper.IsHumanVehicleTravelActive(crew.VehicleID)
					&& !MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(crew.VehicleID))
				{
					return false;
				}

				bool hasPreviewNode = MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(crew.VehicleID, out NodeID resolvedNodeId, out string resolvedSource, "preview-scope-direct")
					&& resolvedNodeId.IsValid
					&& !string.IsNullOrWhiteSpace(resolvedSource)
					&& IsDirectPreviewScopeSource(resolvedSource)
					&& TryMatchScopePreviewNodeToBuilding(building, resolvedNodeId, out _, out _, out _, out _);
				if (!hasPreviewNode
					&& TryResolveAuthorizedScopeStatusNode(G.GetHumanCrew(), crew, building, out Node authorizedNode, out string authorizedSource))
				{
					resolvedNodeId = authorizedNode.id;
					resolvedSource = authorizedSource;
					hasPreviewNode = resolvedNodeId.IsValid;
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"preview-scope-direct-authorized vehicle={crew.VehicleID.id} crew={crew.peepId.id} building={building.Id.id} node={resolvedNodeId} source={resolvedSource}");
				}
				if (!hasPreviewNode)
				{
					return false;
				}

				if (!MultiCrewVehicleHelper.IsHumanVehiclePreviewNodeKnownOrReached(crew.VehicleID, resolvedNodeId)
					&& !MultiCrewVehicleHelper.IsHumanVehicleFinalGoalScopePreviewAllowed(crew.VehicleID, resolvedNodeId, resolvedSource))
				{
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"preview-scope-direct-blocked vehicle={crew.VehicleID.id} crew={crew.peepId.id} building={building.Id.id} node={resolvedNodeId} source={resolvedSource} reason=unknown-not-arrived");
					return false;
				}

				Entity peep = crew.GetPeep();
				AgentComponent agent = peep?.components?.agent;
				if (agent == null)
				{
					return false;
				}

				CrewCost cost = global::Game.Game.serv.globals.settings.people.social.costs.scopeOutCost;
				if (!agent.CanPay(cost))
				{
					return false;
				}

				if (!agent.DoPay(cost, "PreviewScopeOut"))
				{
					return false;
				}

				G.GetHumanPlayer()?.territory?.ScopeOutBuildingWithFeedback(building, crew.peepId);
				RefreshScopedBuildingPicks(building, crew.VehicleID, "preview-scope-direct");
				GameplayTweaksPlugin.VerificationLog("ScopeOut", $"preview-scope-direct vehicle={crew.VehicleID.id} crew={crew.peepId.id} building={building.Id.id} node={resolvedNodeId} source={resolvedSource}");
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] TryExecutePreviewScopeOut: " + ex.Message);
				return false;
			}
		}

		internal static void RefreshScopedBuildingPicks(Entity building, EntityID vehicleId, string sourceTag)
		{
			try
			{
				Node scopedNode = null;
				if (!TryResolveScopeBuildingNode(building, out scopedNode) || scopedNode == null || !scopedNode.id.IsValid)
				{
					return;
				}

				Node refreshFinalNode = scopedNode;
				if (vehicleId.IsValid
					&& MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNode(vehicleId, out Node previewNode, out string previewSource, "scope-feedback-refresh")
					&& previewNode != null
					&& previewNode.id.IsValid
					&& TryMatchScopePreviewNodeToBuilding(building, previewNode.id, out _, out _, out _, out _))
				{
					refreshFinalNode = previewNode;
					GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-feedback-preview-refresh vehicle={vehicleId.id} building={building?.Id.id ?? 0UL} scopedNode={scopedNode.id} refreshNode={refreshFinalNode.id} source={previewSource}");
				}

				bool transientScopeFeedback = sourceTag.StartsWith("scope-feedback-transient", StringComparison.Ordinal);
				EntityID refreshVehicleId = vehicleId;
				GameplayTweaksPlugin.RefreshBuildingPickStateAfterHumanVehicleTravel(scopedNode, refreshFinalNode, sourceTag, refreshVehicleId);
				GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-feedback-refresh vehicle={vehicleId.id} refreshVehicle={(refreshVehicleId.IsValid ? refreshVehicleId.id : 0UL)} building={building?.Id.id ?? 0UL} node={scopedNode.id} refreshNode={refreshFinalNode.id} source={sourceTag} transient={transientScopeFeedback}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] RefreshScopedBuildingPicks: " + ex.Message);
			}
		}

		internal static bool TryResolveScopeBuildingNode(Entity building, out Node node)
		{
			node = null;
			if (!TryResolveScopeBuildingBoardNodeId(building, out NodeID nodeId))
			{
				return false;
			}
			node = nodeId.FindNode();
			return node != null;
		}

		private static bool TryResolveScopeBuildingBoardNodeId(Entity building, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			if (building?.components?.board == null)
			{
				return false;
			}

			try
			{
				MethodInfo getNodeId = building.components.board.GetType().GetMethod("GetNodeID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (getNodeId != null)
				{
					object value = getNodeId.Invoke(building.components.board, null);
					if (value is NodeID resolvedNodeId && resolvedNodeId.IsValid)
					{
						nodeId = resolvedNodeId;
						return true;
					}
				}
			}
			catch
			{
			}

			NodeID beadNodeId = building.data?.board?.bead.nodeId ?? NodeID.INVALID;
			if (beadNodeId.IsValid)
			{
				nodeId = beadNodeId;
				return true;
			}

			return false;
		}

		internal static bool TryGetScopeComparisonNodeIds(Entity building, out Node node, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId)
		{
			return TryGetScopeDisplayComparisonNodeIds(building, out node, out comparisonNodeIds, out comparisonNodeId);
		}

		internal static bool TryGetScopeDisplayComparisonNodeIds(Entity building, out Node node, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId)
		{
			node = null;
			comparisonNodeIds = new List<NodeID>();
			comparisonNodeId = NodeID.INVALID;
			if (building == null)
			{
				LogScopeNodeResolution("display", building, NodeID.INVALID, NodeID.INVALID, NodeID.INVALID, NodeID.INVALID, NodeID.INVALID, comparisonNodeIds, comparisonNodeId, "building-null");
				return false;
			}

			NodeID boardNodeId = NodeID.INVALID;
			if (TryResolveScopeBuildingBoardNodeId(building, out boardNodeId) && boardNodeId.IsValid)
			{
				node = boardNodeId.FindNode();
				AddComparisonNode(comparisonNodeIds, boardNodeId);
			}

			Node snappedNode = null;
			WorldPos worldPos = building.data?.board?.worldpos ?? WorldPos.Zero;
			if (!worldPos.IsZero && global::Game.Game.ctx?.board?.nodes != null)
			{
				snappedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(worldPos, 5f);
				if (snappedNode != null)
				{
					AddComparisonNode(comparisonNodeIds, snappedNode.id);
					if (node == null)
					{
						node = snappedNode;
					}
				}
			}

			NodeID beadNodeId = building.data?.board?.bead.nodeId ?? NodeID.INVALID;
			if (beadNodeId.IsValid)
			{
				AddComparisonNode(comparisonNodeIds, beadNodeId);
				if (node == null)
				{
					node = beadNodeId.FindNode();
				}
			}

			NodeEdge buildingEdge = TryResolveScopeBuildingEdge(building);
			if (buildingEdge != null)
			{
				AddComparisonNode(comparisonNodeIds, buildingEdge.a);
				AddComparisonNode(comparisonNodeIds, buildingEdge.b);
				if (node == null)
				{
					node = buildingEdge.a.FindNode() ?? buildingEdge.b.FindNode();
				}
			}

			if (comparisonNodeIds.Count <= 0)
			{
				LogScopeNodeResolution("display", building, boardNodeId, beadNodeId, snappedNode?.id ?? NodeID.INVALID, buildingEdge?.a ?? NodeID.INVALID, buildingEdge?.b ?? NodeID.INVALID, comparisonNodeIds, comparisonNodeId, "no-comparison-nodes");
				return false;
			}

			TryAddSelectedVehiclePreviewComparisonNode(building, comparisonNodeIds);

			comparisonNodeId = comparisonNodeIds[0];
			LogScopeNodeResolution("display", building, boardNodeId, beadNodeId, snappedNode?.id ?? NodeID.INVALID, buildingEdge?.a ?? NodeID.INVALID, buildingEdge?.b ?? NodeID.INVALID, comparisonNodeIds, comparisonNodeId, "resolved");
			return true;
		}

		internal static bool TryGetScopeInteractionNodeIds(Entity building, out Node node, out List<NodeID> comparisonNodeIds, out NodeID comparisonNodeId)
		{
			node = null;
			comparisonNodeIds = new List<NodeID>();
			comparisonNodeId = NodeID.INVALID;
			if (building == null)
			{
				LogScopeNodeResolution("interaction", building, NodeID.INVALID, NodeID.INVALID, NodeID.INVALID, NodeID.INVALID, NodeID.INVALID, comparisonNodeIds, comparisonNodeId, "building-null");
				return false;
			}

			NodeID boardNodeId = NodeID.INVALID;
			if (TryResolveScopeBuildingBoardNodeId(building, out boardNodeId) && boardNodeId.IsValid)
			{
				node = boardNodeId.FindNode();
				AddComparisonNode(comparisonNodeIds, boardNodeId);
			}

			NodeID beadNodeId = building.data?.board?.bead.nodeId ?? NodeID.INVALID;
			if (beadNodeId.IsValid)
			{
				AddComparisonNode(comparisonNodeIds, beadNodeId);
				if (node == null)
				{
					node = beadNodeId.FindNode();
				}
			}

			Node snappedNode = null;
			WorldPos worldPos = building.data?.board?.worldpos ?? WorldPos.Zero;
			if (comparisonNodeIds.Count <= 0 && !worldPos.IsZero && global::Game.Game.ctx?.board?.nodes != null)
			{
				snappedNode = global::Game.Game.ctx.board.nodes.FindNearestNodeAround(worldPos, 5f);
				if (snappedNode != null)
				{
					node = snappedNode;
					AddComparisonNode(comparisonNodeIds, snappedNode.id);
				}
			}

			NodeEdge buildingEdge = TryResolveScopeBuildingEdge(building);
			if (comparisonNodeIds.Count <= 0 && buildingEdge != null)
			{
				AddComparisonNode(comparisonNodeIds, buildingEdge.a);
				AddComparisonNode(comparisonNodeIds, buildingEdge.b);
				if (node == null)
				{
					node = buildingEdge.a.FindNode() ?? buildingEdge.b.FindNode();
				}
			}

			if (comparisonNodeIds.Count <= 0)
			{
				LogScopeNodeResolution("interaction", building, boardNodeId, beadNodeId, snappedNode?.id ?? NodeID.INVALID, buildingEdge?.a ?? NodeID.INVALID, buildingEdge?.b ?? NodeID.INVALID, comparisonNodeIds, comparisonNodeId, "no-comparison-nodes");
				return false;
			}

			if (node == null && comparisonNodeIds[0].IsValid)
			{
				node = comparisonNodeIds[0].FindNode();
			}

			comparisonNodeId = node?.id.IsValid == true ? node.id : comparisonNodeIds[0];
			LogScopeNodeResolution("interaction", building, boardNodeId, beadNodeId, snappedNode?.id ?? NodeID.INVALID, buildingEdge?.a ?? NodeID.INVALID, buildingEdge?.b ?? NodeID.INVALID, comparisonNodeIds, comparisonNodeId, "resolved");
			return true;
		}

		private static void TryAddSelectedVehiclePreviewComparisonNode(Entity building, List<NodeID> comparisonNodeIds)
		{
			if (building == null || comparisonNodeIds == null || comparisonNodeIds.Count <= 0)
			{
				return;
			}

			PlayerCrew humanCrew = G.GetHumanCrew();
			if (!MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(humanCrew, out EntityID selectedVehicleId, out _)
				|| !selectedVehicleId.IsValid
				|| !MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out NodeID previewNodeId, out _, "scope-compare")
				|| !previewNodeId.IsValid)
			{
				return;
			}

			if (TryMatchScopePreviewNodeToBuilding(building, previewNodeId, out List<NodeID> matchedComparisonNodeIds, out NodeID comparisonNodeId, out NodeID matchedNodeId, out bool clusterMatched))
			{
				MultiCrewVehicleHelper.LogVehicleAuthority("scope-preview-edge-normalized", $"{selectedVehicleId.id}:{comparisonNodeId}:{matchedNodeId}:{building.Id.id}", $"scope-preview-edge-normalized vehicle={selectedVehicleId.id} previewNode={previewNodeId} matchedNode={matchedNodeId} normalizedNodes={string.Join(",", matchedComparisonNodeIds.Select(nodeId => nodeId.ToString()))} building={building.Id.id} clusterMatched={clusterMatched}", dedupe: false);
				return;
			}

			MultiCrewVehicleHelper.LogVehicleAuthority("scope-preview-edge-blocked", $"{selectedVehicleId.id}:{previewNodeId}:{building.Id.id}", $"scope-preview-edge-blocked vehicle={selectedVehicleId.id} previewNode={previewNodeId} normalizedNodes={string.Join(",", comparisonNodeIds.Select(nodeId => nodeId.ToString()))} building={building.Id.id}", dedupe: false);
		}

		private static void AddComparisonNode(List<NodeID> comparisonNodeIds, NodeID nodeId)
		{
			if (!nodeId.IsValid || comparisonNodeIds == null || comparisonNodeIds.Contains(nodeId))
			{
				return;
			}

			comparisonNodeIds.Add(nodeId);
		}

		private static void LogScopeNodeResolution(string mode, Entity building, NodeID boardNodeId, NodeID beadNodeId, NodeID snappedNodeId, NodeID edgeA, NodeID edgeB, IEnumerable<NodeID> comparisonNodeIds, NodeID primaryNodeId, string reason)
		{
			try
			{
				string comparisonNodeText = comparisonNodeIds == null
					? string.Empty
					: string.Join(",", comparisonNodeIds.Where(candidate => candidate.IsValid).Select(candidate => candidate.ToString()));
				GameplayTweaksPlugin.VerificationLog(
					"ScopeOut",
					$"scope-node-resolve mode={mode} building={building?.Id.id ?? 0UL} boardNode={boardNodeId} beadNode={beadNodeId} snappedNode={snappedNodeId} edgeA={edgeA} edgeB={edgeB} primaryNode={primaryNodeId} comparisonNodes={comparisonNodeText} reason={reason}");
			}
			catch
			{
			}
		}

		private static NodeEdge TryResolveScopeBuildingEdge(Entity building)
		{
			if (building?.data?.board == null)
			{
				return null;
			}

			try
			{
				NodeEdgeID edgeId = building.data.board.bead.edgeId;
				return edgeId.IsValid ? edgeId.FindEdge() : null;
			}
			catch
			{
				return null;
			}
		}
	}

	internal static class CommandScopeOutGuardPatch
	{
		private static bool TryGetVehicleScopePreviewNode(CommandScopeOut command, out CrewAssignment crew, out Node previewNode)
		{
			crew = CrewAssignment.EMPTY;
			previewNode = null;
			PlayerCrew humanCrew = G.GetHumanCrew();
			if (command == null || humanCrew == null)
			{
				return false;
			}

			crew = humanCrew.GetCrewForPeep(command.peepId);
			if (!crew.IsValid || !crew.IsInVehicle || !crew.VehicleID.IsValid)
			{
				return false;
			}

			return MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNode(crew.VehicleID, out previewNode, out _, "scope-command")
				&& previewNode != null;
		}

		private static bool IsBuildingOnPreviewNode(Entity building, Node previewNode)
		{
			if (building == null || previewNode == null || !previewNode.id.IsValid)
			{
				return false;
			}

			return CommandButtonScopeOutPatch.TryMatchScopePreviewNodeToBuilding(building, previewNode.id, out _, out _, out _, out _);
		}

		private static EntityID FindPreviewScopeCandidate(Node previewNode, PlayerID pid, PlayerTerritory territory)
		{
			if (previewNode == null || territory == null)
			{
				return EntityID.INVALID;
			}

			List<EntityID> candidates = previewNode.MakeListOfScopeOutCandidates(pid);
			EntityID frontageClusterFallback = EntityID.INVALID;
			for (int index = candidates.Count - 1; index >= 0; index--)
			{
				EntityID candidateId = candidates[index];
				Entity candidate = candidateId.FindEntity();
				if (!candidateId.IsValid || candidate == null || territory.IsScoped(candidate) || territory.ScopeOutReserved(candidateId))
				{
					continue;
				}
				if (!CommandButtonScopeOutPatch.TryMatchScopePreviewNodeToBuilding(candidate, previewNode.id, out _, out _, out _, out bool clusterMatched))
				{
					continue;
				}

				if (!clusterMatched)
				{
					return candidateId;
				}

				if (!frontageClusterFallback.IsValid)
				{
					frontageClusterFallback = candidateId;
				}
			}

			return frontageClusterFallback;
		}

		[HarmonyPostfix]
		internal static void OnStartedPostfix(CommandScopeOut __instance)
		{
			try
			{
				if (__instance == null || !__instance.pid.IsHumanPlayer)
					return;
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer?.territory == null)
					return;

				if (TryGetVehicleScopePreviewNode(__instance, out _, out Node previewNode) && previewNode != null)
				{
					Entity currentBuilding = __instance.next.FindEntity();
					bool targetMatchesPreview = __instance.next.IsValid && IsBuildingOnPreviewNode(currentBuilding, previewNode);
					if (!targetMatchesPreview)
					{
						EntityID previousTarget = __instance.next;
						if (previousTarget.IsValid && humanPlayer.territory.ScopeOutReserved(previousTarget))
						{
							humanPlayer.territory.UnreserveScopeOut(previousTarget);
						}

						if (previousTarget.IsValid)
						{
							__instance.next = EntityID.INVALID;
							GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-command-preview-cancel crew={__instance.peepId.id} priorBuilding={previousTarget.id} previewNode={previewNode.id} reason=preview-target-mismatch");
							return;
						}

						EntityID previewCandidate = FindPreviewScopeCandidate(previewNode, __instance.pid, humanPlayer.territory);
						if (previewCandidate.IsValid)
						{
							__instance.next = previewCandidate;
							humanPlayer.territory.ReserveScopeOut(previewCandidate);
							GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-command-preview-remap crew={__instance.peepId.id} vehicle={(previewNode?.id.IsValid == true ? 1 : 0)} priorBuilding={previousTarget.id} replacementBuilding={previewCandidate.id} previewNode={previewNode.id}");
						}
						else
						{
							__instance.next = EntityID.INVALID;
							GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-command-preview-cancel crew={__instance.peepId.id} priorBuilding={previousTarget.id} previewNode={previewNode.id} reason=no-preview-candidate");
							return;
						}
					}
				}

				if (__instance.next.IsNotValid)
				{
					return;
				}

				Entity building = __instance.next.FindEntity();
				if (building == null || !humanPlayer.territory.IsScoped(building))
				{
					return;
				}

				if (humanPlayer.territory.ScopeOutReserved(__instance.next))
				{
					humanPlayer.territory.UnreserveScopeOut(__instance.next);
				}
				__instance.next = EntityID.INVALID;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] CommandScopeOutGuardPatch: " + ex.Message);
			}
		}
	}

	internal static class ScopeOutFeedbackSelectionPatch
	{
		private static bool IsTransientVehicleScopeFeedbackSource(EntityID vehicleId, string source)
		{
			if (!string.IsNullOrEmpty(source)
				&& (source.StartsWith("selected-final-goal", StringComparison.Ordinal)
					|| source.StartsWith("route-final-goal", StringComparison.Ordinal)
					|| string.Equals(source, "selected-final-goal-memory", StringComparison.Ordinal)))
			{
				return true;
			}

			if (string.Equals(source, "mobile", StringComparison.Ordinal))
			{
				bool activeTravel = vehicleId.IsValid && MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId);
				bool queuedResume = vehicleId.IsValid && MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId);
				return activeTravel || queuedResume;
			}

			return string.Equals(source, "recent-finalize", StringComparison.Ordinal)
				|| string.Equals(source, "authoritative", StringComparison.Ordinal);
		}

		private static bool TryGetSelectedVehicleScopeFeedbackNodeId(EntityID selectedVehicleId, out NodeID nodeId, out string source)
		{
			nodeId = NodeID.INVALID;
			source = "none";
			if (!selectedVehicleId.IsValid)
			{
				return false;
			}

			if (MultiCrewVehicleHelper.TryGetHumanVehicleScopePreviewNodeId(selectedVehicleId, out nodeId, out source, "scope-feedback")
				&& nodeId.IsValid)
			{
				return true;
			}

			if (MultiCrewVehicleHelper.TryGetBuildingHumanVehicleInteractiveNodeId(selectedVehicleId, out nodeId, out source)
				&& nodeId.IsValid)
			{
				return true;
			}

			if (GameplayTweaksPlugin.TryGetSelectedVehicleUiFinalNode(selectedVehicleId, out nodeId)
				&& nodeId.IsValid)
			{
				source = "selected-final-goal-memory";
				return true;
			}

			return false;
		}

		[HarmonyPostfix]
		internal static void Postfix(PlayerTerritory __instance, Entity building)
		{
			try
			{
				if (__instance == null || building == null)
					return;
				PlayerInfo humanPlayer = G.GetHumanPlayer();
				if (humanPlayer == null || humanPlayer.territory != __instance)
					return;
				SelectionManager selection = global::Game.Game.ctx?.selection;
				Entity currentActive = selection?.CurrentActive;
				if (currentActive == null)
					return;
				Node scopedNode = TryGetBoardNode(building);
				Node activeNode = TryGetBoardNode(currentActive);
				bool hasSelectedVehicle = MultiCrewVehicleHelper.TryGetSelectedHumanVehicleForMapScope(G.GetHumanCrew(), out EntityID selectedVehicleId, out _)
					&& selectedVehicleId.IsValid;
				bool preserveSameNodeSelection = false;
				string refreshSourceTag = "scope-feedback-transient";
				if (hasSelectedVehicle
					&& scopedNode != null
					&& TryGetSelectedVehicleScopeFeedbackNodeId(selectedVehicleId, out NodeID selectedNodeId, out string selectedSource)
					&& selectedNodeId.IsValid
					&& selectedNodeId == scopedNode.id)
				{
					bool transientSelectionSource = IsTransientVehicleScopeFeedbackSource(selectedVehicleId, selectedSource);
					refreshSourceTag = transientSelectionSource ? "scope-feedback-transient" : "scope-feedback-stable";
					if (!transientSelectionSource)
					{
						preserveSameNodeSelection = true;
						GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-feedback-selection-preserved vehicle={selectedVehicleId.id} node={selectedNodeId} source={selectedSource} building={building?.Id.id ?? 0UL}");
						Entity selectedVehicle = selectedVehicleId.FindEntity();
						if (selection != null && selectedVehicle != null && currentActive != selectedVehicle)
						{
							selection.HandleDeselect();
							selection.SetActive(selectedVehicle);
							GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-feedback-vehicle-reactivated vehicle={selectedVehicleId.id} node={selectedNodeId} priorActive={currentActive?.Id.id ?? 0UL}");
						}
					}
					else
					{
						GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-feedback-selection-skipped vehicle={selectedVehicleId.id} node={selectedNodeId} source={selectedSource} building={building?.Id.id ?? 0UL}");
					}
					if (transientSelectionSource)
					{
						CommandButtonScopeOutPatch.RefreshScopedBuildingPicks(building, selectedVehicleId, refreshSourceTag);
					}
					else
					{
						GameplayTweaksPlugin.VerificationLog("ScopeOut", $"scope-feedback-refresh-skipped vehicle={selectedVehicleId.id} node={selectedNodeId} source={refreshSourceTag} building={building?.Id.id ?? 0UL}");
					}
				}
				if (!preserveSameNodeSelection && currentActive == building)
				{
					selection.HandleDeselect();
				}
				if (!preserveSameNodeSelection && scopedNode != null && activeNode != null && scopedNode.id == activeNode.id)
				{
					selection.HandleDeselect();
				}
				if (!preserveSameNodeSelection)
				{
					HideCornerInfoForNode(scopedNode);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ScopeOutFeedbackSelectionPatch: " + ex.Message);
			}
		}

		private static Node TryGetBoardNode(Entity entity)
		{
			if (entity?.components?.board == null)
				return null;
			try
			{
				MethodInfo getNodeId = entity.components.board.GetType().GetMethod("GetNodeID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (getNodeId == null)
					return null;
				return ((NodeID)getNodeId.Invoke(entity.components.board, null)).FindNode();
			}
			catch
			{
				return null;
			}
		}

		private static void HideCornerInfoForNode(Node scopedNode)
		{
			if (scopedNode == null)
				return;
			try
			{
				CornerInfoDialog cornerInfo = global::Game.Game.ctx?.hud?.cornerInfo;
				if (cornerInfo == null || !cornerInfo.IsShowing)
					return;
				FieldInfo nodeField = typeof(CornerInfoDialog).GetField("_node", BindingFlags.Instance | BindingFlags.NonPublic);
				Node dialogNode = nodeField?.GetValue(cornerInfo) as Node;
				if (dialogNode != null && dialogNode.id == scopedNode.id)
					cornerInfo.Hide();
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] ScopeOutFeedbackSelectionPatch.HideCornerInfoForNode: " + ex.Message);
			}
		}
	}

	/// <summary>Prevent GrantVehicle.Apply from throwing when ctx.visit is null or GetCrewNode() returns null (e.g. during starter skill / setup).</summary>
	internal static class GrantVehicleApplyGuardPatch
	{
		[HarmonyPrefix]
		internal static bool Prefix(GrantVehicle __instance, GrantContext ctx)
		{
			try
			{
				if (ctx.visit != null && ctx.visit.GetCrewNode() != null)
					return true;

				PlayerInfo player = ctx.GetPlayer();
				if (player?.crew == null)
				{
					Debug.LogWarning("[GameplayTweaks] GrantVehicle.Apply skipped: player/crew unavailable.");
					return false;
				}

				Node fallbackNode = null;
				if (ctx.visit?.peep?.data?.agent?.nid.IsValid == true)
					fallbackNode = ctx.visit.peep.data.agent.nid.FindNode();
				if (fallbackNode == null)
					fallbackNode = player.territory?.GetHeadquartersNode();
				if (fallbackNode == null)
				{
					Debug.LogWarning("[GameplayTweaks] GrantVehicle.Apply skipped: no fallback node available.");
					return false;
				}

				Entity vehicle = player.crew.CreateAndTrackVehicle(__instance.id, fallbackNode.pos);
				if (ctx.pid.IsHumanPlayer)
					PlayerMeetings.RevealAllUnitsOfPlayer(ctx.pid);
				if (vehicle != null)
				{
					var healthFraction = SomaSim.Util.Fixnum.Clamp(__instance.healthfraction, 0, 1);
					var maxHealth = vehicle.components.mobile.MaxHealth();
					vehicle.components.mobile.SetHealth(CrewAssignment.EMPTY, healthFraction * maxHealth);
					if (ctx.visit != null)
						ctx.visit.vehicle = vehicle;
				}

				Debug.LogWarning("[GameplayTweaks] GrantVehicle.Apply used fallback node.");
				return false;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] GrantVehicle.Apply guard failed: " + ex.Message);
				return false;
			}
		}
	}

}
