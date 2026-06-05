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
	internal static class ShopAccessClassifier
	{
		internal static ShopAccessSummary Classify(EntityID buildingId)
		{
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			return Classify(building);
		}

		internal static ShopAccessSummary Classify(Entity building)
		{
			ShopAccessSummary summary = new ShopAccessSummary();
			summary.BuildingID = building?.Id ?? EntityID.INVALID;
			summary.BuildingTemplate = GetTemplateString(building);

			if (building?.components?.building == null)
			{
				summary.Mode = "neither";
				summary.Reason = "missing-building";
				return summary;
			}

			if (!building.components.building.IsBusinessBuildingType)
			{
				summary.Mode = "neither";
				summary.Reason = "not-business-building";
				return summary;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			summary.BizID = biz?.Id ?? EntityID.INVALID;
			summary.BizTemplate = GetTemplateString(biz);
			summary.BusinessType = ClassifyBusinessType(summary.BuildingTemplate, summary.BizTemplate);

			ModulesComponent modules = building.components.modules;
			if (modules == null)
			{
				summary.Mode = "neither";
				summary.Reason = "missing-modules";
				return summary;
			}

			if (modules.inventory == null)
			{
				summary.Mode = "neither";
				summary.Reason = "missing-inventory";
				return summary;
			}

			summary.HasPurchaseProducingModule = HasInstalledPurchaseProducingModule(modules);
			summary.HasBuyStock = HasPositivePlayerOffer(modules, playerBuys: true, playerSells: false);
			summary.HasSellStock = HasPositivePlayerOffer(modules, playerBuys: false, playerSells: true);

			if (summary.HasBuyStock && summary.HasSellStock)
			{
				summary.Mode = "buy-sell";
				summary.Reason = "stock-and-module-valid";
			}
			else if (summary.HasBuyStock)
			{
				summary.Mode = "buy-only";
				summary.Reason = "buy-stock-valid";
			}
			else if (summary.HasSellStock)
			{
				summary.Mode = "sell-only";
				summary.Reason = summary.HasPurchaseProducingModule ? "no-purchase-stock" : "no-buy-offers";
			}
			else
			{
				summary.Mode = "neither";
				summary.Reason = summary.HasPurchaseProducingModule ? "purchase-producing-missing-buy-stock" : "no-buy-or-sell-stock";
			}

			return summary;
		}

		internal static void LogStartupSamples(string source, ManualLogSource logger, int sampleLimit)
		{
			if (logger == null || sampleLimit <= 0)
			{
				return;
			}

			try
			{
				IEnumerable<Entity> buildings = global::Game.Game.ctx?.entityman?.GetCachedEntitiesBuildingsUnsafe();
				if (buildings == null)
				{
					logger.LogInfo("shop-access-summary source=" + source + " unavailable reason=missing-building-cache");
					return;
				}

				int buySell = 0;
				int buyOnly = 0;
				int sellOnly = 0;
				int neither = 0;
				int missingPurchaseStock = 0;
				int banks = 0;
				int warehouses = 0;
				int fronts = 0;
				int logged = 0;

				foreach (Entity building in buildings)
				{
					ShopAccessSummary summary = Classify(building);
					if (!summary.IsBusinessBuilding)
					{
						continue;
					}

					switch (summary.Mode)
					{
					case "buy-sell":
						buySell++;
						break;
					case "buy-only":
						buyOnly++;
						break;
					case "sell-only":
						sellOnly++;
						break;
					default:
						neither++;
						break;
					}

					if (string.Equals(summary.BusinessType, "bank", StringComparison.Ordinal))
					{
						banks++;
					}
					else if (string.Equals(summary.BusinessType, "warehouse", StringComparison.Ordinal))
					{
						warehouses++;
					}
					else if (string.Equals(summary.BusinessType, "front", StringComparison.Ordinal))
					{
						fronts++;
					}

					if (summary.HasPurchaseProducingModule && !summary.HasBuyStock)
					{
						missingPurchaseStock++;
					}

					if (logged < sampleLimit && ShouldLogSample(summary))
					{
						logged++;
						logger.LogInfo(summary.FormatLogLine(source));
					}
				}

				logger.LogInfo(
					"shop-access-summary source=" + source +
					" buySell=" + buySell +
					" buyOnly=" + buyOnly +
					" sellOnly=" + sellOnly +
					" neither=" + neither +
					" missingPurchaseStock=" + missingPurchaseStock +
					" banks=" + banks +
					" warehouses=" + warehouses +
					" fronts=" + fronts +
					" samples=" + logged);
			}
			catch (Exception ex)
			{
				logger.LogWarning("shop-access-summary failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static bool ShouldLogSample(ShopAccessSummary summary)
		{
			return summary.IsBusinessBuilding
				&& (summary.HasPurchaseProducingModule && !summary.HasBuyStock
					|| string.Equals(summary.Mode, "sell-only", StringComparison.Ordinal)
					|| string.Equals(summary.BusinessType, "bank", StringComparison.Ordinal)
					|| string.Equals(summary.BusinessType, "warehouse", StringComparison.Ordinal));
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

		private static string ClassifyBusinessType(string buildingTemplate, string bizTemplate)
		{
			if (ContainsAny(buildingTemplate, "bank") || ContainsAny(bizTemplate, "bank"))
			{
				return "bank";
			}
			if (ContainsAny(buildingTemplate, "warehouse", "wholesale") || ContainsAny(bizTemplate, "warehouse", "wholesale"))
			{
				return "warehouse";
			}
			if (ContainsAny(bizTemplate, "player-legal", "front") || ContainsAny(buildingTemplate, "front"))
			{
				return "front";
			}
			if (ContainsAny(buildingTemplate, "school", "worship", "church") || ContainsAny(bizTemplate, "school", "worship", "church"))
			{
				return "civic";
			}
			return "shop";
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

	public sealed class ShopAccessSummary
	{
		public EntityID BuildingID;
		public EntityID BizID;
		public string BuildingTemplate = "(null)";
		public string BizTemplate = "(null)";
		public string BusinessType = "unknown";
		public string Mode = "neither";
		public string Reason = "unknown";
		public bool HasBuyStock;
		public bool HasSellStock;
		public bool HasPurchaseProducingModule;

		public bool IsBusinessBuilding => BuildingID.IsValid;

		public bool HasBuyAccess => HasBuyStock;

		public bool HasSellAccess => HasSellStock;

		public string FormatBridgeSummary()
		{
			return "building=" + BuildingID.id +
				" biz=" + BizID.id +
				" type=" + BusinessType +
				" mode=" + Mode +
				" reason=" + Reason +
				" buyStock=" + HasBuyStock +
				" sellStock=" + HasSellStock +
				" purchaseProducing=" + HasPurchaseProducingModule +
				" buildingTemplate=" + BuildingTemplate +
				" bizTemplate=" + BizTemplate;
		}

		internal string FormatLogLine(string source)
		{
			return "shop-access source=" + source + " " + FormatBridgeSummary();
		}
	}
}
