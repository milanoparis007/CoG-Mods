using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
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

		public static void ApplyPatch(Harmony harmony)
		{
			try
			{
				harmony.Patch(
					AccessTools.Method(typeof(CommandAutomationStep), "OnStarted"),
					prefix: new HarmonyMethod(typeof(DeliveryStreetCreditPatch), nameof(OnStartedPrefix)),
					postfix: new HarmonyMethod(typeof(DeliveryStreetCreditPatch), nameof(OnStartedPostfix)));
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
