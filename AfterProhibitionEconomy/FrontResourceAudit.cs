using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;

namespace AfterProhibitionEconomy
{
	internal static class FrontResourceAudit
	{
		internal static FrontResourceSummary Classify(EntityID buildingId)
		{
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			return Classify(building);
		}

		internal static FrontResourceSummary Classify(Entity building)
		{
			FrontResourceSummary summary = new FrontResourceSummary
			{
				BuildingID = building?.Id ?? EntityID.INVALID,
				BuildingTemplate = GetTemplateString(building)
			};

			if (building?.components?.building == null)
			{
				summary.Reason = "missing-building";
				return summary;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			summary.BizID = biz?.Id ?? EntityID.INVALID;
			summary.BizTemplate = GetTemplateString(biz);
			summary.HumanControlled = building.data?.building != null && building.data.building.controlled.Get().IsHumanPlayer;
			summary.IsBusinessBuilding = building.components.building.IsBusinessBuildingType;
			summary.IsSafehouse = building.components.building.IsSafehouse;
			summary.IsBank = ContainsAny(summary.BuildingTemplate, "bank") || ContainsAny(summary.BizTemplate, "bank");
			summary.IsWarehouse = ContainsAny(summary.BuildingTemplate, "warehouse", "wholesale") || ContainsAny(summary.BizTemplate, "warehouse", "wholesale");

			ModulesComponent modules = building.components.modules;
			if (modules == null)
			{
				summary.Reason = "missing-modules";
				return summary;
			}

			summary.HasInventory = modules.inventory != null;
			List<IModule> slots = modules.GetAllSlotsUnsafe();
			if (slots == null || slots.Count == 0)
			{
				summary.Reason = "missing-slots";
				return summary;
			}

			foreach (IModule module in slots)
			{
				IModuleConfig config = module?.ModuleConfig;
				if (config == null)
				{
					continue;
				}

				summary.ModuleCount++;
				string moduleId = config.Id.String ?? string.Empty;
				if (IsPlayerLegalFrontModule(config))
				{
					summary.PlayerLegalModules++;
					summary.IsFront = true;
				}
				if (IsDirtyCashBackroomModule(config))
				{
					summary.DirtyCashBackroomModules++;
				}
				if (ContainsAny(moduleId, "source-", "conversion-", "sink-", "bootlegger-", "speakeasy", "restaurant", "grocery", "warehouse"))
				{
					summary.ResourceRelevantModules++;
				}
			}

			summary.HasBuyStock = HasPositivePlayerOffer(modules, playerBuys: true, playerSells: false);
			summary.HasSellStock = HasPositivePlayerOffer(modules, playerBuys: false, playerSells: true);
			summary.HasPurchaseProducingModule = HasInstalledPurchaseProducingModule(modules);
			summary.IsResourceCandidate = summary.HumanControlled
				&& (summary.IsFront
					|| summary.IsSafehouse
					|| summary.IsBank
					|| summary.IsWarehouse
					|| summary.DirtyCashBackroomModules > 0
					|| summary.ResourceRelevantModules > 0);

			if (!summary.IsResourceCandidate)
			{
				summary.Reason = "not-front-resource-candidate";
			}
			else if (!summary.HasInventory)
			{
				summary.Reason = "missing-inventory";
			}
			else if (summary.HasPurchaseProducingModule && !summary.HasBuyStock)
			{
				summary.Reason = "purchase-producing-missing-buy-stock";
			}
			else
			{
				summary.Reason = "resource-state-readable";
			}

			return summary;
		}

		internal static void LogStartupAudit(string source, ManualLogSource logger, int sampleLimit)
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
					logger.LogInfo("front-resource-audit source=" + source + " unavailable reason=missing-building-cache");
					return;
				}

				int scanned = 0;
				int candidates = 0;
				int fronts = 0;
				int safehouses = 0;
				int banks = 0;
				int warehouses = 0;
				int dirtyBackrooms = 0;
				int missingInventory = 0;
				int missingBuyStock = 0;
				int readable = 0;
				int logged = 0;

				foreach (Entity building in buildings)
				{
					scanned++;
					FrontResourceSummary summary = Classify(building);
					if (!summary.IsResourceCandidate)
					{
						continue;
					}

					candidates++;
					if (summary.IsFront)
					{
						fronts++;
					}
					if (summary.IsSafehouse)
					{
						safehouses++;
					}
					if (summary.IsBank)
					{
						banks++;
					}
					if (summary.IsWarehouse)
					{
						warehouses++;
					}
					if (summary.DirtyCashBackroomModules > 0)
					{
						dirtyBackrooms++;
					}
					if (!summary.HasInventory)
					{
						missingInventory++;
					}
					if (summary.HasPurchaseProducingModule && !summary.HasBuyStock)
					{
						missingBuyStock++;
					}
					if (string.Equals(summary.Reason, "resource-state-readable", StringComparison.Ordinal))
					{
						readable++;
					}

					if (logged < sampleLimit)
					{
						logged++;
						logger.LogInfo(summary.FormatLogLine(source));
					}
				}

				logger.LogInfo(
					"front-resource-audit source=" + source +
					" scanned=" + scanned +
					" candidates=" + candidates +
					" fronts=" + fronts +
					" safehouses=" + safehouses +
					" banks=" + banks +
					" warehouses=" + warehouses +
					" dirtyBackrooms=" + dirtyBackrooms +
					" missingInventory=" + missingInventory +
					" missingBuyStock=" + missingBuyStock +
					" readable=" + readable +
					" samples=" + logged);
			}
			catch (Exception ex)
			{
				logger.LogWarning("front-resource-audit failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static bool HasInstalledPurchaseProducingModule(ModulesComponent modules)
		{
			if (modules?.bizmodules == null || modules.inventory == null)
			{
				return false;
			}

			foreach (IBizModule bizModule in modules.bizmodules)
			{
				IEnumerable<MfgItem> items = bizModule?.ProduceAllItemsInCurrentRecipe();
				if (items != null && items.Any(item => !item.consumed))
				{
					return true;
				}
			}

			return false;
		}

		private static bool HasPositivePlayerOffer(ModulesComponent modules, bool playerBuys, bool playerSells)
		{
			if (modules?.inventory == null)
			{
				return false;
			}

			try
			{
				return modules
					.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys, playerSells)
					.Any(element => element.qty.IsPositive);
			}
			catch
			{
				return false;
			}
		}

		private static bool IsPlayerLegalFrontModule(IModuleConfig config)
		{
			if (config?.Common?.tags == null)
			{
				return false;
			}

			string id = config.Id.String;
			return !string.IsNullOrEmpty(id)
				&& id.StartsWith("player-legal-", StringComparison.OrdinalIgnoreCase)
				&& config.Common.tags.Contains((Label)"tag-player-legal-biz");
		}

		private static bool IsDirtyCashBackroomModule(IModuleConfig config)
		{
			if (config?.Common?.tags == null || !config.Common.tags.Contains(TagConstants.TAG_SAFEHOUSE_BACKROOMS))
			{
				return false;
			}

			string id = config.Id.String;
			return !string.IsNullOrEmpty(id)
				&& !id.StartsWith("inventory-", StringComparison.OrdinalIgnoreCase)
				&& !id.StartsWith("explanation-", StringComparison.OrdinalIgnoreCase)
				&& (config is ManufactureModuleConfig || config is ConsumerModuleConfig);
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
	}

	public sealed class FrontResourceSummary
	{
		public EntityID BuildingID { get; internal set; }
		public EntityID BizID { get; internal set; }
		public string BuildingTemplate { get; internal set; }
		public string BizTemplate { get; internal set; }
		public bool IsBusinessBuilding { get; internal set; }
		public bool HumanControlled { get; internal set; }
		public bool IsSafehouse { get; internal set; }
		public bool IsBank { get; internal set; }
		public bool IsWarehouse { get; internal set; }
		public bool IsFront { get; internal set; }
		public bool HasInventory { get; internal set; }
		public bool HasBuyStock { get; internal set; }
		public bool HasSellStock { get; internal set; }
		public bool HasPurchaseProducingModule { get; internal set; }
		public bool IsResourceCandidate { get; internal set; }
		public int ModuleCount { get; internal set; }
		public int PlayerLegalModules { get; internal set; }
		public int DirtyCashBackroomModules { get; internal set; }
		public int ResourceRelevantModules { get; internal set; }
		public string Reason { get; internal set; }

		internal string FormatLogLine(string source)
		{
			return "front-resource source=" + source +
				" building=" + BuildingID +
				" biz=" + BizID +
				" reason=" + (Reason ?? "unknown") +
				" human=" + HumanControlled +
				" safehouse=" + IsSafehouse +
				" front=" + IsFront +
				" bank=" + IsBank +
				" warehouse=" + IsWarehouse +
				" inventory=" + HasInventory +
				" buyStock=" + HasBuyStock +
				" sellStock=" + HasSellStock +
				" legalModules=" + PlayerLegalModules +
				" dirtyBackrooms=" + DirtyCashBackroomModules +
				" resourceModules=" + ResourceRelevantModules;
		}

		internal string FormatBridgeSummary()
		{
			return "front-resource building=" + BuildingID +
				" biz=" + BizID +
				" candidate=" + IsResourceCandidate +
				" reason=" + (Reason ?? "unknown") +
				" front=" + IsFront +
				" safehouse=" + IsSafehouse +
				" bank=" + IsBank +
				" warehouse=" + IsWarehouse +
				" inventory=" + HasInventory +
				" buyStock=" + HasBuyStock +
				" sellStock=" + HasSellStock;
		}
	}
}
