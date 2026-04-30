using BepInEx.Configuration;
using Game.Session.Entities;
using Game.Session.Sim;
using HarmonyLib;
using UnityEngine;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	internal static ConfigEntry<bool> EnableFakeTraffic;

	internal static ConfigEntry<int> FakeTrafficMaxVisible;

	internal static ConfigEntry<float> FakeTrafficInnerRingRadius;

	internal static ConfigEntry<float> FakeTrafficMiddleRingRadius;

	internal static ConfigEntry<float> FakeTrafficDespawnRadius;

	internal static ConfigEntry<float> FakeTrafficSpawnPerRoadKm;

	internal static ConfigEntry<int> FakeTrafficUpdateBuckets;

	internal static ConfigEntry<float> FakeTrafficSelectionSuppressRadius;

	internal static ConfigEntry<float> FakeTrafficDowntownBias;

	internal static ConfigEntry<float> FakeTrafficQueueSpacing;

	internal static ConfigEntry<float> FakeTrafficIntersectionStopChance;

	internal static ConfigEntry<float> FakeTrafficStopLineOffset;

	internal static ConfigEntry<float> FakeTrafficStopSecondsMin;

	internal static ConfigEntry<float> FakeTrafficStopSecondsMax;

	internal static ConfigEntry<float> FakeTrafficPlatoonChance;

	internal static ConfigEntry<int> FakeTrafficMaxPlatoonFollowers;

	internal static ConfigEntry<float> FakeTrafficSpeedMin;

	internal static ConfigEntry<float> FakeTrafficSpeedMax;

	internal static ConfigEntry<int> FakeTrafficRealAmbientCap;

	internal static ConfigEntry<bool> FakeTrafficDiagnosticsEnabled;

	internal static ConfigEntry<float> FakeTrafficDiagnosticsLogInterval;

	private static FakeTrafficManager _fakeTrafficManager;

	private static bool _fakeTrafficStateLogged;

	private static void EnsureFakeTrafficSubsystem()
	{
		if ((Object)(object)Instance == (Object)null)
		{
			return;
		}

		if ((Object)(object)_fakeTrafficManager == (Object)null)
		{
			_fakeTrafficManager = Instance.gameObject.GetComponent<FakeTrafficManager>();
			if ((Object)(object)_fakeTrafficManager == (Object)null)
			{
				_fakeTrafficManager = Instance.gameObject.AddComponent<FakeTrafficManager>();
			}
		}

		if (!_fakeTrafficStateLogged)
		{
			_fakeTrafficStateLogged = true;
			VerificationLog("FakeTraffic", $"config enabled={EnableFakeTraffic?.Value ?? false} maxVisible={FakeTrafficMaxVisible?.Value ?? 0} innerRadius={FakeTrafficInnerRingRadius?.Value ?? 0f:0.##} middleRadius={FakeTrafficMiddleRingRadius?.Value ?? 0f:0.##} despawnRadius={FakeTrafficDespawnRadius?.Value ?? 0f:0.##} spawnPerRoadKm={FakeTrafficSpawnPerRoadKm?.Value ?? 0f:0.##} updateBuckets={FakeTrafficUpdateBuckets?.Value ?? 0} suppressRadius={FakeTrafficSelectionSuppressRadius?.Value ?? 0f:0.##} downtownBias={FakeTrafficDowntownBias?.Value ?? 0f:0.##} queueSpacing={FakeTrafficQueueSpacing?.Value ?? 0f:0.##} stopChance={FakeTrafficIntersectionStopChance?.Value ?? 0f:0.##} stopLineOffset={FakeTrafficStopLineOffset?.Value ?? 0f:0.##} stopMin={FakeTrafficStopSecondsMin?.Value ?? 0f:0.##} stopMax={FakeTrafficStopSecondsMax?.Value ?? 0f:0.##} platoonChance={FakeTrafficPlatoonChance?.Value ?? 0f:0.##} platoonFollowers={FakeTrafficMaxPlatoonFollowers?.Value ?? 0} speedMin={FakeTrafficSpeedMin?.Value ?? 0f:0.##} speedMax={FakeTrafficSpeedMax?.Value ?? 0f:0.##} realAmbientCap={FakeTrafficRealAmbientCap?.Value ?? 0}");
		}
	}

	internal static string GetFakeTrafficDiagnosticsSummary()
	{
		if ((Object)(object)_fakeTrafficManager == (Object)null)
		{
			return "manager=missing";
		}

		return _fakeTrafficManager.GetDiagnosticsSummary();
	}

	internal static bool ShouldRunFakeTraffic()
	{
		if (!(EnableFakeTraffic?.Value ?? false))
		{
			return false;
		}

		if (Game.Game.ctx == null || !Game.Game.ctx.IsInteractive)
		{
			return false;
		}

		return Game.Game.serv?.saveload?.prefs?.game?.trafficEnabled ?? false;
	}

	private void OnDestroy()
	{
		if ((Object)(object)_fakeTrafficManager != (Object)null)
		{
			Object.Destroy(_fakeTrafficManager);
			_fakeTrafficManager = null;
		}
	}
}

internal static class FakeTrafficAmbientCapPatch
{
	internal static void ApplyPatch(Harmony harmony)
	{
		var target = AccessTools.Method(typeof(TransitManager), "CanSpawnAmbientCar");
		if (target != null)
		{
			harmony.Patch(target, postfix: new HarmonyMethod(typeof(FakeTrafficAmbientCapPatch), nameof(Postfix)));
		}
	}

	private static void Postfix(TransitManager __instance, ref bool __result)
	{
		if (!__result || !GameplayTweaksPlugin.ShouldRunFakeTraffic())
		{
			return;
		}

		int cap = Mathf.Max(0, GameplayTweaksPlugin.FakeTrafficRealAmbientCap?.Value ?? 0);
		if (cap <= 0)
		{
			__result = false;
			return;
		}

		int ambientCount = 0;
		var cars = __instance?.data?.cars;
		if (cars == null)
		{
			return;
		}

		for (int i = 0; i < cars.Count; i++)
		{
			Entity entity = cars[i].FindEntity();
			if (entity != null && entity.config != null && entity.config.Template == TransitManager.AMBIENT_CAR)
			{
				ambientCount++;
				if (ambientCount >= cap)
				{
					__result = false;
					return;
				}
			}
		}
	}
}
}
