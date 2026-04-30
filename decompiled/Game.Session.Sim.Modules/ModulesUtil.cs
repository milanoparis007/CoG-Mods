using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public static class ModulesUtil
{
	public struct ScheduledDeliveryResult
	{
		public Resource res;

		public QtyAndDir qtyAndDir;

		public Price cash;

		public Entity building;
	}

	public struct ModuleAndItem
	{
		public IModule module;

		public MfgItem item;

		public bool IsValid => module != null;

		public bool IsNotValid => module == null;
	}

	public static string GAMBLING_TAG = "tag-res-gambling";

	private static readonly IList<IModuleConfig> EMPTY = new List<IModuleConfig>();

	public static IEnumerable<EntityID> GetControlledBuildingsWithoutManagers(PlayerID pid)
	{
		return from info in pid.FindPlayer().territory.GetAllControlledBuildingsUnsafe().Select(MakeModuleQuery)
			where info.manager == null
			select info.container.Id;
	}

	public static ModuleQuery MakeModuleQuery(EntityID buildingId)
	{
		return MakeModuleQuery(buildingId.FindEntity());
	}

	public static ModuleQuery MakeModuleQuery(Entity container)
	{
		Entity npc = BuildingUtil.FindOwnerOrManagerForAnyBuilding(container);
		(PlayerID ownerpid, Entity manager) managerOrNull = GetManagerOrNull(container);
		PlayerID item = managerOrNull.ownerpid;
		Entity item2 = managerOrNull.manager;
		NodeID nodeId = ((container.components.building != null) ? container.components.board.GetNodeID() : NodeID.INVALID);
		return new ModuleQuery(item, container, nodeId, npc, item2);
	}

	public static (PlayerID ownerpid, Entity manager) GetManagerOrNull(Entity building)
	{
		bool num = building.components.building != null;
		PlayerID playerID = building.components.building?.GetControllingPlayer() ?? PlayerID.INVALID;
		Entity item = ((num && playerID.IsAnyPlayer) ? playerID.FindPlayer().crew.GetCrewForTarget(building.Id).GetPeep() : null);
		return (ownerpid: playerID, manager: item);
	}

	internal static void GiveXPToManager(ModuleQuery q, ModuleResult result, int crossed)
	{
		if (q.manager == null || crossed <= 0)
		{
			return;
		}
		bool num = (result & ModuleResult.MfgCompleted) != 0;
		bool flag = (result & ModuleResult.ConsCompleted) != 0;
		if (num || flag)
		{
			for (int i = 0; i < crossed; i++)
			{
				q.manager.components.agent.AddXP(XPSource.FromModule);
			}
		}
	}

	public static IEnumerable<IModuleConfig> FindAllModuleDefsExpensive()
	{
		return from config in Game.ctx.entityman.GetCachedConfigsByComponentUnsafe<DefModuleConfig>()
			where config.defmodule != null && config.defmodule.def != null
			select config.defmodule.def;
	}

	public static IEnumerable<IModuleConfig> FindAllGamblingModuleDefsExpensive()
	{
		return from def in FindAllModuleDefsExpensive()
			where def.Common.tags.Contains(new Label(GAMBLING_TAG))
			select def;
	}

	public static IModuleConfig FindModuleDef(Label moduleId)
	{
		return Game.ctx.entityman.FindTemplate(moduleId)?.defmodule?.def;
	}

	public static List<IBizModule> GetBizModules(EntityID buildingId)
	{
		return GetBizModules(buildingId.FindEntity());
	}

	public static List<IBizModule> GetBizModules(Entity building)
	{
		if (building == null || building.Id.IsNotValid || building.components.building == null || building.components.modules == null)
		{
			return null;
		}
		return building.components.modules.bizmodules;
	}

	public static InventoryModule GetInventory(Entity entity)
	{
		if (entity == null)
		{
			return null;
		}
		InventoryModule inventoryModule = entity.components.modules?.inventory;
		if (inventoryModule != null)
		{
			return inventoryModule;
		}
		if (entity.components.agent != null)
		{
			return entity.components.agent.FindCrewAssignment().GetVehicle()?.components.modules.inventory;
		}
		return null;
	}

	public static string GetInventoryFullPercent(Entity entity)
	{
		return Loc.Percentage(GetInventory(entity).GetUsedCapacityAsPercent());
	}

	public static InventoryModule GetInventory(CrewAssignment crew)
	{
		return GetInventory(crew.GetTarget());
	}

	public static InventoryModule GetInventory(EntityID eid)
	{
		return GetInventory(eid.FindEntity());
	}

	public static bool HasInventory(Entity entity)
	{
		return GetInventory(entity) != null;
	}

	public static bool HasModule(Entity entity, Label moduleId)
	{
		return entity.components.modules?.HasModuleInstalled(moduleId) ?? false;
	}

	public static AddModuleDef MakeAddModuleDef(IModuleConfig config, VisitState visit)
	{
		return new AddModuleDef
		{
			config = config,
			passesReqs = PassesReqs(config, visit),
			passesVisreqs = PassesVisReqs(config, visit)
		};
	}

	private static bool PassesVisReqs(IModuleConfig config, VisitState visit)
	{
		return config.Common?.visreqs?.AllPass(visit) ?? true;
	}

	private static bool PassesReqs(IModuleConfig config, VisitState visit)
	{
		return config.Common?.reqs?.AllPass(visit) ?? true;
	}

	public static void ClearInventoryResourcesAndCash(InventoryModule module, PlayerInfo owner)
	{
		InventoryModuleData data = module.data;
		owner.finances.DoChangeMoney(data, new Price(-1 * data.money.cash), MoneyReason.Other);
		data.ClearResources();
	}

	public static bool WasJustEnabled(SimTime time, SimTime enableTime)
	{
		SimTime simTime = time;
		SimTime simTime2 = time.IncrementDays(-Game.ctx.clock.DaysPerTurn);
		bool num = simTime.days >= enableTime.days;
		bool flag = simTime2.days >= enableTime.days;
		if (num)
		{
			return !flag;
		}
		return false;
	}

	public static IEnumerable<IModuleConfig> FindUpgradeSet(IModuleConfig example)
	{
		Label tag = example.Common.upgradetag;
		if (tag.IsNotSet)
		{
			return EMPTY;
		}
		return from cfg in FindAllModuleDefsExpensive()
			where cfg.Common.upgradetag == tag
			select cfg;
	}

	public static List<AddModuleDef> FindUpgradesOrNull(IModule module, VisitState visit, SimTime now)
	{
		if (!module.IsEnabled(now))
		{
			return null;
		}
		return (from cfg in FindUpgradeSet(module.ModuleConfig)
			where cfg != module.ModuleConfig
			select MakeAddModuleDef(cfg, visit) into def
			where def.passesVisreqs
			select def).ToList();
	}

	internal static string DescribeInventory(Entity entity, bool addHeader = true)
	{
		if (entity == null)
		{
			return "";
		}
		if (!addHeader)
		{
			return DescribeInventoryHelper("", entity);
		}
		string header = null;
		if (entity.components.building != null)
		{
			header = BuildingUtil.FindBuildingName(entity);
		}
		if (entity.components.mobile != null)
		{
			string name = entity.config.mobile.GetName();
			string text = Loc.Percentage(entity.components.mobile.CurrentHealthAsFraction);
			string text2 = entity.components.mobile.MakeConditionString(shortinfo: true);
			header = Loc.Get("ui.contents.vehicle-header", "name", name, "label", text2, "percent", text);
		}
		return DescribeInventoryHelper(header, entity);
	}

	private static string DescribeInventoryHelper(string header, Entity entity)
	{
		if (header == null)
		{
			return "";
		}
		InventoryModule inventoryModule = entity.components.modules?.inventory;
		if (inventoryModule == null)
		{
			return header;
		}
		string text = "";
		using (ListPool<string>.PooledBlockList pooledBlockList = ListPool<string>.Allocate())
		{
			if (inventoryModule.data.money.IsPositive)
			{
				pooledBlockList.Add(Loc.Money(inventoryModule.data.money));
			}
			foreach (ResourceAndQty content in inventoryModule.data.contents)
			{
				if (content.qty != 0)
				{
					pooledBlockList.Add(content.MakeQuantityLocString());
				}
			}
			text = string.Join("\n", pooledBlockList);
		}
		string pluralized = Loc.GetPluralized((entity.components.mobile != null) ? "ui.contents.vehicle" : "ui.contents.other", text.Length, "items", text);
		return header + "\n\n" + pluralized;
	}

	internal static string DescribeInventoryBrief(Entity entity)
	{
		if (entity == null)
		{
			return "";
		}
		string name = null;
		if (entity.components.building != null)
		{
			name = BuildingUtil.FindBuildingName(entity);
		}
		if (entity.components.mobile != null)
		{
			name = entity.config.mobile.GetName();
		}
		return DescribeInventoryBriefHelper(name, entity);
	}

	private static string DescribeInventoryBriefHelper(string name, Entity entity)
	{
		if (name == null)
		{
			return "";
		}
		InventoryModule inventoryModule = entity.components.modules?.inventory;
		if (inventoryModule == null)
		{
			return name;
		}
		string text = "";
		using (ListPool<string>.PooledBlockList pooledBlockList = ListPool<string>.Allocate())
		{
			if (inventoryModule.data.money.IsPositive)
			{
				pooledBlockList.Add(Loc.Money(inventoryModule.data.money));
			}
			foreach (ResourceAndQty content in inventoryModule.data.contents)
			{
				if (content.qty != 0)
				{
					pooledBlockList.Add(content.MakeQuantityIconString());
				}
			}
			text = string.Join(", ", pooledBlockList);
		}
		return Loc.GetPluralized((entity.components.mobile != null) ? "ui.contents.vehicle" : "ui.contents.other", text.Length, "items", text);
	}

	internal static string DescribeVehicleAndCapacity(Entity vehicle)
	{
		string name = vehicle.config.mobile.GetName();
		InventoryModule inventoryModule = vehicle.components.modules?.inventory;
		if (inventoryModule == null)
		{
			return name;
		}
		string value = Loc.Volume(inventoryModule.config.capacity, header: true);
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		stringBuilder.AppendLine(name);
		stringBuilder.AppendLine(value);
		return stringBuilder.ToStringAndReturnToPool();
	}

	internal static string DescribeModuleShort(Label moduleId)
	{
		IModuleConfig moduleConfig = FindModuleDef(moduleId);
		if (moduleConfig == null)
		{
			return Loc.Get("module.desc.short.uninteresting");
		}
		if (moduleConfig is ManufactureModuleConfig manufactureModuleConfig)
		{
			string text = "";
			{
				foreach (Recipe recipe in manufactureModuleConfig.recipes)
				{
					string text2 = "";
					foreach (ResourceAndQty item in recipe.produce)
					{
						text2 = text2 + " " + item.FindResource().GetIconAndName();
					}
					text += Loc.Get("module.desc.short.name", "name", Loc.Get(manufactureModuleConfig.Common.display.locname));
					text += Loc.Get("module.desc.short.produces", "restext", text2);
				}
				return text;
			}
		}
		return Loc.Get("module.desc.short.name", "name", Loc.Get(moduleConfig.Common.display.locname)) + Loc.Get("module.desc.short.uninteresting");
	}

	internal static string DescribeInventoryModuleShort(Entity entity)
	{
		if (entity == null)
		{
			return null;
		}
		InventoryModule inventoryModule = entity.components.modules?.inventory;
		if (inventoryModule != null)
		{
			return Loc.Get("module.desc.inventory", "capacity", Loc.Volume(inventoryModule.config.capacity));
		}
		return null;
	}

	public static Fixnum TransferCashBetweenPlayerInventories(PlayerID pid, InventoryModule source, InventoryModule target, Fixnum? cap = null)
	{
		if (source == null || target == null)
		{
			Logger.Error("Null source or target in cash transfer", source == null, target == null);
			return Fixnum.ZERO;
		}
		PlayerFinances finances = pid.FindPlayer().finances;
		Fixnum cash = source.data.money.cash;
		if (cash.IsZero)
		{
			return 0;
		}
		Price price = cap ?? cash;
		if (!price.IsPositive)
		{
			Logger.Warning("Cash transfer delta must be positive!");
			return 0;
		}
		Price price2 = new Price(Fixnum.Clamp(price.cash, 0, cash));
		source.data.DoChangeMoney(finances, -price2);
		target.data.DoChangeMoney(finances, price2);
		finances.RefreshPlayerFinances();
		return price2.cash;
	}

	public static Fixnum TransferResourceBetweenPlayerInventories(PlayerID pid, InventoryModule source, InventoryModule target, Label item, int? maxqty = null)
	{
		if (source == null || target == null)
		{
			Logger.Error("Null source or target in resource transfer", source == null, target == null);
			return Fixnum.ZERO;
		}
		_ = pid.FindPlayer().finances;
		return TransferResourceRespectingLimits(source, target, item, maxqty);
	}

	public static Fixnum TransferResourceRespectingLimits(InventoryModule source, InventoryModule target, Label item, int? max = null, Fixnum? efficiency = null)
	{
		Fixnum qty = FindHowMuchCanBeTransferredOut(source, item, max);
		Fixnum fixnum = FindHowMuchCanBeTransferredIn(target, item, qty);
		if (efficiency.HasValue)
		{
			fixnum = (fixnum * Fixnum.Clamp(efficiency.Value, 0, 1)).Floor();
		}
		if (!fixnum.IsPositive)
		{
			return 0;
		}
		source.data.Increment(item, -fixnum);
		target.data.Increment(item, fixnum);
		return fixnum;
	}

	public static Fixnum FindHowMuchCanBeTransferredOut(InventoryModule source, Label item, int? qty = null)
	{
		ResourceAndQty resourceAndQty = source.data.Get(item);
		if (resourceAndQty.qty.IsZero)
		{
			return 0;
		}
		Fixnum value = ((Fixnum?)qty) ?? resourceAndQty.qty;
		if (value.IsZero)
		{
			return 0;
		}
		if (value.IsNegative)
		{
			Logger.Warning("Resource transfer delta must be positive!");
			return 0;
		}
		return Fixnum.Clamp(value, 0, resourceAndQty.qty).Floor();
	}

	public static Fixnum FindHowMuchCanBeTransferredIn(InventoryModule target, Label item, Fixnum qty)
	{
		int num = target.HowManyResourcesCanFit(Resource.Find(item));
		return Fixnum.Clamp(qty, 0, num).Floor();
	}

	public static void ShowDeliveryTicker(ScheduledDeliveryResult result)
	{
		Fixnum qty = result.qtyAndDir.qty;
		string iconNameAndUnits = result.res.GetIconNameAndUnits(qty);
		string text = BuildingUtil.FindBuildingName(result.building);
		string text2 = "";
		if (result.qtyAndDir.IsToBuilding)
		{
			string text3 = Loc.Get("ui.trade.delivery.sell", "resdesc", iconNameAndUnits, "bizdesc", text);
			string message = Loc.Get("ui.trade.delivery.sinfo", "status", text3, "extra", text2, "cash", Loc.Price(result.cash));
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.TRADE_SELL, TickerTitle.DEFAULT, message, result.building.Id);
		}
		else
		{
			string text4 = Loc.Get("ui.trade.delivery.buy", "resdesc", iconNameAndUnits, "bizdesc", text);
			string message2 = Loc.Get("ui.trade.delivery.binfo", "status", text4, "extra", text2, "cash", Loc.Price(result.cash));
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.TRADE_BUY, TickerTitle.DEFAULT, message2, result.building.Id);
		}
	}

	public static DeliveryInfo FindItemDeliveryFromModules(Entity building, ModuleQuery q, Label resid)
	{
		List<IBizModule> list = building?.components.modules?.bizmodules;
		if (list == null)
		{
			return DeliveryInfo.INVALID;
		}
		foreach (IBizModule item in list)
		{
			DeliveryInfo result = item.ProduceDeliveryInfo(resid, q);
			if (result.IsValid)
			{
				return result;
			}
		}
		return DeliveryInfo.INVALID;
	}

	public static IEnumerable<Label> FindAllResourcesProducedByBizModules(Entity building, ModuleQuery _)
	{
		List<IBizModule> list = building?.components.modules?.bizmodules;
		if (list == null)
		{
			yield break;
		}
		foreach (IBizModule item in list)
		{
			IEnumerable<MfgItem> enumerable = item.ProduceAllItemsInCurrentRecipe();
			foreach (MfgItem item2 in enumerable)
			{
				yield return item2.id;
			}
		}
	}

	public static void AddUnlockedMfgItems(PlayerInfo player, Entity building, List<ModuleAndItem> results)
	{
		List<IBizModule> bizModules = GetBizModules(building);
		PlayerSkills skills = player.skills;
		foreach (IBizModule item in bizModules)
		{
			foreach (MfgItem item2 in item.ProduceAllItemsInCurrentRecipe())
			{
				if (skills.HasResourceUnlocked(item2.id))
				{
					results.Add(new ModuleAndItem
					{
						module = item,
						item = item2
					});
				}
			}
		}
	}

	private static List<ResourceAndQty> ExpandGroups(List<ResourceAndQty> sources, List<ResourceAndQty> target)
	{
		ResourceSettings resources = Game.serv.globals.settings.resources;
		foreach (ResourceAndQty source in sources)
		{
			if (resources.IsResourceBasic(source.id))
			{
				target.Add(source);
				continue;
			}
			foreach (Label groupmember in Game.serv.globals.settings.resources.FindResource(source.id).groupmembers)
			{
				target.Add(new ResourceAndQty(groupmember, source.qty));
			}
		}
		return target;
	}

	private static List<RefillElement> ExpandGroups(List<RefillElement> sources, List<RefillElement> target)
	{
		ResourceSettings resources = Game.serv.globals.settings.resources;
		foreach (RefillElement source in sources)
		{
			if (resources.IsResourceBasic(source.id))
			{
				target.Add(source);
				continue;
			}
			foreach (Label groupmember in Game.serv.globals.settings.resources.FindResource(source.id).groupmembers)
			{
				target.Add(new RefillElement
				{
					id = groupmember,
					below = source.below,
					get = source.get
				});
			}
		}
		return target;
	}

	private static List<SellOffElement> ExpandGroups(List<SellOffElement> sources, List<SellOffElement> target)
	{
		ResourceSettings resources = Game.serv.globals.settings.resources;
		foreach (SellOffElement source in sources)
		{
			if (resources.IsResourceBasic(source.id))
			{
				target.Add(source);
				continue;
			}
			foreach (Label groupmember in Game.serv.globals.settings.resources.FindResource(source.id).groupmembers)
			{
				target.Add(new SellOffElement
				{
					id = groupmember,
					above = source.above,
					sell = source.sell
				});
			}
		}
		return target;
	}

	public static List<ResourceAndQty> ExpandGroups(List<ResourceAndQty> sources)
	{
		if (sources.Count != 0)
		{
			return ExpandGroups(sources, new List<ResourceAndQty>());
		}
		return new List<ResourceAndQty>();
	}

	public static List<RefillElement> ExpandGroups(List<RefillElement> sources)
	{
		if (sources.Count != 0)
		{
			return ExpandGroups(sources, new List<RefillElement>());
		}
		return new List<RefillElement>();
	}

	public static List<SellOffElement> ExpandGroups(List<SellOffElement> sources)
	{
		if (sources.Count != 0)
		{
			return ExpandGroups(sources, new List<SellOffElement>());
		}
		return new List<SellOffElement>();
	}
}
