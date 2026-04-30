using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.UI.Session.Ledger;

internal static class LedgerReportGenerator
{
	private static readonly Fixnum NUM_MONTHS_FOR_ESTIMATE = 3;

	private static readonly Fixnum NUM_WEEKS_IN_MONTH = 4;

	private const string listEntryIndent = "· ";

	internal static ReportDataModel GetCrew()
	{
		ReportDataModel reportDataModel = new ReportDataModel(Loc.Get("ledger.crewlist.header"), ReportDataModel.SortType.InitialSortAscending, new Column(25, Loc.Get("ledger.crewlist.hr.1"), Loc.Get("ledger.crewlist.mo.1"), Cell.Alignment.Left), new Column(8, Loc.Get("ledger.crewlist.hr.2"), Loc.Get("ledger.crewlist.mo.2")), new Column(2, "", ""), new Column(30, Loc.Get("ledger.crewlist.hr.3"), Loc.Get("ledger.crewlist.mo.3"), Cell.Alignment.Left));
		foreach (CrewAssignment item in Game.ctx.players.Human.crew.AllCrew)
		{
			Entity peep = item.GetPeep();
			string fullName = peep.data.person.FullName;
			Price price = new Price(peep.data.agent.lastPaidSalary);
			string text = (item.IsInBuilding ? Loc.Get("ledger.crewlist.location.manage") : (item.IsInVehicle ? Loc.Get("ledger.crewlist.location.road") : (item.IsDead ? Loc.Get("ledger.crewlist.location.dead") : Loc.Get("ledger.crewlist.location.none"))));
			reportDataModel.AddRow(new Cell(fullName), new Cell(price), new Cell(""), new Cell(text));
		}
		return reportDataModel;
	}

	internal static ReportDataModel GetInventory()
	{
		ReportDataModel reportDataModel = new ReportDataModel(Loc.Get("ledger.inventory.header"), ReportDataModel.SortType.InitialSortAscending, new Column(20, Loc.Get("ledger.inventory.hr.1"), Loc.Get("ledger.inventory.mo.1"), Cell.Alignment.Left), new Column(7, Loc.Get("ledger.inventory.hr.2"), Loc.Get("ledger.inventory.mo.2")), new Column(2, "", ""), new Column(40, Loc.Get("ledger.inventory.hr.3"), Loc.Get("ledger.inventory.mo.3"), Cell.Alignment.Left));
		foreach (CrewAssignment item in Game.ctx.players.Human.crew.AllCrew)
		{
			Entity peep = item.GetPeep();
			if (item.GetVehicle() == null)
			{
				continue;
			}
			foreach (ResourceAndQty content in ModulesUtil.GetInventory(peep).data.contents)
			{
				reportDataModel.AddRow(new Cell(content.FindResource().GetName()), new Cell((int)content.qty), new Cell(""), new Cell(peep.data.person.FullName));
			}
		}
		foreach (EntityID item2 in Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe())
		{
			Entity entity = item2.FindEntity();
			foreach (ResourceAndQty content2 in ModulesUtil.GetInventory(entity).data.contents)
			{
				reportDataModel.AddRow(new Cell(content2.FindResource().GetName()), new Cell((int)content2.qty), new Cell(""), new Cell(BuildingUtil.FindBuildingName(entity.Id)));
			}
		}
		return reportDataModel;
	}

	internal static ReportDataModel GetFronts()
	{
		PlayerInfo human = Game.ctx.players.Human;
		ReportDataModel reportDataModel = new ReportDataModel(Loc.Get("ledger.fronts.header"), ReportDataModel.SortType.InitialSortAscending, new Column(25, Loc.Get("ledger.fronts.hr.1"), Loc.Get("ledger.fronts.mo.1"), Cell.Alignment.Left), new Column(20, Loc.Get("ledger.fronts.hr.2"), Loc.Get("ledger.fronts.mo.2")), new Column(15, Loc.Get("ledger.fronts.hr.3"), Loc.Get("ledger.fronts.mo.3")));
		foreach (OutpostEntry item in human.outposts.GetOutpostEntriesUnsafe())
		{
			string bizname = item.outpostId.FindBuilding().data.building.business.FindEntity().data.biz.bizname;
			Fixnum delta = item.money.Delta;
			Price price = Game.ctx.players.Human.outposts.FindMonthlyOutpostCost(item.outpostId);
			reportDataModel.AddRow(new Cell(bizname), new Cell(delta), new Cell(price));
		}
		return reportDataModel;
	}

	internal static ReportDataModel GetGambling()
	{
		PlayerInfo human = Game.ctx.players.Human;
		ReportDataModel reportDataModel = new ReportDataModel(Loc.Get("ledger.gambling.header"), ReportDataModel.SortType.InitialSortNone, new Column(30, Loc.Get("ledger.gambling.hr.1"), Loc.Get("ledger.gambling.mo.1"), Cell.Alignment.Left), new Column(15, Loc.Get("ledger.gambling.hr.2"), Loc.Get("ledger.gambling.mo.2")), new Column(15, Loc.Get("ledger.gambling.hr.3"), Loc.Get("ledger.gambling.mo.3")), new Column(15, Loc.Get("ledger.gambling.hr.4"), Loc.Get("ledger.gambling.mo.4")));
		List<EntityID> list = (from x in human.territory.GetAllControlledBuildingsUnsafe()
			where x.FindEntity().components.modules.gambling != null
			select x).ToList();
		Cell cell = new Cell("");
		foreach (EntityID item in list)
		{
			GamblingModule gambling = item.FindEntity().components.modules.gambling;
			string gamblingHouseName = BuildingUtil.GetGamblingHouseName(item.FindEntity());
			reportDataModel.AddRow(new Cell(gamblingHouseName), cell, cell, cell);
			foreach (AmenityData amenity in gambling.data.amenities)
			{
				Money cash = new Money(amenity.lastTurnResults.Delta.cash);
				Money cash2 = new Money((from x in amenity.prevTurnResults.Take(3)
					select x.Delta.cash).Sum());
				Money cash3 = new Money((from x in amenity.prevTurnResults.Take(12)
					select x.Delta.cash).Sum());
				reportDataModel.AddRow(new Cell(Loc.Get(amenity.GetAmenityDef().locname)), new Cell(cash), new Cell(cash2), new Cell(cash3));
			}
		}
		return reportDataModel;
	}

	public static Money GetNetWorthOfPlayer(PlayerInfo player)
	{
		Money result = default(Money);
		Money moneyTotal = player.finances.GetMoneyTotal();
		result += moneyTotal;
		foreach (ResourceAndQty datum in MakeResourcesList(player).data)
		{
			Money money = FindWorth(datum);
			result += money;
		}
		foreach (EntityID item2 in player.territory.GetAllControlledBuildingsUnsafe())
		{
			Money item = FindWorth(item2).worth;
			result += item;
		}
		return result;
	}

	internal static ReportDataModel GetNetWorth()
	{
		PlayerInfo human = Game.ctx.players.Human;
		string[] array = new string[4]
		{
			"date",
			Loc.FormatDateLong(Game.ctx.clock.Now),
			"groupname",
			human.social.PlayerGroupName
		};
		object[] replacements = array;
		ReportDataModel reportDataModel = new ReportDataModel(Loc.Get("ledger.networth.header", replacements), ReportDataModel.SortType.InitialSortNone, new Column(45, Loc.Get("ledger.networth.hr.1"), Loc.Get("ledger.networth.mo.1"), Cell.Alignment.Left), new Column(15, Loc.Get("ledger.networth.hr.2"), Loc.Get("ledger.networth.mo.2")));
		reportDataModel.AddRow(new Cell(Loc.Get("ledger.networth.category.assets")), new Cell(""));
		Money moneyTotal = human.finances.GetMoneyTotal();
		reportDataModel.AddRow(new Cell(IndentLoc("ledger.networth.item.cash")), new Cell(moneyTotal));
		Money cash = moneyTotal;
		reportDataModel.AddRow(new Cell(Loc.Get("ledger.networth.category.items")), new Cell(""));
		foreach (ResourceAndQty item in MakeResourcesList(human).data.OrderBy((ResourceAndQty e) => e.FindResource().GetName()))
		{
			Money money = FindWorth(item);
			reportDataModel.AddRow(new Cell(Indent(item.MakeQuantityXNameString())), new Cell(money));
			cash += money;
		}
		reportDataModel.AddRow(new Cell(Loc.Get("ledger.networth.category.operations")), new Cell(""));
		foreach (EntityID item2 in human.territory.GetAllControlledBuildingsUnsafe())
		{
			var (money2, input) = FindWorth(item2);
			reportDataModel.AddRow(new Cell(Indent(input)), new Cell(money2));
			cash += money2;
		}
		reportDataModel.AddRow(new Cell(Loc.Get("ledger.networth.item.line")), new Cell(""));
		reportDataModel.AddRow(new Cell(Loc.Get("ledger.networth.item.total")), new Cell(cash));
		return reportDataModel;
	}

	private static (Money worth, string name) FindWorth(EntityID buildingId)
	{
		Entity entity = buildingId.FindEntity();
		Money item = Money.ZERO;
		GamblingModule gambling = entity.components.modules.gambling;
		if (gambling != null)
		{
			ModQuery query = gambling.MakeManagerBasedModQuery(Game.ctx.players.Human, entity);
			foreach (AmenityData amenity in gambling.data.amenities)
			{
				item += FindWorth(amenity, query) * NUM_MONTHS_FOR_ESTIMATE;
			}
			return (worth: item, name: Loc.Get(gambling.config.common.display.locname));
		}
		IModule module = entity?.components.modules?.FindBackroomModule();
		if (module == null)
		{
			return (worth: Money.ZERO, name: null);
		}
		if (module is ConsumerModule consumerModule)
		{
			ConsumerRecipe consumerRecipe = consumerModule?.config?.sink;
			if (consumerRecipe != null)
			{
				Fixnum fixnum = (Fixnum)((consumerRecipe.consumeDayz <= 0) ? 1f : (30f / (float)consumerRecipe.consumeDayz));
				item = FindWorth(consumerRecipe.consume) * fixnum * NUM_MONTHS_FOR_ESTIMATE;
			}
		}
		else if (module is ManufactureModule { CurrentRecipe: { } currentRecipe })
		{
			Fixnum fixnum2 = (Fixnum)((currentRecipe.produceConsumeDayz <= 0) ? 1f : (30f / (float)currentRecipe.produceConsumeDayz));
			item = FindWorth(currentRecipe.produce) * fixnum2 * NUM_MONTHS_FOR_ESTIMATE;
		}
		return (worth: item, name: Loc.Get(module.ModuleConfig?.Common?.display?.locname));
	}

	private static Money FindWorth(AmenityData amenity, ModQuery query)
	{
		AmenityDef amenityDef = amenity.GetAmenityDef();
		if (amenityDef.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Mod)
		{
			return new Money(0);
		}
		Fixnum fixnum = (amenityDef.behavior.minBetPerPlayer.Evaluate(query) + amenityDef.behavior.maxBetPerPlayer.Evaluate(query)) / 2;
		Fixnum fixnum2 = 0;
		if (amenityDef.behavior.maxCustomers != null)
		{
			fixnum2 = amenityDef.behavior.maxCustomers.Evaluate(query);
		}
		else if (amenityDef.behavior.slots != null)
		{
			fixnum2 = amenityDef.behavior.slots.slotCount.Evaluate(query);
		}
		if (amenityDef.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Rake)
		{
			return new Money(new Fixnum((float)fixnum * (float)fixnum2 * ((float)amenityDef.behavior.rake.rakePercent.Evaluate(query) / 100f)));
		}
		float num = (float)(amenityDef.behavior.playerWinChancePercent.Evaluate(query) / 100);
		Fixnum fixnum3 = amenityDef.behavior.payoutMultiplier.Evaluate(query);
		Fixnum fixnum4 = new Fixnum((float)fixnum2 * num);
		Fixnum fixnum5 = fixnum2 - fixnum4;
		Fixnum fixnum6 = fixnum * fixnum5;
		Fixnum fixnum7 = fixnum4 * fixnum * fixnum3;
		return new Money((fixnum6 - fixnum7) * NUM_WEEKS_IN_MONTH);
	}

	private static Money FindWorth(ResourceAndQty item)
	{
		if (item.FindResource().IsModuleGroup)
		{
			return FindWorth(item.FindResource().groupmembers.Select((Label x) => new ResourceAndQty(x, item.qty)).ToList());
		}
		return item.FindResource().GetPriceWithMultiplier(playerBuying: false, item.qty, PlayerID.HumanPlayer).Abs.AsMoney;
	}

	private static Money FindWorth(List<ResourceAndQty> list)
	{
		return new Money(list.Sum((ResourceAndQty item) => FindWorth(item).cash.IntFloor()));
	}

	private static string Indent(string input)
	{
		return "· " + input;
	}

	private static string IndentLoc(string key)
	{
		return Indent(Loc.Get(key));
	}

	private static ResourceAndQtyList MakeResourcesList(PlayerInfo player)
	{
		ResourceAndQtyList resourceAndQtyList = new ResourceAndQtyList();
		foreach (EntityID allVehicle in player.crew.AllVehicles)
		{
			foreach (ResourceAndQty content in ModulesUtil.GetInventory(allVehicle).data.contents)
			{
				resourceAndQtyList.Increment(content);
			}
		}
		foreach (EntityID item in player.territory.GetAllControlledBuildingsUnsafe())
		{
			foreach (ResourceAndQty content2 in ModulesUtil.GetInventory(item).data.contents)
			{
				resourceAndQtyList.Increment(content2);
			}
		}
		return resourceAndQtyList;
	}
}
