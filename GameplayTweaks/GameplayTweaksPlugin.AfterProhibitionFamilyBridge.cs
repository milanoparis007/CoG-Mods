using System;
using System.Reflection;
using BepInEx.Bootstrap;
using Game.Session.Entities;

namespace GameplayTweaks
{
public partial class GameplayTweaksPlugin
{
	private static bool TrySchedulePregnancyWithAfterProhibitionFamily(
		Entity selectedPeep,
		out ulong motherId,
		out int dueDay,
		out int durationDays,
		out int futureKidsCount,
		out string reason)
	{
		motherId = 0UL;
		dueDay = -1;
		durationDays = 0;
		futureKidsCount = 0;
		reason = "missing-family-plugin";

		try
		{
			if (!Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				if (pluginType == null)
				{
					continue;
				}

				MethodInfo ownsMethod = pluginType.GetMethod("OwnsPregnancyLifecycle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null || !(ownsMethod.Invoke(null, null) is bool ownsPregnancy) || !ownsPregnancy)
				{
					reason = "bridge-disabled";
					return false;
				}

				MethodInfo scheduleMethod = pluginType.GetMethod("TrySchedulePregnancyForCrewPeep", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (scheduleMethod == null)
				{
					reason = "missing-schedule-method";
					return false;
				}

				object[] args =
				{
					selectedPeep,
					0UL,
					-1,
					0,
					0,
					string.Empty
				};
				bool scheduled = scheduleMethod.Invoke(null, args) is bool result && result;
				motherId = args[1] is ulong outMotherId ? outMotherId : 0UL;
				dueDay = args[2] is int outDueDay ? outDueDay : -1;
				durationDays = args[3] is int outDurationDays ? outDurationDays : 0;
				futureKidsCount = args[4] is int outFutureKidsCount ? outFutureKidsCount : 0;
				reason = args[5] as string ?? (scheduled ? "ok" : "unknown");
				return scheduled;
			}

			reason = "missing-plugin-type";
			return false;
		}
		catch (Exception ex)
		{
			reason = "bridge-error:" + ex.GetType().Name + ":" + ex.Message;
			return false;
		}
	}

	private static bool ShouldFallbackToGameplayTweaksPregnancy(string reason)
	{
		return string.Equals(reason, "missing-family-plugin", StringComparison.Ordinal)
			|| string.Equals(reason, "missing-plugin-type", StringComparison.Ordinal)
			|| string.Equals(reason, "missing-schedule-method", StringComparison.Ordinal)
			|| string.Equals(reason, "bridge-disabled", StringComparison.Ordinal)
			|| (reason != null && reason.StartsWith("bridge-error:", StringComparison.Ordinal));
	}

	private static bool TryGetBusinessOwnerFamilyBlockReasonWithAfterProhibitionFamily(Entity person, out string reason)
	{
		reason = null;
		try
		{
			if (!Chainloader.PluginInfos.ContainsKey("afterprohibition.family"))
			{
				return false;
			}

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type pluginType = assembly.GetType("AfterProhibitionFamily.AfterProhibitionFamilyPlugin", throwOnError: false);
				if (pluginType == null)
				{
					continue;
				}

				MethodInfo ownsMethod = pluginType.GetMethod("OwnsBusinessOwnerFamilySafety", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (ownsMethod == null || !(ownsMethod.Invoke(null, null) is bool ownsSafety) || !ownsSafety)
				{
					reason = "bridge-disabled";
					return false;
				}

				MethodInfo safetyMethod = pluginType.GetMethod("IsBusinessOwnerFamilySafe", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (safetyMethod == null)
				{
					reason = "missing-business-owner-safety-method";
					return false;
				}

				object[] args = { person, string.Empty };
				bool safe = safetyMethod.Invoke(null, args) is bool value && value;
				reason = args[1] as string ?? (safe ? "ok" : "unknown");
				return !safe && !string.Equals(reason, "ok", StringComparison.Ordinal);
			}

			reason = "missing-plugin-type";
			return false;
		}
		catch (Exception ex)
		{
			reason = "bridge-error:" + ex.GetType().Name + ":" + ex.Message;
			return false;
		}
	}
}
}
