using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private sealed class DeliveryStreetCreditPatch
	{
		private struct DeliveryStreetCreditState
		{
			public int Level;
			public float Progress;
			public bool Valid;
		}

		private const float PassengerStreetCredShare = 0.25f;

		private static readonly Dictionary<string, int> _automationNullRefLogDayByKey = new Dictionary<string, int>();

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				harmony.Patch(
					AccessTools.Method(typeof(CommandAutomationStep), "OnStarted"),
					prefix: new HarmonyMethod(typeof(DeliveryStreetCreditPatch), nameof(OnStartedPrefix)),
					postfix: new HarmonyMethod(typeof(DeliveryStreetCreditPatch), nameof(OnStartedPostfix)),
					finalizer: new HarmonyMethod(typeof(DeliveryStreetCreditPatch), nameof(OnStartedFinalizer)));
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("[GameplayTweaks] DeliveryStreetCreditPatch ApplyPatch failed: " + ex);
			}
		}

		private static void OnStartedPrefix(CommandAutomationStep __instance, out DeliveryStreetCreditState __state)
		{
			__state = CaptureState(GetCrewStateOrNull(__instance?.peepId ?? EntityID.INVALID));
		}

		private static void OnStartedPostfix(CommandAutomationStep __instance, DeliveryStreetCreditState __state)
		{
			try
			{
				PlayerInfo player = G.FindPlayerById(__instance.pid.id);
				if (player?.crew == null)
				{
					return;
				}
				Entity driverPeep = __instance.peepId.FindEntity();
				CrewModState driverState = GetCrewStateOrNull(__instance.peepId);
				if (driverPeep == null || driverState == null)
				{
					return;
				}
				CrewAssignment driverAssignment = player.crew.GetCrewForPeep(__instance.peepId);
				if (!driverAssignment.IsValid || !driverAssignment.IsInVehicle || !driverAssignment.VehicleID.IsValid)
				{
					return;
				}
				float driverGain = ResolveStreetCreditDelta(driverState, __state);
				if (driverGain <= 0f)
				{
					return;
				}
				float passengerGain = driverGain * PassengerStreetCredShare;
				if (passengerGain <= 0f)
				{
					return;
				}
				List<CrewAssignment> passengers = MultiCrewVehicleHelper.GetPassengers(player.crew, driverAssignment.VehicleID);
				foreach (CrewAssignment passenger in passengers)
				{
					if (!passenger.IsValid || passenger.peepId == __instance.peepId)
					{
						continue;
					}
					CrewModState passengerState = GetOrCreateCrewState(passenger.peepId);
					if (passengerState == null)
					{
						continue;
					}
					CrewRelationshipHandlerPatch.ApplyStreetCreditProgressGain(passengerState, player, passenger.peepId, passengerGain, "delivery-passenger");
				}
				VerificationLog("StreetCredit", $"source=delivery-passenger driver={__instance.peepId.id} vehicle={driverAssignment.VehicleID.id} driverGain={driverGain:0.000} passengerGain={passengerGain:0.000} passengers={passengers.Count}");
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogWarning("[GameplayTweaks] DeliveryStreetCreditPatch OnStartedPostfix failed: " + ex.Message);
			}
		}

		private static Exception OnStartedFinalizer(CommandAutomationStep __instance, Exception __exception)
		{
			if (__exception == null)
			{
				return null;
			}
			if (__exception is NullReferenceException
				&& ShouldSuppressHumanAutomationNullRef(__instance, out string context))
			{
				LogSuppressedAutomationNullRef(context);
				return null;
			}

			return __exception;
		}

		private static bool ShouldSuppressHumanAutomationNullRef(CommandAutomationStep command, out string context)
		{
			context = "reason=unknown";
			if (command == null || !command.pid.IsHumanPlayer)
			{
				return false;
			}

			PlayerInfo player = G.FindPlayerById(command.pid.id);
			if (player?.crew == null)
			{
				context = $"player={command.pid.id} peep={command.peepId.id} building={command.buildingId.id} reason=no-human-crew";
				return true;
			}

			CrewAssignment assignment = player.crew.GetCrewForPeep(command.peepId);
			EntityID vehicleId = assignment.IsValid ? assignment.VehicleID : EntityID.INVALID;
			AutomationSequence sequence = null;
			AutomationStep step = null;
			try
			{
				sequence = assignment.IsValid ? player.automation?.GetAutoOrNull(assignment) : null;
				step = sequence?.GetNextStep();
			}
			catch
			{
			}

			string staleReason = GetAutomationNullRefStaleReason(command, player, assignment, sequence, step);
			bool routeOwned = vehicleId.IsValid
				&& (MultiCrewVehicleHelper.IsHumanVehicleTravelActive(vehicleId)
					|| MultiCrewVehicleHelper.HasQueuedHumanVehiclePendingResume(vehicleId)
					|| MultiCrewVehicleHelper.TryGetPendingHumanVehicleTravel(vehicleId, out _, out NodeID expectedNodeId, out NodeID goalNodeId)
						&& (expectedNodeId.IsValid || goalNodeId.IsValid));
			if (sequence != null && (staleReason != null || !routeOwned))
			{
				try
				{
					sequence.Stop();
				}
				catch
				{
				}
			}
			context = $"player={command.pid.id} peep={command.peepId.id} vehicle={vehicleId.id} building={command.buildingId.id} routeOwned={routeOwned} assignmentValid={assignment.IsValid} inVehicle={assignment.IsInVehicle} seq={(sequence != null ? sequence.id.id.ToString() : "none")} step={(sequence != null ? sequence.nextstep.ToString() : "-1")} action={(step != null ? step.action.ToString() : "none")} target={(step != null ? step.target.id.ToString() : "0")} stopped={(sequence != null && (staleReason != null || !routeOwned))} reason={staleReason ?? (routeOwned ? "route-owned" : "automation-step-nullref")}";
			return true;
		}

		private static string GetAutomationNullRefStaleReason(CommandAutomationStep command, PlayerInfo player, CrewAssignment assignment, AutomationSequence sequence, AutomationStep step)
		{
			try
			{
				if (!assignment.IsValid)
				{
					return "missing-crew-assignment";
				}
				Entity peep = command.peepId.FindEntity();
				if (peep?.components?.agent == null)
				{
					return "missing-peep-agent";
				}
				if (sequence == null)
				{
					return "missing-automation-sequence";
				}
				if (step == null)
				{
					return "missing-automation-step";
				}
				if (!command.buildingId.IsValid && !step.target.IsValid)
				{
					return "missing-target";
				}
				Entity target = (command.buildingId.IsValid ? command.buildingId : step.target).FindEntity();
				if (target == null)
				{
					return "missing-target-entity";
				}
				if (step.action != AutoAction.None && step.target.IsValid && step.target.FindEntity() == null)
				{
					return "missing-step-target";
				}
				if (player?.automation == null)
				{
					return "missing-automation-executor";
				}
			}
			catch
			{
				return "inspection-failed";
			}
			return null;
		}

		private static void LogSuppressedAutomationNullRef(string context)
		{
			try
			{
				int day = G.GetNow().days;
				string key = context ?? "unknown";
				if (_automationNullRefLogDayByKey.TryGetValue(key, out int lastDay) && lastDay == day)
				{
					return;
				}
				_automationNullRefLogDayByKey[key] = day;
				VerificationLog("VehicleNodeAuthority", $"automation-step-nullref-swallowed {context}");
			}
			catch
			{
			}
		}

		private static DeliveryStreetCreditState CaptureState(CrewModState state)
		{
			if (state == null)
			{
				return default(DeliveryStreetCreditState);
			}
			return new DeliveryStreetCreditState
			{
				Level = state.StreetCreditLevel,
				Progress = Mathf.Clamp01(state.StreetCreditProgress),
				Valid = true
			};
		}

		private static float ResolveStreetCreditDelta(CrewModState state, DeliveryStreetCreditState before)
		{
			if (state == null || !before.Valid)
			{
				return 0f;
			}
			float after = state.StreetCreditLevel + Mathf.Clamp01(state.StreetCreditProgress);
			float prior = before.Level + before.Progress;
			return Mathf.Max(0f, after - prior);
		}
	}
}
}
