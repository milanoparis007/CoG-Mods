using System;
using BepInEx.Logging;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace AfterProhibitionEconomy
{
	internal static class RouteShopOrderClassifier
	{
		internal static RouteShopOrderSummary Classify(EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)
		{
			Entity building = buildingId.IsValid ? buildingId.FindEntity() : null;
			Label resourceLabel = string.IsNullOrWhiteSpace(resourceId) ? Label.NULL : (Label)resourceId;
			return Classify(building, resourceLabel, playerBuys, quantity, availableCash);
		}

		internal static RouteShopOrderSummary Classify(Entity building, Label resourceId, bool playerBuys, int quantity, int availableCash)
		{
			RouteShopOrderSummary summary = new RouteShopOrderSummary
			{
				BuildingID = building?.Id ?? EntityID.INVALID,
				BuildingTemplate = building?.config?.Template.String ?? "(null)",
				ResourceID = resourceId.IsSet ? resourceId.ToString() : "(none)",
				Mode = playerBuys ? "buy" : "sell",
				RequestedQty = quantity,
				AvailableCash = availableCash
			};

			if (building?.components?.building == null)
			{
				summary.Reason = "missing-building";
				return summary;
			}

			Entity biz = BuildingUtil.FindBizForBuilding(building);
			summary.BizID = biz?.Id ?? EntityID.INVALID;
			summary.BizTemplate = biz?.config?.Template.String ?? "(null)";
			if (biz?.components?.biz == null)
			{
				summary.Reason = "missing-biz";
				return summary;
			}

			ModulesComponent modules = building.components.modules;
			if (modules == null)
			{
				summary.Reason = "missing-modules";
				return summary;
			}
			if (modules.inventory == null)
			{
				summary.Reason = "missing-inventory";
				return summary;
			}
			if (resourceId.IsNotSet)
			{
				summary.Reason = "missing-resource";
				return summary;
			}
			if (quantity <= 0)
			{
				summary.Reason = "empty-quantity";
				return summary;
			}

			Resource resource = FindResourceOrNull(resourceId);
			if (resource == null)
			{
				summary.Reason = "invalid-resource";
				return summary;
			}

			Fixnum requestedQty = quantity;
			BuySellElement? offer = FindOffer(modules, resourceId, playerBuys);
			if (!offer.HasValue)
			{
				summary.Reason = playerBuys ? "buy-resource-not-offered" : "sell-resource-not-accepted";
				return summary;
			}

			summary.AvailableQty = offer.Value.qty;
			summary.StockOrDemandAvailable = offer.Value.qty >= requestedQty;
			summary.UnitPrice = ResolvePrice(Game.Game.ctx?.players?.Human, biz, resource, playerBuys);
			summary.MoneyDelta = summary.UnitPrice * (playerBuys ? -requestedQty : requestedQty);
			summary.RequiredCash = playerBuys ? summary.MoneyDelta.cash.Abs : Fixnum.ZERO;
			summary.CashKnown = availableCash >= 0;
			summary.CashAvailable = !playerBuys || !summary.CashKnown || (Fixnum)availableCash >= summary.RequiredCash;

			if (!summary.StockOrDemandAvailable)
			{
				summary.Reason = playerBuys ? "insufficient-shop-stock" : "insufficient-shop-demand";
				return summary;
			}
			if (!summary.CashAvailable)
			{
				summary.Reason = "insufficient-cash";
				return summary;
			}

			summary.Valid = true;
			summary.Reason = "stock-cash-resource-valid";
			return summary;
		}

		internal static void LogClassification(string source, ManualLogSource logger, EntityID buildingId, string resourceId, bool playerBuys, int quantity, int availableCash)
		{
			if (logger == null)
			{
				return;
			}

			try
			{
				RouteShopOrderSummary summary = Classify(buildingId, resourceId, playerBuys, quantity, availableCash);
				logger.LogInfo(
					"route-shop-order source=" + source +
					" mode=" + summary.Mode +
					" valid=" + summary.Valid +
					" reason=" + summary.Reason +
					" building=" + (summary.BuildingID.IsValid ? summary.BuildingID.ToString() : "invalid") +
					" biz=" + (summary.BizID.IsValid ? summary.BizID.ToString() : "invalid") +
					" resource=" + summary.ResourceID +
					" qty=" + summary.RequestedQty +
					" availableQty=" + summary.AvailableQty +
					" requiredCash=" + summary.RequiredCash +
					" availableCash=" + summary.AvailableCash);
			}
			catch (Exception ex)
			{
				logger.LogWarning("route-shop-order failed source=" + source + " error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}

		private static BuySellElement? FindOffer(ModulesComponent modules, Label resourceId, bool playerBuys)
		{
			foreach (BuySellElement element in modules.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, playerBuys, !playerBuys))
			{
				if (element.item.id == resourceId && element.qty.IsPositive)
				{
					return element;
				}
			}

			return null;
		}

		private static Price ResolvePrice(PlayerInfo player, Entity biz, Resource resource, bool playerBuys)
		{
			try
			{
				if (player == null || biz == null || resource == null)
				{
					return Price.ZERO;
				}

				return BuySellUtils.FindPriceNegotiatedAbs(player, biz, resource, playerBuys);
			}
			catch
			{
				return Price.ZERO;
			}
		}

		private static Resource FindResourceOrNull(Label resourceId)
		{
			try
			{
				return resourceId.IsSet ? Resource.Find(resourceId) : null;
			}
			catch
			{
				return null;
			}
		}
	}

	internal sealed class RouteShopOrderSummary
	{
		public EntityID BuildingID { get; internal set; }

		public string BuildingTemplate { get; internal set; }

		public EntityID BizID { get; internal set; }

		public string BizTemplate { get; internal set; }

		public string ResourceID { get; internal set; }

		public string Mode { get; internal set; }

		public int RequestedQty { get; internal set; }

		public Fixnum AvailableQty { get; internal set; }

		public Price UnitPrice { get; internal set; }

		public Price MoneyDelta { get; internal set; }

		public Fixnum RequiredCash { get; internal set; }

		public int AvailableCash { get; internal set; }

		public bool CashKnown { get; internal set; }

		public bool CashAvailable { get; internal set; }

		public bool StockOrDemandAvailable { get; internal set; }

		public bool Valid { get; internal set; }

		public string Reason { get; internal set; }

		public string FormatBridgeSummary()
		{
			return "mode=" + Mode +
				" valid=" + Valid +
				" reason=" + Reason +
				" building=" + (BuildingID.IsValid ? BuildingID.ToString() : "invalid") +
				" buildingTemplate=" + BuildingTemplate +
				" biz=" + (BizID.IsValid ? BizID.ToString() : "invalid") +
				" bizTemplate=" + BizTemplate +
				" resource=" + ResourceID +
				" qty=" + RequestedQty +
				" availableQty=" + AvailableQty +
				" unitPrice=" + UnitPrice +
				" moneyDelta=" + MoneyDelta.cash +
				" requiredCash=" + RequiredCash +
				" availableCash=" + AvailableCash +
				" cashKnown=" + CashKnown +
				" cashAvailable=" + CashAvailable +
				" stockOrDemandAvailable=" + StockOrDemandAvailable;
		}
	}
}
