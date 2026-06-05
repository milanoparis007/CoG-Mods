using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Game.Core;
using Game.Session.Entities;

namespace AfterProhibitionEconomy
{
	internal static class CivicPurchaseAccessClassifier
	{
		internal static CivicPurchaseAccessSummary Classify(EntityID buildingId)
		{
			ShopAccessSummary shop = ShopAccessClassifier.Classify(buildingId);
			return Classify(shop);
		}

		internal static CivicPurchaseAccessSummary Classify(ShopAccessSummary shop)
		{
			CivicPurchaseAccessSummary summary = new CivicPurchaseAccessSummary();
			if (shop == null)
			{
				summary.Type = "unknown";
				summary.Access = false;
				summary.Reason = "missing-shop-summary";
				return summary;
			}

			summary.BuildingID = shop.BuildingID;
			summary.BizID = shop.BizID;
			summary.Type = shop.BusinessType;
			summary.ShopMode = shop.Mode;
			summary.ShopReason = shop.Reason;
			summary.BuildingTemplate = shop.BuildingTemplate;
			summary.BizTemplate = shop.BizTemplate;
			summary.HasBuyStock = shop.HasBuyStock;
			summary.HasSellStock = shop.HasSellStock;
			summary.HasPurchaseProducingModule = shop.HasPurchaseProducingModule;

			if (string.Equals(shop.BusinessType, "bank", StringComparison.Ordinal))
			{
				ClassifyBank(summary, shop);
				return summary;
			}

			if (string.Equals(shop.BusinessType, "warehouse", StringComparison.Ordinal))
			{
				ClassifyWarehouse(summary, shop);
				return summary;
			}

			summary.Access = false;
			summary.Reason = "not-bank-or-warehouse";
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
					logger.LogInfo("civic-purchase-summary source=" + source + " unavailable reason=missing-building-cache");
					return;
				}

				int banks = 0;
				int banksAccess = 0;
				int banksBlocked = 0;
				int warehouses = 0;
				int warehousesAccess = 0;
				int warehousesBlocked = 0;
				int logged = 0;

				foreach (Entity building in buildings)
				{
					ShopAccessSummary shop = ShopAccessClassifier.Classify(building);
					if (!shop.IsBusinessBuilding)
					{
						continue;
					}

					CivicPurchaseAccessSummary summary = Classify(shop);
					if (!summary.IsCivicPurchaseCandidate)
					{
						continue;
					}

					if (summary.IsBank)
					{
						banks++;
						if (summary.Access)
						{
							banksAccess++;
						}
						else
						{
							banksBlocked++;
						}
					}
					else if (summary.IsWarehouse)
					{
						warehouses++;
						if (summary.Access)
						{
							warehousesAccess++;
						}
						else
						{
							warehousesBlocked++;
						}
					}

					if (logged < sampleLimit)
					{
						logged++;
						logger.LogInfo(summary.FormatLogLine(source));
					}
				}

				logger.LogInfo(
					"civic-purchase-summary source=" + source +
					" banks=" + banks +
					" banksAccess=" + banksAccess +
					" banksBlocked=" + banksBlocked +
					" warehouses=" + warehouses +
					" warehousesAccess=" + warehousesAccess +
					" warehousesBlocked=" + warehousesBlocked +
					" samples=" + logged);
			}
			catch (Exception ex)
			{
				logger.LogWarning("civic-purchase-summary failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static void ClassifyBank(CivicPurchaseAccessSummary summary, ShopAccessSummary shop)
		{
			if (string.Equals(shop.Reason, "missing-modules", StringComparison.Ordinal))
			{
				summary.Access = false;
				summary.Reason = "bank-missing-modules";
				return;
			}

			if (string.Equals(shop.Reason, "missing-inventory", StringComparison.Ordinal))
			{
				summary.Access = false;
				summary.Reason = "bank-missing-inventory";
				return;
			}

			if (shop.HasBuyAccess)
			{
				summary.Access = true;
				summary.Reason = "bank-buy-stock-valid";
				return;
			}

			if (shop.HasPurchaseProducingModule)
			{
				summary.Access = true;
				summary.Reason = "bank-module-valid";
				return;
			}

			summary.Access = true;
			summary.Reason = "bank-template-valid";
		}

		private static void ClassifyWarehouse(CivicPurchaseAccessSummary summary, ShopAccessSummary shop)
		{
			if (string.Equals(shop.Reason, "missing-modules", StringComparison.Ordinal))
			{
				summary.Access = false;
				summary.Reason = "warehouse-missing-modules";
				return;
			}

			if (string.Equals(shop.Reason, "missing-inventory", StringComparison.Ordinal))
			{
				summary.Access = false;
				summary.Reason = "warehouse-missing-inventory";
				return;
			}

			if (shop.HasBuyAccess)
			{
				summary.Access = true;
				summary.Reason = "warehouse-stock-valid";
				return;
			}

			if (shop.HasPurchaseProducingModule)
			{
				summary.Access = false;
				summary.Reason = "warehouse-missing-purchase-stock";
				return;
			}

			summary.Access = false;
			summary.Reason = "warehouse-no-buy-stock";
		}
	}

	public sealed class CivicPurchaseAccessSummary
	{
		public EntityID BuildingID;
		public EntityID BizID;
		public string Type = "unknown";
		public string ShopMode = "neither";
		public string ShopReason = "unknown";
		public string Reason = "unknown";
		public string BuildingTemplate = "(null)";
		public string BizTemplate = "(null)";
		public bool Access;
		public bool HasBuyStock;
		public bool HasSellStock;
		public bool HasPurchaseProducingModule;

		public bool IsBank => string.Equals(Type, "bank", StringComparison.Ordinal);

		public bool IsWarehouse => string.Equals(Type, "warehouse", StringComparison.Ordinal);

		public bool IsCivicPurchaseCandidate => IsBank || IsWarehouse;

		public string FormatBridgeSummary()
		{
			return "type=" + Type +
				" building=" + BuildingID.id +
				" biz=" + BizID.id +
				" access=" + Access +
				" reason=" + Reason +
				" shopMode=" + ShopMode +
				" shopReason=" + ShopReason +
				" buyStock=" + HasBuyStock +
				" sellStock=" + HasSellStock +
				" purchaseProducing=" + HasPurchaseProducingModule +
				" buildingTemplate=" + BuildingTemplate +
				" bizTemplate=" + BizTemplate;
		}

		internal string FormatLogLine(string source)
		{
			return "civic-purchase-access source=" + source + " " + FormatBridgeSummary();
		}
	}
}
