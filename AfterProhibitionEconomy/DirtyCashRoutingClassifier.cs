using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using HarmonyLib;

namespace AfterProhibitionEconomy
{
	internal static class DirtyCashRoutingClassifier
	{
		private const string CompatibilityPluginTypeName = "AfterProhibitionCompatibility.AfterProhibitionCompatibilityPlugin";

		private static MethodInfo _isDirtyCashEconomyActiveMethod;
		private static MethodInfo _isDirtyCashVolumeFixActiveMethod;
		private static MethodInfo _getDirtyCashCompatibilitySummaryMethod;
		private static MethodInfo _getDirtyCashPatchOwnerSummaryMethod;

		internal static DirtyCashRoutingSummary Classify(EntityID buildingId)
		{
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			return Classify(building);
		}

		internal static DirtyCashRoutingSummary Classify(Entity building)
		{
			CompatibilityState compatibility = GetCompatibilityState();
			DirtyCashRoutingSummary summary = new DirtyCashRoutingSummary
			{
				BuildingID = building?.Id ?? EntityID.INVALID,
				BuildingTemplate = GetTemplateString(building),
				CompatibilityAvailable = compatibility.BridgeAvailable,
				ExternalDirtyCashActive = compatibility.DirtyCashEconomyActive,
				ExternalDirtyCashVolumeFixActive = compatibility.DirtyCashVolumeFixActive,
				CompatibilitySummary = compatibility.Summary,
				PatchOwnerSummary = compatibility.PatchOwnerSummary
			};

			if (building?.components?.building == null)
			{
				summary.RouteMode = "none";
				summary.Reason = "missing-building";
				return summary;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			summary.BizID = biz?.Id ?? EntityID.INVALID;
			summary.BizTemplate = GetTemplateString(biz);
			summary.BuildingType = ClassifyBuildingType(building, summary.BuildingTemplate, summary.BizTemplate);
			summary.HumanControlled = building.data?.building != null && building.data.building.controlled.Get().IsHumanPlayer;

			ModulesComponent modules = building.components.modules;
			if (modules == null)
			{
				summary.RouteMode = "none";
				summary.Reason = "missing-modules";
				return summary;
			}

			summary.HasInventory = modules.inventory != null;
			summary.HasDirtyCashBackroom = HasInstalledModuleMatching(modules, IsDirtyCashBackroomModule);
			summary.HasPlayerLegalBusiness = HasInstalledModuleMatching(modules, IsPlayerLegalDirtyCashBusinessModule);
			summary.IsBank = string.Equals(summary.BuildingType, "bank", StringComparison.Ordinal);
			summary.IsWarehouse = string.Equals(summary.BuildingType, "warehouse", StringComparison.Ordinal);
			summary.IsFront = string.Equals(summary.BuildingType, "front", StringComparison.Ordinal) || summary.HasPlayerLegalBusiness;
			summary.IsSafehouse = building.components.building.IsSafehouse;
			summary.IsRoutingCandidate = summary.HumanControlled
				&& (summary.HasDirtyCashBackroom || summary.HasPlayerLegalBusiness || summary.IsBank || summary.IsWarehouse || summary.IsFront || summary.IsSafehouse);
			summary.UsesExternalDirtyCashRouting = summary.IsRoutingCandidate && (compatibility.DirtyCashEconomyActive || compatibility.DirtyCashVolumeFixActive);

			if (!summary.IsRoutingCandidate)
			{
				summary.RouteMode = "none";
				summary.Reason = "not-dirty-cash-routing-candidate";
			}
			else if (compatibility.DirtyCashEconomyActive)
			{
				summary.RouteMode = "external-dirty-cash-economy";
				summary.Reason = "external-dirty-cash-owner";
			}
			else if (compatibility.DirtyCashVolumeFixActive)
			{
				summary.RouteMode = "external-volume-fix";
				summary.Reason = "external-volume-owner";
			}
			else
			{
				summary.RouteMode = "local-gameplaytweaks";
				summary.Reason = "no-external-dirty-cash-owner";
			}

			return summary;
		}

		internal static void LogStartupSamples(string source, ManualLogSource logger, int sampleLimit)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					logger.LogInfo("dirty-cash-routing-summary source=" + source + " unavailable reason=missing-building-cache");
					return;
				}

				int scanned = 0;
				int candidates = 0;
				int external = 0;
				int local = 0;
				int dirtyBackrooms = 0;
				int legalFronts = 0;
				int banks = 0;
				int warehouses = 0;
				int safehouses = 0;
				int humanControlled = 0;
				int logged = 0;
				CompatibilityState compatibility = GetCompatibilityState();

				foreach (Entity building in buildings)
				{
					scanned++;
					DirtyCashRoutingSummary summary = Classify(building);
					if (summary.HumanControlled)
					{
						humanControlled++;
					}
					if (summary.HasDirtyCashBackroom)
					{
						dirtyBackrooms++;
					}
					if (summary.HasPlayerLegalBusiness || summary.IsFront)
					{
						legalFronts++;
					}
					if (summary.IsBank)
					{
						banks++;
					}
					if (summary.IsWarehouse)
					{
						warehouses++;
					}
					if (summary.IsSafehouse)
					{
						safehouses++;
					}
					if (!summary.IsRoutingCandidate)
					{
						continue;
					}

					candidates++;
					if (summary.UsesExternalDirtyCashRouting)
					{
						external++;
					}
					else
					{
						local++;
					}

					if (logged < sampleLimit)
					{
						logged++;
						logger.LogInfo(summary.FormatLogLine(source));
					}
				}

				logger.LogInfo(
					"dirty-cash-routing-summary source=" + source +
					" compatibilityBridge=" + compatibility.BridgeAvailable +
					" externalDirtyCash=" + compatibility.DirtyCashEconomyActive +
					" volumeFix=" + compatibility.DirtyCashVolumeFixActive +
					" scanned=" + scanned +
					" candidates=" + candidates +
					" external=" + external +
					" local=" + local +
					" humanControlled=" + humanControlled +
					" safehouses=" + safehouses +
					" fronts=" + legalFronts +
					" dirtyBackrooms=" + dirtyBackrooms +
					" banks=" + banks +
					" warehouses=" + warehouses +
					" samples=" + logged);
			}
			catch (Exception ex)
			{
				logger.LogWarning("dirty-cash-routing-summary failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static CompatibilityState GetCompatibilityState()
		{
			CompatibilityState state = new CompatibilityState();
			try
			{
				Type compatibilityType = AccessTools.TypeByName(CompatibilityPluginTypeName);
				if (compatibilityType == null)
				{
					state.Summary = "compatibility-bridge unavailable";
					return state;
				}

				_isDirtyCashEconomyActiveMethod = _isDirtyCashEconomyActiveMethod ?? AccessTools.Method(compatibilityType, "IsDirtyCashEconomyActive");
				_isDirtyCashVolumeFixActiveMethod = _isDirtyCashVolumeFixActiveMethod ?? AccessTools.Method(compatibilityType, "IsDirtyCashVolumeFixActive");
				_getDirtyCashCompatibilitySummaryMethod = _getDirtyCashCompatibilitySummaryMethod ?? AccessTools.Method(compatibilityType, "GetDirtyCashCompatibilitySummary");
				_getDirtyCashPatchOwnerSummaryMethod = _getDirtyCashPatchOwnerSummaryMethod ?? AccessTools.Method(compatibilityType, "GetDirtyCashPatchOwnerSummary");

				state.BridgeAvailable = _isDirtyCashEconomyActiveMethod != null || _isDirtyCashVolumeFixActiveMethod != null;
				state.DirtyCashEconomyActive = InvokeBool(_isDirtyCashEconomyActiveMethod);
				state.DirtyCashVolumeFixActive = InvokeBool(_isDirtyCashVolumeFixActiveMethod);
				state.Summary = _getDirtyCashCompatibilitySummaryMethod?.Invoke(null, null) as string ?? string.Empty;
				state.PatchOwnerSummary = _getDirtyCashPatchOwnerSummaryMethod?.Invoke(null, null) as string ?? string.Empty;
			}
			catch (Exception ex)
			{
				state.Summary = "compatibility-bridge error=" + ex.GetType().Name + ":" + ex.Message;
			}

			return state;
		}

		private static bool InvokeBool(MethodInfo method)
		{
			if (method == null)
			{
				return false;
			}

			object value = method.Invoke(null, null);
			return value is bool enabled && enabled;
		}

		private static bool HasInstalledModuleMatching(ModulesComponent modules, Func<IModuleConfig, bool> predicate)
		{
			List<IModule> slots = modules?.GetAllSlotsUnsafe();
			if (slots == null || predicate == null)
			{
				return false;
			}

			foreach (IModule slot in slots)
			{
				IModuleConfig config = slot?.ModuleConfig;
				if (config != null && predicate(config))
				{
					return true;
				}
			}

			return false;
		}

		private static bool IsDirtyCashBackroomModule(IModuleConfig config)
		{
			if (config?.Common?.tags == null || !config.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS))
			{
				return false;
			}

			if (config is ExplanationModuleConfig || config is VehicleModuleConfig)
			{
				return false;
			}

			string id = config.Id.String;
			if (string.IsNullOrEmpty(id))
			{
				return false;
			}

			if (id.StartsWith("explanation-", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("player-garage", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("player-truck-garage", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("garage-", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("truck-garage", StringComparison.OrdinalIgnoreCase) ||
				id.StartsWith("inventory-", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return config is ManufactureModuleConfig || config is ConsumerModuleConfig;
		}

		private static bool IsPlayerLegalDirtyCashBusinessModule(IModuleConfig config)
		{
			if (config?.Common?.tags == null || config is ExplanationModuleConfig || config is VehicleModuleConfig)
			{
				return false;
			}

			string id = config.Id.String;
			if (string.IsNullOrEmpty(id) || !id.StartsWith("player-legal-", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			return config.Common.tags.Contains((Label)"tag-player-legal-biz") &&
				(config is ManufactureModuleConfig || config is ConsumerModuleConfig);
		}

		private static string ClassifyBuildingType(Entity building, string buildingTemplate, string bizTemplate)
		{
			if (building?.components?.building?.IsSafehouse == true)
			{
				return "safehouse";
			}
			if (ContainsAny(buildingTemplate, "bank") || ContainsAny(bizTemplate, "bank"))
			{
				return "bank";
			}
			if (ContainsAny(buildingTemplate, "warehouse", "wholesale") || ContainsAny(bizTemplate, "warehouse", "wholesale"))
			{
				return "warehouse";
			}
			if (ContainsAny(buildingTemplate, "front") || ContainsAny(bizTemplate, "front", "player-legal"))
			{
				return "front";
			}
			return building?.components?.building?.IsBusinessBuildingType == true ? "business" : "other";
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

		private sealed class CompatibilityState
		{
			internal bool BridgeAvailable;
			internal bool DirtyCashEconomyActive;
			internal bool DirtyCashVolumeFixActive;
			internal string Summary = string.Empty;
			internal string PatchOwnerSummary = string.Empty;
		}
	}

	public sealed class DirtyCashRoutingSummary
	{
		public EntityID BuildingID { get; internal set; }
		public EntityID BizID { get; internal set; }
		public string BuildingTemplate { get; internal set; }
		public string BizTemplate { get; internal set; }
		public string BuildingType { get; internal set; }
		public string RouteMode { get; internal set; }
		public string Reason { get; internal set; }
		public bool CompatibilityAvailable { get; internal set; }
		public bool ExternalDirtyCashActive { get; internal set; }
		public bool ExternalDirtyCashVolumeFixActive { get; internal set; }
		public bool HumanControlled { get; internal set; }
		public bool HasInventory { get; internal set; }
		public bool HasDirtyCashBackroom { get; internal set; }
		public bool HasPlayerLegalBusiness { get; internal set; }
		public bool IsBank { get; internal set; }
		public bool IsWarehouse { get; internal set; }
		public bool IsFront { get; internal set; }
		public bool IsSafehouse { get; internal set; }
		public bool IsRoutingCandidate { get; internal set; }
		public bool UsesExternalDirtyCashRouting { get; internal set; }
		public string CompatibilitySummary { get; internal set; }
		public string PatchOwnerSummary { get; internal set; }

		internal string FormatLogLine(string source)
		{
			return "dirty-cash-routing source=" + source +
				" building=" + BuildingID +
				" biz=" + BizID +
				" type=" + (BuildingType ?? "unknown") +
				" mode=" + (RouteMode ?? "none") +
				" reason=" + (Reason ?? "unknown") +
				" externalDirtyCash=" + ExternalDirtyCashActive +
				" volumeFix=" + ExternalDirtyCashVolumeFixActive +
				" human=" + HumanControlled +
				" safehouse=" + IsSafehouse +
				" front=" + IsFront +
				" dirtyBackroom=" + HasDirtyCashBackroom +
				" legalBusiness=" + HasPlayerLegalBusiness;
		}

		internal string FormatBridgeSummary()
		{
			return "dirty-cash-routing building=" + BuildingID +
				" biz=" + BizID +
				" type=" + (BuildingType ?? "unknown") +
				" mode=" + (RouteMode ?? "none") +
				" reason=" + (Reason ?? "unknown") +
				" candidate=" + IsRoutingCandidate +
				" external=" + UsesExternalDirtyCashRouting +
				" compatibilityBridge=" + CompatibilityAvailable +
				" externalDirtyCash=" + ExternalDirtyCashActive +
				" volumeFix=" + ExternalDirtyCashVolumeFixActive;
		}
	}
}
