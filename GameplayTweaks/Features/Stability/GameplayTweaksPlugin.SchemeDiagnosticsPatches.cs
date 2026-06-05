using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static class SchemeVehicleDiagnosticsPatch
	{
		private sealed class SchemeVehicleSnapshot
		{
			public string Source;
			public string CommandType;
			public PlayerID Pid;
			public EntityID PeepId;
			public EntityID VehicleId;
			public NodeID PeepNodeId;
			public NodeID VehicleNodeId;
			public NodeID GoalNodeId;
			public string VehicleNodeSource;
			public string VehicleTemplate;
			public int OccupantCount;
			public int PassengerCount;
			public int SlotCount;
			public EntityID DriverPeepId;
			public bool IsDriver;
			public bool IsInScheme;
			public bool IsOnBoard;
			public bool HasPendingRoute;
			public bool RouteActive;
			public bool RouteQueuedResume;
			public NodeID PendingExpectedNodeId;
			public NodeID PendingGoalNodeId;
			public string Occupants;
			public string SchemeId;
			public string ChapterId;
			public string ScriptId;
			public string RecoveryScriptId;
			public string OffBoardReason;
			public string OffBoardVehicleTemplate;
		}

		private sealed class SchemeVehicleSuspension
		{
			public EntityID PeepId;
			public EntityID VehicleId;
			public EntityID DriverPeepId;
			public NodeID VehicleNodeId;
			public string SchemeId;
			public string ChapterId;
			public string VehicleTemplate;
			public string Occupants;
			public int OccupantCount;
			public bool WasDriver;
		}

		private static readonly Dictionary<ulong, SchemeVehicleSuspension> SuspendedSchemeVehiclesByPeep = new Dictionary<ulong, SchemeVehicleSuspension>();
		private static readonly FieldInfo PlayerCrewDataField = typeof(PlayerCrew).GetField("_crewdata", BindingFlags.Instance | BindingFlags.NonPublic);

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				int patched = 0;

				MethodInfo startScheme = AccessTools.Method(typeof(PlayerScheme), nameof(PlayerScheme.StartSchemeForCrew), new[] { typeof(Label), typeof(Entity) });
				if (startScheme != null)
				{
					harmony.Patch(startScheme, prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(StartSchemeForCrewPrefix)));
					patched++;
				}

				MethodInfo startChapter = AccessTools.Method(typeof(PlayerScheme), nameof(PlayerScheme.StartCurrentChapter), new[] { typeof(Label), typeof(bool) });
				if (startChapter != null)
				{
					harmony.Patch(startChapter, prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(StartCurrentChapterPrefix)));
					patched++;
				}

				MethodInfo decisionPop = AccessTools.Method(typeof(PlayerScheme), nameof(PlayerScheme.OnDecisionPop), new[] { typeof(SchemeData) });
				if (decisionPop != null)
				{
					harmony.Patch(decisionPop, prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(OnDecisionPopPrefix)));
					patched++;
				}

				MethodInfo boardPerform = AccessTools.Method(typeof(CommandBoardCallback), "PerformTurnActions");
				if (boardPerform != null)
				{
					harmony.Patch(
						boardPerform,
						prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(CommandBoardCallbackPerformTurnActionsPrefix)),
						postfix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(CommandBoardCallbackPerformTurnActionsPostfix)));
					patched++;
				}

				MethodInfo gotoPerform = AccessTools.Method(typeof(CommandGoto), "PerformTurnActions");
				if (gotoPerform != null)
				{
					harmony.Patch(
						gotoPerform,
						prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(CommandGotoPerformTurnActionsPrefix)),
						postfix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(CommandGotoPerformTurnActionsPostfix)));
					patched++;
				}

				MethodInfo teleportPerform = AccessTools.Method(typeof(CommandTeleportTo), "PerformTurnActions");
				if (teleportPerform != null)
				{
					harmony.Patch(
						teleportPerform,
						prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(CommandTeleportToPerformTurnActionsPrefix)),
						postfix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(CommandTeleportToPerformTurnActionsPostfix)));
					patched++;
				}

				MethodInfo removeCrew = AccessTools.Method(typeof(PlayerCrew), nameof(PlayerCrew.RemoveCrewFromBoard), new[] { typeof(CrewAssignment), typeof(PlayerCrewData.OffBoardReason), typeof(bool), typeof(int) });
				if (removeCrew != null)
				{
					harmony.Patch(
						removeCrew,
						prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(RemoveCrewFromBoardPrefix)),
						postfix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(RemoveCrewFromBoardPostfix)));
					patched++;
				}

				MethodInfo returnCrew = AccessTools.Method(typeof(PlayerCrew), nameof(PlayerCrew.ReturnCrewToBoard), new[] { typeof(CrewAssignment) });
				if (returnCrew != null)
				{
					harmony.Patch(
						returnCrew,
						prefix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(ReturnCrewToBoardPrefix)),
						postfix: new HarmonyMethod(typeof(SchemeVehicleDiagnosticsPatch), nameof(ReturnCrewToBoardPostfix)));
					patched++;
				}

				VerificationLog("SchemeVehicle", $"diagnostics applied patched={patched}");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicleDiagnosticsPatch failed: " + ex.Message);
			}
		}

		private static bool StartSchemeForCrewPrefix(PlayerScheme __instance, Label schemeID, Entity crew)
		{
			try
			{
				PlayerInfo player = ResolvePlayerForPeep(crew?.Id ?? EntityID.INVALID);
				CrewAssignment assignment = player?.crew?.GetCrewForPeep(crew?.Id ?? EntityID.INVALID) ?? CrewAssignment.EMPTY;
				SchemeVehicleSnapshot snapshot = CaptureSnapshot("scheme-start", player, assignment, NodeID.INVALID, "start");
				snapshot.SchemeId = FormatLabel(schemeID);
				LogSnapshot("scheme-start", snapshot);
				if (TryBlockConcurrentVehicleSchemeStart(player, assignment, schemeID, out string blockReason))
				{
					VerificationLog(
						"SchemeVehicle",
						"scheme-start-blocked"
						+ " peep=" + FormatEntityId(assignment.peepId)
						+ " vehicle=" + FormatEntityId(assignment.VehicleID)
						+ " scheme=" + FormatLabel(schemeID)
						+ " reason=" + blockReason);
					MultiCrewVehicleHelper.ShowHudMessage("That vehicle already has a crew member on a scheme.");
					return false;
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle start diagnostics failed: " + ex.Message);
			}

			return true;
		}

		private static void StartCurrentChapterPrefix(PlayerScheme __instance, Label schemeID, bool forceSucceed)
		{
			try
			{
				SchemeData scheme = __instance?.Data?.GetOngoingSchemeForId(schemeID);
				if (scheme == null)
				{
					return;
				}

				PlayerInfo player = ResolvePlayerForPeep(scheme.crewAssigned);
				CrewAssignment assignment = player?.crew?.GetCrewForPeep(scheme.crewAssigned) ?? CrewAssignment.EMPTY;
				SchemeVehicleSnapshot snapshot = CaptureSnapshot("scheme-chapter-start", player, assignment, ResolveTargetNode(scheme.currentChapter?.scriptTarget ?? EntityID.INVALID), "start");
				FillSchemeDetails(__instance, scheme, snapshot);
				LogSnapshot("scheme-chapter-start", snapshot, "forceSucceed=" + forceSucceed.ToString(CultureInfo.InvariantCulture));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle chapter-start diagnostics failed: " + ex.Message);
			}
		}

		private static void OnDecisionPopPrefix(PlayerScheme __instance, SchemeData scheme)
		{
			try
			{
				if (scheme == null)
				{
					return;
				}

				PlayerInfo player = ResolvePlayerForPeep(scheme.crewAssigned);
				CrewAssignment assignment = player?.crew?.GetCrewForPeep(scheme.crewAssigned) ?? CrewAssignment.EMPTY;
				EntityID target = scheme.overallTarget.IsValid ? scheme.overallTarget : scheme.currentChapter?.scriptTarget ?? EntityID.INVALID;
				SchemeVehicleSnapshot snapshot = CaptureSnapshot("scheme-decision-recovery", player, assignment, ResolveTargetNode(target), "before");
				FillSchemeDetails(__instance, scheme, snapshot);
				LogSnapshot("scheme-decision-recovery", snapshot);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle recovery diagnostics failed: " + ex.Message);
			}
		}

		private static void CommandBoardCallbackPerformTurnActionsPrefix(CommandBoardCallback __instance, out SchemeVehicleSnapshot __state)
		{
			__state = null;
			try
			{
				if (__instance == null || (__instance.type != CommandType.RemoveFromBoard && __instance.type != CommandType.ReturnToBoard))
				{
					return;
				}

				PlayerInfo player = ResolvePlayer(__instance.pid);
				CrewAssignment assignment = player?.crew?.GetCrewForPeep(__instance.targetPeep) ?? CrewAssignment.EMPTY;
				__state = CaptureSnapshot("scheme-board-command", player, assignment, NodeID.INVALID, "before");
				__state.CommandType = __instance.type.ToString();
				TryFillSchemeDetails(player, __instance.targetPeep, __state);
				TryFillOffBoardDetails(player, __instance.targetPeep, __state);
				LogSnapshot(GetBoardCommandTag(__instance.type, "start"), __state);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle board command prefix failed: " + ex.Message);
			}
		}

		private static void CommandBoardCallbackPerformTurnActionsPostfix(CommandBoardCallback __instance, SchemeVehicleSnapshot __state)
		{
			try
			{
				if (__state == null || __instance == null)
				{
					return;
				}

				PlayerInfo player = ResolvePlayer(__instance.pid);
				CrewAssignment assignment = player?.crew?.GetCrewForPeep(__instance.targetPeep) ?? CrewAssignment.EMPTY;
				SchemeVehicleSnapshot after = CaptureSnapshot("scheme-board-command", player, assignment, NodeID.INVALID, "after");
				after.CommandType = __instance.type.ToString();
				CopySchemeDetails(__state, after);
				TryFillOffBoardDetails(player, __instance.targetPeep, after);
				LogSnapshot(GetBoardCommandTag(__instance.type, "finish"), after, "beforeVehicle=" + FormatEntityId(__state.VehicleId));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle board command postfix failed: " + ex.Message);
			}
		}

		private static void CommandGotoPerformTurnActionsPrefix(CommandGoto __instance, out SchemeVehicleSnapshot __state)
		{
			__state = null;
			try
			{
				if (!TryCaptureSchemeCommandSnapshot(__instance?.pid ?? PlayerID.INVALID, __instance?.peepId ?? EntityID.INVALID, __instance?.goalID ?? NodeID.INVALID, "scheme-target-move", "before", out __state))
				{
					return;
				}

				__state.CommandType = "GoTo";
				LogSnapshot("scheme-target-move-start", __state);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle goto prefix failed: " + ex.Message);
			}
		}

		private static void CommandGotoPerformTurnActionsPostfix(CommandGoto __instance, SchemeVehicleSnapshot __state)
		{
			try
			{
				if (__state == null)
				{
					return;
				}

				if (TryCaptureSchemeCommandSnapshot(__instance?.pid ?? PlayerID.INVALID, __instance?.peepId ?? EntityID.INVALID, __instance?.goalID ?? NodeID.INVALID, "scheme-target-move", "after", out SchemeVehicleSnapshot after))
				{
					after.CommandType = "GoTo";
					LogSnapshot("scheme-target-move-finish", after, "beforeNode=" + FormatNodeId(__state.VehicleNodeId));
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle goto postfix failed: " + ex.Message);
			}
		}

		private static void CommandTeleportToPerformTurnActionsPrefix(CommandTeleportTo __instance, out SchemeVehicleSnapshot __state)
		{
			__state = null;
			try
			{
				if (!TryCaptureSchemeCommandSnapshot(__instance?.pid ?? PlayerID.INVALID, __instance?.peepId ?? EntityID.INVALID, __instance?.goalID ?? NodeID.INVALID, "scheme-target-teleport", "before", out __state))
				{
					return;
				}

				__state.CommandType = "TeleportTo";
				LogSnapshot("scheme-target-teleport-start", __state);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle teleport prefix failed: " + ex.Message);
			}
		}

		private static void CommandTeleportToPerformTurnActionsPostfix(CommandTeleportTo __instance, SchemeVehicleSnapshot __state)
		{
			try
			{
				if (__state == null)
				{
					return;
				}

				if (TryCaptureSchemeCommandSnapshot(__instance?.pid ?? PlayerID.INVALID, __instance?.peepId ?? EntityID.INVALID, __instance?.goalID ?? NodeID.INVALID, "scheme-target-teleport", "after", out SchemeVehicleSnapshot after))
				{
					after.CommandType = "TeleportTo";
					LogSnapshot("scheme-target-teleport-finish", after, "beforeNode=" + FormatNodeId(__state.VehicleNodeId));
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle teleport postfix failed: " + ex.Message);
			}
		}

		private static void RemoveCrewFromBoardPrefix(PlayerCrew __instance, CrewAssignment crew, PlayerCrewData.OffBoardReason reason, bool removeCar, int daysAway, out SchemeVehicleSnapshot __state)
		{
			__state = null;
			try
			{
				if (reason != PlayerCrewData.OffBoardReason.Scheme)
				{
					return;
				}

				PlayerInfo player = ResolvePlayerForPeep(crew.peepId) ?? ResolvePlayerForCrew(__instance);
				__state = CaptureSnapshot("scheme-remove-from-board", player, crew, NodeID.INVALID, "before");
				__state.OffBoardReason = reason.ToString();
				TryFillSchemeDetails(player, crew.peepId, __state);
				TryRecordSchemeVehicleSuspension(player, __state);
				LogSnapshot("scheme-remove-from-board-start", __state, "removeCar=" + removeCar.ToString(CultureInfo.InvariantCulture) + " daysAway=" + daysAway.ToString(CultureInfo.InvariantCulture));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle remove prefix failed: " + ex.Message);
			}
		}

		private static void RemoveCrewFromBoardPostfix(PlayerCrew __instance, CrewAssignment crew, PlayerCrewData.OffBoardReason reason, bool removeCar, int daysAway, SchemeVehicleSnapshot __state)
		{
			try
			{
				if (__state == null)
				{
					return;
				}

				PlayerInfo player = ResolvePlayerForPeep(crew.peepId) ?? ResolvePlayerForCrew(__instance);
				CrewAssignment afterCrew = player?.crew?.GetCrewForPeep(crew.peepId) ?? crew;
				SchemeVehicleSnapshot after = CaptureSnapshot("scheme-remove-from-board", player, afterCrew, NodeID.INVALID, "after");
				CopySchemeDetails(__state, after);
				after.OffBoardReason = reason.ToString();
				TryFillOffBoardDetails(player, crew.peepId, after);
				if (TryGetSuspension(crew.peepId, out SchemeVehicleSuspension suspension) && IsSuspensionOriginalVehicleAvailable(player?.crew, suspension))
				{
					VerificationLog("SchemeVehicle", $"scheme-vehicle-suspended peep={crew.peepId.id} vehicle={suspension.VehicleId.id} occupants={suspension.OccupantCount} beforeVehicle={FormatEntityId(__state.VehicleId)} offBoardTemplate={after.OffBoardVehicleTemplate}");
				}
				LogSnapshot("scheme-remove-from-board-finish", after, "beforeVehicle=" + FormatEntityId(__state.VehicleId) + " removeCar=" + removeCar.ToString(CultureInfo.InvariantCulture));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle remove postfix failed: " + ex.Message);
			}
		}

		private static void ReturnCrewToBoardPrefix(PlayerCrew __instance, CrewAssignment crew, out SchemeVehicleSnapshot __state)
		{
			__state = null;
			try
			{
				PlayerInfo player = ResolvePlayerForPeep(crew.peepId) ?? ResolvePlayerForCrew(__instance);
				if (!TryGetOffBoardInfo(player?.crew, crew.peepId, out OffBoardInfo offBoard) || offBoard.reason != PlayerCrewData.OffBoardReason.Scheme)
				{
					return;
				}

				__state = CaptureSnapshot("scheme-return-to-board", player, crew, NodeID.INVALID, "before");
				__state.OffBoardReason = offBoard.reason.ToString();
				__state.OffBoardVehicleTemplate = FormatLabel(offBoard.vehTemplate);
				TryFillSchemeDetails(player, crew.peepId, __state);
				if (TryPrepareSchemeVehicleReturn(player, crew.peepId, out SchemeVehicleSuspension suspension, out string reason))
				{
					VerificationLog("SchemeVehicle", $"scheme-duplicate-spawn-blocked peep={crew.peepId.id} vehicle={suspension.VehicleId.id} reason={reason} offBoardTemplate={__state.OffBoardVehicleTemplate}");
				}
				else if (TryGetSuspension(crew.peepId, out SchemeVehicleSuspension existing))
				{
					VerificationLog("SchemeVehicle", $"scheme-duplicate-spawn-block-skipped peep={crew.peepId.id} vehicle={existing.VehicleId.id} reason={reason}");
				}
				LogSnapshot("scheme-return-to-board-start", __state);
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle return prefix failed: " + ex.Message);
			}
		}

		private static void ReturnCrewToBoardPostfix(PlayerCrew __instance, CrewAssignment crew, SchemeVehicleSnapshot __state)
		{
			try
			{
				if (__state == null)
				{
					return;
				}

				PlayerInfo player = ResolvePlayerForPeep(crew.peepId) ?? ResolvePlayerForCrew(__instance);
				CrewAssignment afterCrew = player?.crew?.GetCrewForPeep(crew.peepId) ?? CrewAssignment.EMPTY;
				SchemeVehicleSnapshot after = CaptureSnapshot("scheme-return-to-board", player, afterCrew, NodeID.INVALID, "after");
				CopySchemeDetails(__state, after);
				TryFillOffBoardDetails(player, crew.peepId, after);
				if (TryCompleteSchemeVehicleReturn(player, crew.peepId, out SchemeVehicleSuspension suspension, out string reason))
				{
					after = CaptureSnapshot("scheme-return-to-board", player, player?.crew?.GetCrewForPeep(crew.peepId) ?? CrewAssignment.EMPTY, NodeID.INVALID, "after-reattach");
					CopySchemeDetails(__state, after);
					VerificationLog("SchemeVehicle", $"scheme-vehicle-reattached peep={crew.peepId.id} vehicle={suspension.VehicleId.id} driver={FormatEntityId(suspension.DriverPeepId)} node={FormatNodeId(suspension.VehicleNodeId)} reason={reason}");
				}
				else if (TryGetSuspension(crew.peepId, out SchemeVehicleSuspension existing))
				{
					VerificationLog("SchemeVehicle", $"scheme-vehicle-reattach-skipped peep={crew.peepId.id} vehicle={existing.VehicleId.id} reason={reason}");
					if (!IsSuspensionOriginalVehicleAvailable(player?.crew, existing))
					{
						ClearSuspension(crew.peepId);
					}
				}
				LogSnapshot("scheme-return-to-board-finish", after, "beforeOffBoardTemplate=" + (__state.OffBoardVehicleTemplate ?? "none"));
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle return postfix failed: " + ex.Message);
			}
		}

		private static bool TryCaptureSchemeCommandSnapshot(PlayerID pid, EntityID peepId, NodeID goalNodeId, string source, string stage, out SchemeVehicleSnapshot snapshot)
		{
			snapshot = null;
			PlayerInfo player = ResolvePlayer(pid) ?? ResolvePlayerForPeep(peepId);
			if (player?.schemes == null || !player.schemes.IsInScheme(peepId))
			{
				return false;
			}

			CrewAssignment assignment = player.crew?.GetCrewForPeep(peepId) ?? CrewAssignment.EMPTY;
			if (!assignment.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return false;
			}

			snapshot = CaptureSnapshot(source, player, assignment, goalNodeId, stage);
			TryFillSchemeDetails(player, peepId, snapshot);
			return true;
		}

		private static bool TryBlockConcurrentVehicleSchemeStart(PlayerInfo player, CrewAssignment assignment, Label schemeID, out string reason)
		{
			reason = "none";
			if (player?.crew == null || player.schemes == null || !player.PID.IsHumanPlayer)
			{
				return false;
			}

			if (!assignment.IsValid || !assignment.peepId.IsValid || !assignment.IsInVehicle || !assignment.VehicleID.IsValid)
			{
				return false;
			}

			if (player.schemes.IsInScheme(assignment.peepId))
			{
				reason = "actor-already-in-scheme activePeep=" + FormatEntityId(assignment.peepId) + " source=self requestedScheme=" + FormatLabel(schemeID);
				return true;
			}

			if (TryFindActiveSchemePeepInVehicle(player, assignment.VehicleID, assignment.peepId, out EntityID activePeepId, out string source))
			{
				reason = "vehicle-active-scheme activePeep=" + FormatEntityId(activePeepId) + " source=" + source + " requestedScheme=" + FormatLabel(schemeID);
				return true;
			}

			return false;
		}

		private static bool TryFindActiveSchemePeepInVehicle(PlayerInfo player, EntityID vehicleId, EntityID excludedPeepId, out EntityID activePeepId, out string source)
		{
			activePeepId = EntityID.INVALID;
			source = "none";
			if (player?.crew == null || player.schemes == null || !vehicleId.IsValid)
			{
				return false;
			}

			try
			{
				foreach (CrewAssignment occupant in MultiCrewVehicleHelper.GetAllCrewInVehicle(player.crew, vehicleId))
				{
					if (!occupant.IsValid || !occupant.peepId.IsValid || occupant.peepId == excludedPeepId)
					{
						continue;
					}

					if (player.schemes.IsInScheme(occupant.peepId))
					{
						activePeepId = occupant.peepId;
						source = "occupant";
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle active occupant scan failed: " + ex.Message);
			}

			try
			{
				foreach (KeyValuePair<ulong, SchemeVehicleSuspension> entry in SuspendedSchemeVehiclesByPeep.ToArray())
				{
					SchemeVehicleSuspension suspension = entry.Value;
					if (suspension == null || !suspension.PeepId.IsValid)
					{
						SuspendedSchemeVehiclesByPeep.Remove(entry.Key);
						continue;
					}

					if (!player.schemes.IsInScheme(suspension.PeepId))
					{
						SuspendedSchemeVehiclesByPeep.Remove(entry.Key);
						VerificationLog("SchemeVehicle", $"scheme-vehicle-suspension-cleared peep={suspension.PeepId.id} vehicle={FormatEntityId(suspension.VehicleId)} reason=stale-not-in-scheme");
						continue;
					}

					if (suspension.VehicleId == vehicleId && suspension.PeepId != excludedPeepId)
					{
						activePeepId = suspension.PeepId;
						source = "suspended";
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle active suspension scan failed: " + ex.Message);
			}

			return false;
		}

		private static SchemeVehicleSnapshot CaptureSnapshot(string source, PlayerInfo player, CrewAssignment assignment, NodeID goalNodeId, string stage)
		{
			SchemeVehicleSnapshot snapshot = new SchemeVehicleSnapshot
			{
				Source = source,
				Pid = player?.PID ?? PlayerID.INVALID,
				PeepId = assignment.peepId,
				VehicleId = assignment.IsInVehicle ? assignment.VehicleID : EntityID.INVALID,
				GoalNodeId = goalNodeId,
				VehicleNodeId = NodeID.INVALID,
				PeepNodeId = TryGetPeepNodeId(assignment.peepId),
				VehicleNodeSource = "none",
				VehicleTemplate = "none",
				Occupants = "none",
				OffBoardReason = "none",
				OffBoardVehicleTemplate = "none"
			};

			if (player?.crew != null && assignment.IsValid)
			{
				try
				{
					snapshot.IsInScheme = player.schemes?.IsInScheme(assignment.peepId) == true;
					Entity peep = assignment.peepId.FindEntity();
					snapshot.IsOnBoard = peep != null && player.crew.IsOnBoard(peep);
				}
				catch
				{
					snapshot.IsOnBoard = false;
				}
			}

			if (player?.crew == null || !snapshot.VehicleId.IsValid)
			{
				return snapshot;
			}

			Entity vehicle = snapshot.VehicleId.FindEntity();
			if (vehicle?.config != null)
			{
				snapshot.VehicleTemplate = FormatLabel(vehicle.config.Template);
			}

			if (MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(snapshot.VehicleId, out NodeID vehicleNodeId, out string nodeSource))
			{
				snapshot.VehicleNodeId = vehicleNodeId;
				snapshot.VehicleNodeSource = nodeSource ?? "unknown";
			}

			List<CrewAssignment> occupants = MultiCrewVehicleHelper.GetAllCrewInVehicle(player.crew, snapshot.VehicleId);
			snapshot.OccupantCount = occupants.Count;
			snapshot.PassengerCount = MultiCrewVehicleHelper.GetPassengers(player.crew, snapshot.VehicleId).Count;
			snapshot.SlotCount = MultiCrewVehicleHelper.GetVehicleCrewSlots(snapshot.VehicleId);
			snapshot.DriverPeepId = MultiCrewVehicleHelper.GetDriverPeepId(player.crew, snapshot.VehicleId);
			snapshot.IsDriver = snapshot.DriverPeepId.IsValid && snapshot.DriverPeepId == snapshot.PeepId;
			snapshot.Occupants = FormatOccupants(occupants);

			if (MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(snapshot.VehicleId, out _, out NodeID expectedNodeId, out NodeID pendingGoalNodeId))
			{
				snapshot.HasPendingRoute = true;
				snapshot.PendingExpectedNodeId = expectedNodeId;
				snapshot.PendingGoalNodeId = pendingGoalNodeId;
			}

			snapshot.RouteActive = MultiCrewVehicleHelper.IsHumanVehicleTravelActive(snapshot.VehicleId);
			snapshot.RouteQueuedResume = MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(snapshot.VehicleId);

			return snapshot;
		}

		private static void FillSchemeDetails(PlayerScheme schemeManager, SchemeData scheme, SchemeVehicleSnapshot snapshot)
		{
			if (scheme == null || snapshot == null)
			{
				return;
			}

			snapshot.SchemeId = FormatLabel(scheme.schemeID);
			snapshot.ChapterId = FormatLabel(scheme.currentChapter?.chapterID ?? Label.NULL);
			ChapterDef chapter = schemeManager?.Settings?.FindChapterById(scheme.currentChapter?.chapterID ?? Label.NULL);
			snapshot.ScriptId = FormatLabel(chapter?.script ?? Label.NULL);
			snapshot.RecoveryScriptId = FormatLabel(chapter?.recoveryScript ?? Label.NULL);
		}

		private static void TryFillSchemeDetails(PlayerInfo player, EntityID peepId, SchemeVehicleSnapshot snapshot)
		{
			try
			{
				SchemeData scheme = player?.schemes?.GetSchemeForCrew(peepId);
				FillSchemeDetails(player?.schemes, scheme, snapshot);
			}
			catch
			{
			}
		}

		private static void CopySchemeDetails(SchemeVehicleSnapshot from, SchemeVehicleSnapshot to)
		{
			if (from == null || to == null)
			{
				return;
			}

			to.SchemeId = from.SchemeId;
			to.ChapterId = from.ChapterId;
			to.ScriptId = from.ScriptId;
			to.RecoveryScriptId = from.RecoveryScriptId;
		}

		private static void TryFillOffBoardDetails(PlayerInfo player, EntityID peepId, SchemeVehicleSnapshot snapshot)
		{
			if (snapshot == null || !TryGetOffBoardInfo(player?.crew, peepId, out OffBoardInfo offBoard))
			{
				return;
			}

			snapshot.OffBoardReason = offBoard.reason.ToString();
			snapshot.OffBoardVehicleTemplate = FormatLabel(offBoard.vehTemplate);
		}

		private static bool TryGetOffBoardInfo(PlayerCrew crew, EntityID peepId, out OffBoardInfo info)
		{
			info = default(OffBoardInfo);
			if (crew == null || !peepId.IsValid)
			{
				return false;
			}

			List<OffBoardInfo> offBoard = crew.GetCrewOffBoardUnsafe();
			if (offBoard == null)
			{
				return false;
			}

			for (int i = 0; i < offBoard.Count; i++)
			{
				if (offBoard[i].crew == peepId)
				{
					info = offBoard[i];
					return true;
				}
			}

			return false;
		}

		private static void TryRecordSchemeVehicleSuspension(PlayerInfo player, SchemeVehicleSnapshot snapshot)
		{
			if (player?.crew == null || snapshot == null || !snapshot.PeepId.IsValid || !snapshot.VehicleId.IsValid)
			{
				return;
			}

			if (!player.PID.IsHumanPlayer || snapshot.OccupantCount <= 1)
			{
				return;
			}

			try
			{
				SuspendedSchemeVehiclesByPeep[snapshot.PeepId.id] = new SchemeVehicleSuspension
				{
					PeepId = snapshot.PeepId,
					VehicleId = snapshot.VehicleId,
					DriverPeepId = snapshot.DriverPeepId,
					VehicleNodeId = snapshot.VehicleNodeId,
					SchemeId = snapshot.SchemeId,
					ChapterId = snapshot.ChapterId,
					VehicleTemplate = snapshot.VehicleTemplate,
					Occupants = snapshot.Occupants,
					OccupantCount = snapshot.OccupantCount,
					WasDriver = snapshot.IsDriver
				};
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle suspension record failed: " + ex.Message);
			}
		}

		private static bool TryPrepareSchemeVehicleReturn(PlayerInfo player, EntityID peepId, out SchemeVehicleSuspension suspension, out string reason)
		{
			reason = "none";
			if (!TryGetSuspension(peepId, out suspension))
			{
				reason = "no-suspension";
				return false;
			}

			if (player?.crew == null)
			{
				reason = "missing-player-crew";
				return false;
			}

			if (!IsSuspensionOriginalVehicleAvailable(player.crew, suspension))
			{
				reason = "original-vehicle-unavailable";
				return false;
			}

			if (!TryClearOffBoardVehicleTemplate(player.crew, peepId, out string template))
			{
				reason = "offboard-template-not-cleared";
				return false;
			}

			reason = "cleared-template-" + template;
			return true;
		}

		private static bool TryCompleteSchemeVehicleReturn(PlayerInfo player, EntityID peepId, out SchemeVehicleSuspension suspension, out string reason)
		{
			reason = "none";
			if (!TryGetSuspension(peepId, out suspension))
			{
				reason = "no-suspension";
				return false;
			}

			if (player?.crew == null)
			{
				reason = "missing-player-crew";
				return false;
			}

			if (!IsSuspensionOriginalVehicleAvailable(player.crew, suspension))
			{
				reason = "original-vehicle-unavailable";
				return false;
			}

			if (!TryAssignCrewToVehicleMetadataOnly(player.crew, peepId, suspension.VehicleId))
			{
				reason = "metadata-assign-failed";
				return false;
			}

			TrySyncReturnedSchemeActorToVehicleNode(player.crew, peepId, suspension.VehicleId, out NodeID nodeId);
			MultiCrewVehicleHelper.TryRefreshCrewHudCardsForVehicle(suspension.VehicleId);
			ClearSuspension(peepId);
			reason = "metadata-reattach node=" + FormatNodeId(nodeId);
			return true;
		}

		private static bool TryGetSuspension(EntityID peepId, out SchemeVehicleSuspension suspension)
		{
			suspension = null;
			return peepId.IsValid && SuspendedSchemeVehiclesByPeep.TryGetValue(peepId.id, out suspension);
		}

		private static void ClearSuspension(EntityID peepId)
		{
			if (peepId.IsValid)
			{
				SuspendedSchemeVehiclesByPeep.Remove(peepId.id);
			}
		}

		private static bool IsSuspensionOriginalVehicleAvailable(PlayerCrew crew, SchemeVehicleSuspension suspension)
		{
			if (crew == null || suspension == null || !suspension.VehicleId.IsValid || suspension.VehicleId.FindEntity() == null)
			{
				return false;
			}

			return MultiCrewVehicleHelper.GetLiveVehicleCrewCount(crew, suspension.VehicleId) > 0;
		}

		private static bool TryClearOffBoardVehicleTemplate(PlayerCrew crew, EntityID peepId, out string priorTemplate)
		{
			priorTemplate = "none";
			List<OffBoardInfo> offBoard = crew?.GetCrewOffBoardUnsafe();
			if (offBoard == null || !peepId.IsValid)
			{
				return false;
			}

			for (int i = 0; i < offBoard.Count; i++)
			{
				OffBoardInfo info = offBoard[i];
				if (info.crew != peepId || info.reason != PlayerCrewData.OffBoardReason.Scheme)
				{
					continue;
				}

				priorTemplate = FormatLabel(info.vehTemplate);
				if (!info.vehTemplate.IsSet)
				{
					return true;
				}

				offBoard[i] = new OffBoardInfo(info.crew, Label.NULL, info.reason);
				return true;
			}

			return false;
		}

		private static bool TryAssignCrewToVehicleMetadataOnly(PlayerCrew crew, EntityID peepId, EntityID vehicleId)
		{
			if (crew == null || !peepId.IsValid || !vehicleId.IsValid || PlayerCrewDataField == null)
			{
				return false;
			}

			try
			{
				PlayerCrewData crewData = PlayerCrewDataField.GetValue(crew) as PlayerCrewData;
				if (crewData == null)
				{
					return false;
				}

				int index = crewData.FindPeepIndex(peepId);
				if (index < 0)
				{
					return false;
				}

				CrewAssignment assignment = crewData.Get(index);
				if (!assignment.IsValid || !assignment.IsNotDead)
				{
					return false;
				}

				crewData.Set(index, assignment.SetVehicle(vehicleId));
				global::Game.Game.ctx?.events?.EnqueueOnce(new SessionEvent(SessionEventType.CrewVehicleReassigned, vehicleId, crew.PID));
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle metadata reattach failed: " + ex.Message);
				return false;
			}
		}

		private static void TrySyncReturnedSchemeActorToVehicleNode(PlayerCrew crew, EntityID peepId, EntityID vehicleId, out NodeID nodeId)
		{
			nodeId = NodeID.INVALID;
			try
			{
				if (!MultiCrewVehicleHelper.TryGetAuthoritativeVehicleNodeId(vehicleId, out nodeId, out _) || !nodeId.IsValid)
				{
					return;
				}

				Entity peep = peepId.FindEntity();
				if (peep != null)
				{
					global::Game.Game.ctx?.transit?.SetAgentAtNode(nodeId, peep);
				}

				MultiCrewVehicleHelper.TrySyncVehicleOccupantsToVehicleNode(crew, vehicleId, "scheme-return-reattach");
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[GameplayTweaks] SchemeVehicle return node sync failed: " + ex.Message);
			}
		}

		private static PlayerInfo ResolvePlayer(PlayerID pid)
		{
			try
			{
				return pid.IsValid ? global::Game.Game.ctx?.players?.WithID(pid) : null;
			}
			catch
			{
				return null;
			}
		}

		private static PlayerInfo ResolvePlayerForPeep(EntityID peepId)
		{
			try
			{
				Entity peep = peepId.IsValid ? peepId.FindEntity() : null;
				PlayerID pid = peep?.data?.agent?.pid ?? PlayerID.INVALID;
				if (pid.IsValid)
				{
					return ResolvePlayer(pid);
				}
			}
			catch
			{
			}

			try
			{
				return G.GetAllPlayers()
					.FirstOrDefault(p => p?.crew != null && p.crew.GetCrewForPeep(peepId).IsValid);
			}
			catch
			{
				return null;
			}
		}

		private static PlayerInfo ResolvePlayerForCrew(PlayerCrew crew)
		{
			if (crew == null)
			{
				return null;
			}

			try
			{
				return G.GetAllPlayers().FirstOrDefault(p => ReferenceEquals(p?.crew, crew));
			}
			catch
			{
				return null;
			}
		}

		private static NodeID ResolveTargetNode(EntityID target)
		{
			try
			{
				Entity entity = target.IsValid ? target.FindEntity() : null;
				return entity?.data?.board?.bead.nodeId ?? NodeID.INVALID;
			}
			catch
			{
				return NodeID.INVALID;
			}
		}

		private static NodeID TryGetPeepNodeId(EntityID peepId)
		{
			try
			{
				return peepId.IsValid ? peepId.FindEntity()?.components?.agent?.GetNode()?.id ?? NodeID.INVALID : NodeID.INVALID;
			}
			catch
			{
				return NodeID.INVALID;
			}
		}

		private static string GetBoardCommandTag(CommandType type, string stage)
		{
			if (type == CommandType.RemoveFromBoard)
			{
				return "scheme-board-remove-" + stage;
			}

			if (type == CommandType.ReturnToBoard)
			{
				return "scheme-board-return-" + stage;
			}

			return "scheme-board-command-" + stage;
		}

		private static void LogSnapshot(string eventName, SchemeVehicleSnapshot snapshot, string extra = null)
		{
			if (snapshot == null)
			{
				return;
			}

			VerificationLog(
				"SchemeVehicle",
				eventName
				+ " source=" + (snapshot.Source ?? "unknown")
				+ " command=" + (snapshot.CommandType ?? "none")
				+ " pid=" + (snapshot.Pid.IsValid ? snapshot.Pid.id.ToString(CultureInfo.InvariantCulture) : "-1")
				+ " scheme=" + (snapshot.SchemeId ?? "none")
				+ " chapter=" + (snapshot.ChapterId ?? "none")
				+ " script=" + (snapshot.ScriptId ?? "none")
				+ " recovery=" + (snapshot.RecoveryScriptId ?? "none")
				+ " peep=" + FormatEntityId(snapshot.PeepId)
				+ " vehicle=" + FormatEntityId(snapshot.VehicleId)
				+ " template=" + (snapshot.VehicleTemplate ?? "none")
				+ " peepNode=" + FormatNodeId(snapshot.PeepNodeId)
				+ " vehicleNode=" + FormatNodeId(snapshot.VehicleNodeId)
				+ " vehicleNodeSource=" + (snapshot.VehicleNodeSource ?? "none")
				+ " goalNode=" + FormatNodeId(snapshot.GoalNodeId)
				+ " occupants=" + snapshot.OccupantCount.ToString(CultureInfo.InvariantCulture)
				+ " passengers=" + snapshot.PassengerCount.ToString(CultureInfo.InvariantCulture)
				+ " slots=" + snapshot.SlotCount.ToString(CultureInfo.InvariantCulture)
				+ " driver=" + FormatEntityId(snapshot.DriverPeepId)
				+ " isDriver=" + snapshot.IsDriver.ToString(CultureInfo.InvariantCulture)
				+ " inScheme=" + snapshot.IsInScheme.ToString(CultureInfo.InvariantCulture)
				+ " onBoard=" + snapshot.IsOnBoard.ToString(CultureInfo.InvariantCulture)
				+ " routePending=" + snapshot.HasPendingRoute.ToString(CultureInfo.InvariantCulture)
				+ " routeActive=" + snapshot.RouteActive.ToString(CultureInfo.InvariantCulture)
				+ " routeQueuedResume=" + snapshot.RouteQueuedResume.ToString(CultureInfo.InvariantCulture)
				+ " pendingExpected=" + FormatNodeId(snapshot.PendingExpectedNodeId)
				+ " pendingGoal=" + FormatNodeId(snapshot.PendingGoalNodeId)
				+ " offBoardReason=" + (snapshot.OffBoardReason ?? "none")
				+ " offBoardTemplate=" + (snapshot.OffBoardVehicleTemplate ?? "none")
				+ " occupantIds=" + (snapshot.Occupants ?? "none")
				+ (string.IsNullOrEmpty(extra) ? string.Empty : " " + extra));
		}

		private static string FormatOccupants(List<CrewAssignment> occupants)
		{
			if (occupants == null || occupants.Count == 0)
			{
				return "none";
			}

			return string.Join(",", occupants
				.Where(item => item.peepId.IsValid)
				.Select(item => item.peepId.id.ToString(CultureInfo.InvariantCulture))
				.ToArray());
		}

		private static string FormatEntityId(EntityID id)
		{
			return id.IsValid ? id.id.ToString(CultureInfo.InvariantCulture) : "0";
		}

		private static string FormatNodeId(NodeID id)
		{
			return id.IsValid ? id.ToString() : "none";
		}

		private static string FormatLabel(Label label)
		{
			return label.IsSet ? label.ToString() : "none";
		}
	}
}
}
