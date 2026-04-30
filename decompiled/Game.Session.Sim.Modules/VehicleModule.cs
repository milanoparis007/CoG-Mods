using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Popups;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class VehicleModule : Module<VehicleModule, VehicleModuleConfig, VehicleModuleData>, IBizModule, IModule
{
	public struct OwnedBizRepairInfo
	{
		public bool canRepair;

		public bool canAfford;

		public Price repairCost;

		public string infoText;

		public string buttonText;

		public string moText;
	}

	public struct VehicleForSale
	{
		public Label template;

		public string displayName;

		public string displayDesc;

		public Price salePrice;
	}

	public Label BizModuleID => config.id;

	public bool IsInteresting => config.interesting;

	public BizModuleLocData LocData => config.vehicleModuleLoc;

	public override void Initialize(ModuleInitData init)
	{
		base.Initialize(init);
		if (init.IsCreated)
		{
			data.lastUpdate = Game.ctx.clock.Now;
			data.numRepairs = 0;
		}
	}

	public override bool IsEnabled(SimTime time)
	{
		return time.days >= data.EnableTime.days;
	}

	public override ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled)
	{
		ModuleResult result = ModuleResult.Default;
		if (!enabled)
		{
			data.lastUpdate = time;
			return result;
		}
		Entity container = q.container;
		MaybeInformAboutConstruction(container, time, initial);
		data.lastUpdate = time;
		return result;
	}

	public IEnumerable<MfgItem> ProduceAllItemsInCurrentRecipe()
	{
		return new List<MfgItem>();
	}

	public DeliveryInfo ProduceDeliveryInfo(Label resId, ModuleQuery q)
	{
		return default(DeliveryInfo);
	}

	public Fixnum ProduceBuyCap(Label resId, ModuleQuery q)
	{
		return default(Fixnum);
	}

	public (Price price, string explanation) FindRepairCost(VisitState visit, ModQuery q, bool explain)
	{
		Fixnum obj = config.repairInfo?.fullHealPrice.Evaluate(q) ?? ((Fixnum)0);
		float value = 1f - visit.vehicle.components.mobile.CurrentHealthAsFraction;
		Fixnum cash = (obj * new Fixnum(value)).PosCeilingNegFloor();
		return new ValueTuple<Price, string>(item2: explain ? config.repairInfo.fullHealPrice.Explain(q, addHeader: false) : null, item1: new Price(cash));
	}

	public OwnedBizRepairInfo MakeRepairInfo(VisitState visit)
	{
		string infoText = Loc.Get("module.vehicle-repair.available");
		if (visit.vehicle == null)
		{
			return new OwnedBizRepairInfo
			{
				canRepair = false,
				infoText = infoText,
				buttonText = Loc.Get("module.vehicle-repair.no-vehicle"),
				moText = Loc.Get("module.vehicle-repair.no-vehicle.mo")
			};
		}
		VehicleSettings.HealthInfo healthInfo = visit.vehicle.components.mobile.FindHealthInfo();
		string text = Loc.Percentage(visit.vehicle.components.mobile.CurrentHealthAsFraction);
		string text2 = Loc.Get("module.vehicle-repair.condition", "percent", text);
		if (!healthInfo.showbar)
		{
			return new OwnedBizRepairInfo
			{
				canRepair = false,
				infoText = infoText,
				buttonText = Loc.Get("module.vehicle-repair.condition-good"),
				moText = Loc.Get("module.vehicle-repair.condition-good.mo", "conditionInfo", text2)
			};
		}
		if (config.repairInfo == null)
		{
			return new OwnedBizRepairInfo
			{
				canRepair = false,
				infoText = infoText,
				buttonText = Loc.Get("module.vehicle-repair.unavailable"),
				moText = Loc.Get("module.vehicle-repair.unavailable.mo", "conditionInfo", text2)
			};
		}
		ModQuery q = visit.MakeCrewModQuery();
		(Price price, string explanation) tuple = FindRepairCost(visit, q, explain: true);
		Price item = tuple.price;
		string item2 = tuple.explanation;
		bool canAfford = visit.GetPlayer().finances.CanChangeMoneyOnCrew(visit, item);
		string buttonText = Loc.Get("module.vehicle-repair.price", "price", Loc.Price(item, abs: true));
		string text3 = Loc.Get("module.vehicle-repair.price.mo", "conditionInfo", text2, "price", Loc.Price(item, abs: true));
		if (item2 != "")
		{
			text3 = text3 + Loc.Get("module.vehicle-repair.price.explanation") + item2;
		}
		return new OwnedBizRepairInfo
		{
			canRepair = true,
			canAfford = canAfford,
			infoText = infoText,
			buttonText = buttonText,
			moText = text3
		};
	}

	public void StartRepairVehicleAtHome(VisitState visit)
	{
		ModQuery q = visit.MakeCrewModQuery();
		var (price, _) = FindRepairCost(visit, q, explain: false);
		visit.GetPlayer().finances.CanChangeMoneyOnCrew(visit, price);
		OkPopup.ShowOkCancel(Loc.Get("module.vehicle-repair.confirm", "price", Loc.Price(price, abs: true)), delegate
		{
			FinishRepairVehicle(visit, price, deselect: true);
		}, delegate
		{
		});
	}

	public void StartRepairVehicleDuringVisit(VisitState visit, Price price)
	{
		FinishRepairVehicle(visit, price, deselect: false);
	}

	private void FinishRepairVehicle(VisitState visit, Price price, bool deselect)
	{
		if (deselect)
		{
			Game.ctx.selection.ClearActive();
		}
		visit.GetPlayer().finances.DoChangeMoneyOnCrew(visit, price, MoneyReason.VehicleMaintenance);
		visit.vehicle.components.mobile.SetHealthToMax(visit.crew);
		TimerUtil.RunNextFrame(delegate
		{
			OkPopup.Show(Loc.Get("module.vehicle-repair.finish"));
		});
		data.numRepairs++;
	}

	public (Price price, string explanation) FindBuyBackPrice(VisitState visit, ModQuery q, bool explain)
	{
		Fixnum basePurchasePrice = visit.vehicle.config.mobile.basePurchasePrice;
		Fixnum cash = config.buyBackInfo?.pricemods.Evaluate(q, basePurchasePrice).PosCeilingNegFloor() ?? ((Fixnum)0);
		return new ValueTuple<Price, string>(item2: explain ? config.buyBackInfo.pricemods.Explain(q, basePurchasePrice) : null, item1: new Price(cash));
	}

	public void PerformVehicleBuyFromPlayer(VisitState visit, Price buyBackPrice)
	{
		PlayerInfo player = visit.GetPlayer();
		PlayerFinances finances = player.finances;
		Fixnum cash = visit.vehicle.components.modules.inventory.data.money.cash;
		finances.DoChangeMoney(visit.vehicle, new Price(-cash), MoneyReason.Other);
		finances.DoChangeMoneyOnSafehouse(new Price(cash), MoneyReason.Other);
		EntityID peepId = visit.peep.Id;
		_ = visit.vehicle.Id;
		Game.ctx.selection.ClearActive();
		TimerUtil.RunNextFrame(delegate
		{
			finances.DoChangeMoneyOnSafehouse(buyBackPrice, MoneyReason.VehicleBuyFromPlayer);
			player.crew.UnassignCrewAndDestroyVehicle(peepId);
			PersonInfoUtil.TweenCameraToEntity(peepId);
			OkPopup.Show(Loc.Get("module.vehicle.sell"));
		});
	}

	private VehicleModuleConfig.VehicleTypeInfo FindSellInfoForVehicle(Label template)
	{
		foreach (VehicleModuleConfig.VehicleTypeInfo vehicle in config.sellInfo.vehicles)
		{
			if (vehicle.entities.ContainsFast(template))
			{
				return vehicle;
			}
		}
		return null;
	}

	public VehicleForSale? FindVehicleForSaleOrNull(VisitState visit)
	{
		if (config.sellInfo == null || config.sellInfo.vehicles == null)
		{
			return null;
		}
		using ListPool<Label>.PooledBlockList pooledBlockList = ListPool<Label>.Allocate();
		foreach (VehicleModuleConfig.VehicleTypeInfo vehicle in config.sellInfo.vehicles)
		{
			if (vehicle.visreqs == null || vehicle.visreqs.AllPass(visit))
			{
				pooledBlockList.AddRange(vehicle.entities);
			}
		}
		if (pooledBlockList.Count == 0)
		{
			return null;
		}
		Label id = pooledBlockList.LastOrDefaultFast();
		if (pooledBlockList.Count > 1)
		{
			id = new SplitMix64((uint)(Game.ctx.clock.Now.ToDate().Month ^ visit.building.Id.index)).PickElement(pooledBlockList);
		}
		ModQuery q = visit.MakeCrewModQuery();
		return MakeVehicleForSale(q, id);
	}

	private VehicleForSale? MakeVehicleForSale(ModQuery q, Label id)
	{
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(id);
		VehicleModuleConfig.VehicleTypeInfo vehicleTypeInfo = FindSellInfoForVehicle(id);
		if (entityConfig == null || vehicleTypeInfo == null)
		{
			return null;
		}
		Fixnum basePurchasePrice = entityConfig.mobile.basePurchasePrice;
		int num = vehicleTypeInfo.pricemods.Evaluate(q, basePurchasePrice).RoundCoarse() * -1;
		return new VehicleForSale
		{
			template = id,
			salePrice = num,
			displayName = entityConfig.mobile.GetName(),
			displayDesc = entityConfig.mobile.GetDesc()
		};
	}

	internal void PerformVehicleSellToPlayer(VisitState visit, VehicleForSale info)
	{
		visit.GetPlayer().finances.DoChangeMoneyOnCrew(visit, info.salePrice, MoneyReason.VehicleMaintenance);
		Entity entity = visit.GetPlayer().crew.CreateAndTrackVehicleAtSafehouse(info.template);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.VEHICLE_NEW, TickerTitle.VEHICLE_NEW, Loc.Get("module.vehicle.buy", "name", info.displayName), entity.Id);
		Game.ctx.simman.hints.ShowVehicleHint();
	}
}
