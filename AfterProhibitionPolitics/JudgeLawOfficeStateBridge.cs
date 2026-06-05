using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace AfterProhibitionPolitics
{
	internal static class JudgeLawOfficeStateBridge
	{
		internal static JudgeLawOfficeStateSummary Capture()
		{
			JudgeLawOfficeStateSummary summary = new JudgeLawOfficeStateSummary
			{
				Today = SafeToday(),
				Reason = "unreadable"
			};

			try
			{
				AuditLawOffices(summary);
				AuditGameplayTweaksLegalState(summary);
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
				JudgeLawOfficeStateSummary summary = Capture();
				logger.LogInfo(
					"judge-law-office-audit source=" + source +
					" gameplayTweaks=" + summary.GameplayTweaksFound +
					" today=" + summary.Today +
					" crewStates=" + summary.CrewStates +
					" judgeActive=" + summary.JudgeBribeActive +
					" localHeatActive=" + summary.LocalHeatActive +
					" localHeatMediumPlus=" + summary.LocalHeatMediumPlus +
					" localHeatHigh=" + summary.LocalHeatHigh +
					" fedsIncoming=" + summary.FedsIncoming +
					" caseDismissed=" + summary.CaseDismissed +
					" extraJailYears=" + summary.ExtraJailYears +
					" retainers=" + summary.Retainers +
					" retainerConfirmed=" + summary.RetainerConfirmed +
					" retainerUnconfirmed=" + summary.RetainerUnconfirmed +
					" retainerTotal=" + summary.RetainerTotal +
					" retainer20kPlus=" + summary.Retainer20kPlus +
					" lawOfficeTemplates=" + summary.LawOfficeTemplateMatches +
					" lawOfficeModules=" + summary.LawOfficeModuleMatches +
					" lawOfficePlayerOwned=" + summary.LawOfficePlayerOwned +
					" lawOfficeScanErrors=" + summary.LawOfficeScanErrors +
					" reason=" + summary.Reason);
			}
			catch (Exception ex)
			{
				logger.LogWarning("judge-law-office-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void AuditGameplayTweaksLegalState(JudgeLawOfficeStateSummary summary)
		{
			Type pluginType = FindType("GameplayTweaks.GameplayTweaksPlugin");
			if (pluginType == null)
			{
				return;
			}

			summary.GameplayTweaksFound = true;
			object saveData = ReadStaticField(pluginType, "SaveData");
			IDictionary crewStates = ReadInstanceField(saveData, "CrewStates") as IDictionary;
			if (crewStates == null)
			{
				return;
			}

			foreach (DictionaryEntry entry in crewStates)
			{
				object state = entry.Value;
				if (state == null)
				{
					continue;
				}

				summary.CrewStates++;
				if (ReadInstanceBool(state, "JudgeBribeActive"))
				{
					summary.JudgeBribeActive++;
				}
				if (ReadInstanceBool(state, "FedsIncoming"))
				{
					summary.FedsIncoming++;
				}
				if (ReadInstanceBool(state, "CaseDismissed"))
				{
					summary.CaseDismissed++;
				}

				int extraYears = ReadInstanceInt(state, "ExtraJailYears", 0);
				if (extraYears > 0)
				{
					summary.ExtraJailYears++;
				}

				string localHeat = ReadInstanceEnumName(state, "LocalHeatLevel");
				if (!string.IsNullOrEmpty(localHeat) && !string.Equals(localHeat, "None", StringComparison.OrdinalIgnoreCase))
				{
					summary.LocalHeatActive++;
				}
				if (IsHeatAtLeast(localHeat, "Medium"))
				{
					summary.LocalHeatMediumPlus++;
				}
				if (IsHeatAtLeast(localHeat, "High"))
				{
					summary.LocalHeatHigh++;
				}

				int retainer = ReadInstanceInt(state, "LawyerRetainer", 0);
				bool confirmed = ReadInstanceBool(state, "LawyerRetainerConfirmed");
				if (retainer > 0)
				{
					summary.Retainers++;
					summary.RetainerTotal += retainer;
					if (retainer >= 20000)
					{
						summary.Retainer20kPlus++;
					}
					if (confirmed)
					{
						summary.RetainerConfirmed++;
					}
					else
					{
						summary.RetainerUnconfirmed++;
					}
				}
			}
		}

		private static void AuditLawOffices(JudgeLawOfficeStateSummary summary)
		{
			try
			{
				IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					return;
				}

				foreach (Entity building in buildings)
				{
					try
					{
						summary.LawOfficeScanned++;
						Entity biz = BuildingUtil.FindBizForBuilding(building);
						string buildingTemplate = GetTemplateString(building);
						string bizTemplate = GetTemplateString(biz);
						bool templateMatch = ContainsAny(buildingTemplate, "lawoffice", "law-office", "law_office")
							|| ContainsAny(bizTemplate, "lawoffice", "law-office", "law_office");
						bool moduleMatch = HasLawOfficeModule(building.components?.modules);

						if (templateMatch)
						{
							summary.LawOfficeTemplateMatches++;
						}
						if (moduleMatch)
						{
							summary.LawOfficeModuleMatches++;
						}
						if ((templateMatch || moduleMatch) && building.data?.building != null && building.data.building.controlled.Get().IsHumanPlayer)
						{
							summary.LawOfficePlayerOwned++;
						}
					}
					catch
					{
						summary.LawOfficeScanErrors++;
					}
				}
			}
			catch
			{
				summary.LawOfficeScanErrors++;
			}
		}

		private static bool HasLawOfficeModule(ModulesComponent modules)
		{
			try
			{
				List<IModule> slots = modules?.GetAllSlotsUnsafe();
				if (slots == null)
				{
					return false;
				}

				foreach (IModule module in slots)
				{
					string id = module?.ModuleConfig?.Id.String;
					if (ContainsAny(id, "lawoffice", "law-office", "law_office", "legal-lawoffice"))
					{
						return true;
					}
				}
			}
			catch
			{
			}

			return false;
		}

		private static bool IsHeatAtLeast(string heat, string threshold)
		{
			int heatRank = HeatRank(heat);
			return heatRank > 0 && heatRank >= HeatRank(threshold);
		}

		private static int HeatRank(string heat)
		{
			if (string.Equals(heat, "Low", StringComparison.OrdinalIgnoreCase))
			{
				return 1;
			}
			if (string.Equals(heat, "Medium", StringComparison.OrdinalIgnoreCase))
			{
				return 2;
			}
			if (string.Equals(heat, "High", StringComparison.OrdinalIgnoreCase))
			{
				return 3;
			}

			return 0;
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

		private static bool ReadInstanceBool(object instance, string fieldName)
		{
			object value = ReadInstanceField(instance, fieldName);
			return value is bool flag && flag;
		}

		private static int ReadInstanceInt(object instance, string fieldName, int fallback)
		{
			object value = ReadInstanceField(instance, fieldName);
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

		private static string ReadInstanceEnumName(object instance, string fieldName)
		{
			object value = ReadInstanceField(instance, fieldName);
			return value?.ToString();
		}

		private static bool ContainsAny(string value, params string[] needles)
		{
			if (string.IsNullOrEmpty(value) || needles == null)
			{
				return false;
			}

			foreach (string needle in needles)
			{
				if (!string.IsNullOrEmpty(needle) && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}

			return false;
		}

		private static string GetTemplateString(Entity entity)
		{
			return entity?.config?.Template.String ?? "(null)";
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

	public sealed class JudgeLawOfficeStateSummary
	{
		public bool GameplayTweaksFound { get; internal set; }
		public int Today { get; internal set; }
		public int CrewStates { get; internal set; }
		public int JudgeBribeActive { get; internal set; }
		public int LocalHeatActive { get; internal set; }
		public int LocalHeatMediumPlus { get; internal set; }
		public int LocalHeatHigh { get; internal set; }
		public int FedsIncoming { get; internal set; }
		public int CaseDismissed { get; internal set; }
		public int ExtraJailYears { get; internal set; }
		public int Retainers { get; internal set; }
		public int RetainerConfirmed { get; internal set; }
		public int RetainerUnconfirmed { get; internal set; }
		public int RetainerTotal { get; internal set; }
		public int Retainer20kPlus { get; internal set; }
		public int LawOfficeScanned { get; internal set; }
		public int LawOfficeTemplateMatches { get; internal set; }
		public int LawOfficeModuleMatches { get; internal set; }
		public int LawOfficePlayerOwned { get; internal set; }
		public int LawOfficeScanErrors { get; internal set; }
		public string Reason { get; internal set; }

		public string FormatBridgeSummary()
		{
			return "judge-law-office gameplayTweaks=" + GameplayTweaksFound +
				" today=" + Today +
				" crewStates=" + CrewStates +
				" judgeActive=" + JudgeBribeActive +
				" localHeatActive=" + LocalHeatActive +
				" localHeatMediumPlus=" + LocalHeatMediumPlus +
				" fedsIncoming=" + FedsIncoming +
				" retainers=" + Retainers +
				" retainerConfirmed=" + RetainerConfirmed +
				" retainerTotal=" + RetainerTotal +
				" lawOfficeTemplates=" + LawOfficeTemplateMatches +
				" lawOfficeModules=" + LawOfficeModuleMatches +
				" lawOfficePlayerOwned=" + LawOfficePlayerOwned +
				" reason=" + (Reason ?? "unknown");
		}
	}
}
