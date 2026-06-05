using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;

namespace AfterProhibitionPolitics
{
	internal static class PoliticalBribeStateBridge
	{
		internal static PoliticalBribeStateSummary Capture()
		{
			PoliticalBribeStateSummary summary = new PoliticalBribeStateSummary
			{
				Today = SafeToday(),
				Reason = "unreadable"
			};

			try
			{
				Type pluginType = FindType("GameplayTweaks.GameplayTweaksPlugin");
				Type crewPatchType = FindType("GameplayTweaks.GameplayTweaksPlugin+CrewRelationshipHandlerPatch");
				if (pluginType == null || crewPatchType == null)
				{
					summary.Reason = "gameplaytweaks-type-missing";
					return summary;
				}

				summary.GameplayTweaksFound = true;
				summary.HumanGlobalActive = ReadStaticBool(crewPatchType, "_globalMayorBribeActive");
				summary.HumanGlobalExpireDay = ReadStaticInt(crewPatchType, "_globalMayorBribeExpireDay", -1);
				summary.HumanGlobalExpired = summary.HumanGlobalActive
					&& summary.HumanGlobalExpireDay >= 0
					&& summary.Today >= summary.HumanGlobalExpireDay;

				object saveData = ReadStaticField(pluginType, "SaveData");
				if (saveData == null)
				{
					summary.Reason = "save-data-missing";
					return summary;
				}

				CountAiMayorBribes(saveData, summary);
				CountCrewBribeStates(saveData, summary);
				summary.Reason = "readable";
				return summary;
			}
			catch (Exception ex)
			{
				summary.Reason = "error-" + ex.GetType().Name;
				return summary;
			}
		}

		internal static void LogStartupAudit(string source, ManualLogSource logger)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				PoliticalBribeStateSummary summary = Capture();
				logger.LogInfo(
					"bribe-state-audit source=" + source +
					" gameplayTweaks=" + summary.GameplayTweaksFound +
					" today=" + summary.Today +
					" humanGlobalActive=" + summary.HumanGlobalActive +
					" humanGlobalExpireDay=" + summary.HumanGlobalExpireDay +
					" humanGlobalExpired=" + summary.HumanGlobalExpired +
					" aiMayorActive=" + summary.AiMayorActive +
					" aiMayorExpired=" + summary.AiMayorExpired +
					" aiMayorFuture=" + summary.AiMayorFuture +
					" crewStates=" + summary.CrewStates +
					" crewMayorActive=" + summary.CrewMayorActive +
					" crewJudgeActive=" + summary.CrewJudgeActive +
					" crewBribeExpired=" + summary.CrewBribeExpired +
					" reason=" + summary.Reason);
			}
			catch (Exception ex)
			{
				logger.LogWarning("bribe-state-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void CountAiMayorBribes(object saveData, PoliticalBribeStateSummary summary)
		{
			IDictionary dict = ReadInstanceField(saveData, "AiGangMayorBribeExpireDayByGang") as IDictionary;
			if (dict == null)
			{
				return;
			}

			foreach (DictionaryEntry entry in dict)
			{
				int expireDay = ConvertToInt(entry.Value, -1);
				if (expireDay < 0)
				{
					continue;
				}
				if (summary.Today < expireDay)
				{
					summary.AiMayorActive++;
					summary.AiMayorFuture++;
				}
				else
				{
					summary.AiMayorExpired++;
				}
			}
		}

		private static void CountCrewBribeStates(object saveData, PoliticalBribeStateSummary summary)
		{
			IDictionary dict = ReadInstanceField(saveData, "CrewStates") as IDictionary;
			if (dict == null)
			{
				return;
			}

			foreach (DictionaryEntry entry in dict)
			{
				object state = entry.Value;
				if (state == null)
				{
					continue;
				}

				summary.CrewStates++;
				bool mayorActive = ReadInstanceBool(state, "MayorBribeActive");
				bool judgeActive = ReadInstanceBool(state, "JudgeBribeActive");
				long expireRaw = ReadInstanceLong(state, "BribeExpiresRaw", 0L);
				if (mayorActive)
				{
					summary.CrewMayorActive++;
				}
				if (judgeActive)
				{
					summary.CrewJudgeActive++;
				}
				if ((mayorActive || judgeActive) && expireRaw > 0L && summary.Today >= expireRaw)
				{
					summary.CrewBribeExpired++;
				}
			}
		}

		private static Type FindType(string fullName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				try
				{
					Type type = assembly.GetType(fullName, throwOnError: false);
					if (type != null)
					{
						return type;
					}
				}
				catch
				{
				}
			}

			return null;
		}

		private static object ReadStaticField(Type type, string fieldName)
		{
			return type?.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
		}

		private static object ReadInstanceField(object instance, string fieldName)
		{
			return instance?.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
		}

		private static bool ReadStaticBool(Type type, string fieldName)
		{
			object value = ReadStaticField(type, fieldName);
			return value is bool flag && flag;
		}

		private static int ReadStaticInt(Type type, string fieldName, int fallback)
		{
			return ConvertToInt(ReadStaticField(type, fieldName), fallback);
		}

		private static bool ReadInstanceBool(object instance, string fieldName)
		{
			object value = ReadInstanceField(instance, fieldName);
			return value is bool flag && flag;
		}

		private static long ReadInstanceLong(object instance, string fieldName, long fallback)
		{
			object value = ReadInstanceField(instance, fieldName);
			if (value is long longValue)
			{
				return longValue;
			}
			if (value is int intValue)
			{
				return intValue;
			}

			return fallback;
		}

		private static int ConvertToInt(object value, int fallback)
		{
			if (value is int intValue)
			{
				return intValue;
			}
			if (value is long longValue)
			{
				return (int)longValue;
			}

			return fallback;
		}

		private static int SafeToday()
		{
			try
			{
				return global::Game.Game.ctx?.clock?.Now.days ?? -1;
			}
			catch
			{
				return -1;
			}
		}
	}

	public sealed class PoliticalBribeStateSummary
	{
		public bool GameplayTweaksFound { get; internal set; }
		public int Today { get; internal set; }
		public bool HumanGlobalActive { get; internal set; }
		public int HumanGlobalExpireDay { get; internal set; }
		public bool HumanGlobalExpired { get; internal set; }
		public int AiMayorActive { get; internal set; }
		public int AiMayorExpired { get; internal set; }
		public int AiMayorFuture { get; internal set; }
		public int CrewStates { get; internal set; }
		public int CrewMayorActive { get; internal set; }
		public int CrewJudgeActive { get; internal set; }
		public int CrewBribeExpired { get; internal set; }
		public string Reason { get; internal set; }

		public string FormatBridgeSummary()
		{
			return "bribe-state gameplayTweaks=" + GameplayTweaksFound +
				" today=" + Today +
				" humanGlobalActive=" + HumanGlobalActive +
				" humanGlobalExpireDay=" + HumanGlobalExpireDay +
				" humanGlobalExpired=" + HumanGlobalExpired +
				" aiMayorActive=" + AiMayorActive +
				" aiMayorExpired=" + AiMayorExpired +
				" crewStates=" + CrewStates +
				" crewMayorActive=" + CrewMayorActive +
				" crewJudgeActive=" + CrewJudgeActive +
				" crewBribeExpired=" + CrewBribeExpired +
				" reason=" + (Reason ?? "unknown");
		}
	}
}
