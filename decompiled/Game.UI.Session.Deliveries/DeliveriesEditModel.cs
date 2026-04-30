using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.UI.Session.Deliveries;

internal class DeliveriesEditModel
{
	public abstract class DropdownItem
	{
		public string text;

		public bool disabled;
	}

	public class DropdownModel<T> : List<T> where T : DropdownItem
	{
		public int selectedIndex;

		public T selected
		{
			get
			{
				if (selectedIndex < 0 || selectedIndex >= base.Count)
				{
					return null;
				}
				return base[selectedIndex];
			}
		}
	}

	public enum CashOrResType
	{
		Cash,
		Resource
	}

	public class CashOrRes : DropdownItem
	{
		public CashOrResType type;

		public Resource res;

		public bool IsCash => type == CashOrResType.Cash;

		public bool IsRes => type == CashOrResType.Resource;

		public static CashOrRes MakeForResource(Label resid)
		{
			Resource resource = Resource.Find(resid);
			return new CashOrRes
			{
				res = resource,
				type = CashOrResType.Resource,
				text = resource.GetIconAndName()
			};
		}

		public static CashOrRes MakeForCash()
		{
			return new CashOrRes
			{
				type = CashOrResType.Cash,
				res = null,
				text = Loc.Get("ui.deliveries.cash.mo")
			};
		}
	}

	public class Destination : DropdownItem
	{
		public enum Type
		{
			None,
			Owned,
			Biz,
			Front
		}

		public Entity building;

		public Type type;

		public bool IsBiz => type == Type.Biz;

		public bool IsOwned => type == Type.Owned;

		public bool IsOutpost => type == Type.Front;

		public Destination()
		{
		}

		public Destination(Entity building, Type type, string text)
		{
			this.building = building;
			this.type = type;
			base.text = text;
		}
	}

	public class Action : DropdownItem
	{
		public AutoAction type;

		public Action()
		{
		}

		public Action(AutoAction type, string text)
		{
			this.type = type;
			base.text = text;
		}
	}

	public class AmtChoice : DropdownItem
	{
		public AmtChoiceType type;
	}

	public AutomationSequence sequence;

	public AutomationStep original;

	public AutomationStep current;

	public int stepindex;

	public DropdownModel<CashOrRes> items = new DropdownModel<CashOrRes>();

	public DropdownModel<Destination> destinations = new DropdownModel<Destination>();

	public DropdownModel<Action> actions = new DropdownModel<Action>();

	public DropdownModel<AmtChoice> amts = new DropdownModel<AmtChoice>();

	public bool skipBuy;

	public bool skipSell;

	public int amtnumber;

	private static readonly Fixnum MIN_REL = new Fixnum(-10);

	public void Select<T>(DropdownModel<T> list, int index) where T : DropdownItem
	{
		list.selectedIndex = index;
		current = GenerateAutomationStep();
	}

	public void SetAmtNumber(int amt)
	{
		amtnumber = amt;
		current = GenerateAutomationStep();
	}

	public void SetBoolean(bool? skipBuy = null, bool? skipSell = null)
	{
		if (skipBuy.HasValue)
		{
			this.skipBuy = skipBuy.Value;
		}
		if (skipSell.HasValue)
		{
			this.skipSell = skipSell.Value;
		}
		if (skipBuy.HasValue || skipSell.HasValue)
		{
			current = GenerateAutomationStep();
		}
	}

	public bool IsDataValid()
	{
		if (items.selected != null && destinations.selected != null && actions.selected != null && actions.selected.type != AutoAction.None && amts.selected != null)
		{
			if (amts.selected.type != AmtChoiceType.Everything)
			{
				return amtnumber > 0;
			}
			return true;
		}
		return false;
	}

	public bool IsFirstHalfValid()
	{
		if (items.selected != null && destinations.selected != null && actions.selected != null)
		{
			return actions.selected.type != AutoAction.None;
		}
		return false;
	}

	public string GetStepEditProgress()
	{
		if (items.selected == null)
		{
			return Loc.Get("ui.deliveries.invalidreason.items.null");
		}
		if (destinations.selected == null)
		{
			return Loc.Get("ui.deliveries.invalidreason.destination.null");
		}
		if (actions.selected == null)
		{
			return Loc.Get("ui.deliveries.invalidreason.actions.null");
		}
		if (actions.selected.type == AutoAction.None)
		{
			return Loc.Get("ui.deliveries.invalidreason.actions.none");
		}
		if (amts.selected == null)
		{
			return Loc.Get("ui.deliveries.invalidreason.quantity.null");
		}
		if (amts.selected.type != AmtChoiceType.Everything && amtnumber <= 0)
		{
			return Loc.Get("ui.deliveries.invalidreason.quantity.zero");
		}
		return Loc.Get("ui.deliveries.validreason");
	}

	internal AutomationStep GenerateAutomationStep()
	{
		AutomationStep automationStep = new AutomationStep
		{
			target = (destinations.selected?.building?.Id ?? EntityID.INVALID),
			action = (actions.selected?.type ?? AutoAction.None),
			skipIfEmptyOnSell = skipSell,
			skipIfFullOnBuy = skipBuy
		};
		CashOrRes selected = items.selected;
		AmtChoice selected2 = amts.selected;
		bool flag = automationStep.target.IsValid && automationStep.action != AutoAction.None && selected != null && selected2 != null;
		automationStep.items = ((!flag) ? default(MovedItems) : new MovedItems
		{
			iscash = selected.IsCash,
			res = (selected.res?.resid ?? Label.NULL),
			type = selected2.type,
			qty = amtnumber
		});
		return automationStep;
	}

	public AutomationStep GetPrevious()
	{
		return GetStep(-1);
	}

	public AutomationStep GetNext()
	{
		return GetStep(1);
	}

	private AutomationStep GetStep(int delta)
	{
		if (sequence.steps.Count > 1)
		{
			return sequence.steps[MathUtil.Modulus(stepindex + delta, sequence.steps.Count)];
		}
		return null;
	}

	public void RegenerateModel(bool actions = false, bool items = false, bool destinations = false, bool amts = false)
	{
		items = items || actions;
		destinations = destinations || items;
		amts = amts || destinations;
		CashOrRes selected = this.items.selected;
		Action selected2 = this.actions.selected;
		Destination selected3 = this.destinations.selected;
		if (actions)
		{
			this.actions.ClearAndAddRange(MakeActions());
			int selectedIndex = TryMaintainSelection<Action>(selected2, this.actions, (Action x, Action y) => x.type == y.type);
			this.actions.selectedIndex = selectedIndex;
		}
		if (items)
		{
			this.items.ClearAndAddRange(MakeUnlockedItemsForAction());
			int selectedIndex2 = TryMaintainSelection<CashOrRes>(selected, this.items, (CashOrRes x, CashOrRes y) => x.res == y.res);
			this.items.selectedIndex = selectedIndex2;
		}
		if (destinations)
		{
			this.destinations.ClearAndAddRange(MakeDestinationsForItems());
			int selectedIndex3 = TryMaintainSelection<Destination>(selected3, this.destinations, (Destination x, Destination y) => x.building == y.building);
			this.destinations.selectedIndex = selectedIndex3;
		}
		if (amts)
		{
			this.amts.ClearAndAddRange(MakeAmts());
			this.amts.selectedIndex = 0;
			amtnumber = 0;
		}
		current = GenerateAutomationStep();
		static int TryMaintainSelection<T>(T item, DropdownModel<T> optionList, Func<T, T, bool> testfunc) where T : DropdownItem
		{
			int num = 0;
			if (item != null)
			{
				num = optionList.FindIndex((T x) => testfunc(x, item));
				if (num != -1 && optionList[num].disabled)
				{
					num = -1;
				}
			}
			if (num != -1)
			{
				return num;
			}
			return 0;
		}
	}

	public void InitializeModelFromStep(AutomationSequence seq, AutomationStep step, int stepindex)
	{
		sequence = seq;
		original = step;
		this.stepindex = stepindex;
		skipBuy = step.skipIfFullOnBuy;
		skipSell = step.skipIfEmptyOnSell;
		RegenerateModel(actions: true);
		actions.selectedIndex = MathUtil.ClampMin(actions.FindIndex((Action a) => a.type == step.action), 0);
		RegenerateModel(actions: false, items: true);
		items.selectedIndex = MathUtil.ClampMin(FindCashOrResIndex(step.items), 0);
		RegenerateModel(actions: false, items: false, destinations: true);
		destinations.selectedIndex = MathUtil.ClampMin(destinations.FindIndex(delegate(Destination d)
		{
			EntityID? entityID = d.building?.Id;
			EntityID target = step.target;
			if (!entityID.HasValue)
			{
				return false;
			}
			return !entityID.HasValue || entityID.GetValueOrDefault() == target;
		}), 0);
		RegenerateModel(actions: false, items: false, destinations: false, amts: true);
		AmtChoiceType amtType = step.items.type;
		amts.selectedIndex = MathUtil.ClampMin(amts.FindIndex((AmtChoice a) => a.type == amtType), 0);
		amtnumber = step.items.qty;
		current = GenerateAutomationStep();
	}

	private int FindCashOrResIndex(MovedItems items)
	{
		if (!items.iscash)
		{
			return this.items.FindIndex((CashOrRes i) => i.IsRes && i.res.resid == items.res);
		}
		return this.items.FindIndex((CashOrRes i) => i.IsCash);
	}

	private IEnumerable<Action> MakeActions()
	{
		string citytype = Game.ctx.session.mapconfig.citytype;
		if (citytype == "pittsburgh" || citytype == "detroit")
		{
			return new List<Action>
			{
				new Action(AutoAction.Buy, Loc.Get("ui.deliveries.actions.mo.buy")),
				new Action(AutoAction.Sell, Loc.Get("ui.deliveries.actions.mo.sell")),
				new Action(AutoAction.PickUp, Loc.Get("ui.deliveries.actions.mo.pickup")),
				new Action(AutoAction.DropOff, Loc.Get("ui.deliveries.actions.mo.dropoff")),
				new Action(AutoAction.HaveCash, Loc.Get("ui.deliveries.actions.mo.cash-on-hand")),
				new Action(AutoAction.FrontVisit, Loc.Get("ui.deliveries.actions.mo.collect-front")),
				new Action(AutoAction.BottlePickup, Loc.Get("ui.deliveries.actions.mo.bottle-pickup")),
				new Action(AutoAction.VehicleRepair, Loc.Get("ui.deliveries.actions.mo.repair-vehicle"))
			};
		}
		return new List<Action>
		{
			new Action(AutoAction.Buy, Loc.Get("ui.deliveries.actions.mo.buy")),
			new Action(AutoAction.Sell, Loc.Get("ui.deliveries.actions.mo.sell")),
			new Action(AutoAction.PickUp, Loc.Get("ui.deliveries.actions.mo.pickup")),
			new Action(AutoAction.DropOff, Loc.Get("ui.deliveries.actions.mo.dropoff")),
			new Action(AutoAction.HaveCash, Loc.Get("ui.deliveries.actions.mo.cash-on-hand")),
			new Action(AutoAction.FrontVisit, Loc.Get("ui.deliveries.actions.mo.collect-front")),
			new Action(AutoAction.VehicleRepair, Loc.Get("ui.deliveries.actions.mo.repair-vehicle"))
		};
	}

	private List<CashOrRes> MakeUnlockedItemsForAction()
	{
		AutoAction autoAction = actions.selected?.type ?? AutoAction.None;
		switch (autoAction)
		{
		case AutoAction.Buy:
		case AutoAction.Sell:
			return MakeItemsList(includeCash: false, includeResources: true, autoAction);
		case AutoAction.PickUp:
		case AutoAction.DropOff:
			return MakeItemsList(includeCash: true, includeResources: true, autoAction);
		case AutoAction.None:
		case AutoAction.HaveCash:
		case AutoAction.FrontVisit:
		case AutoAction.VehicleRepair:
			return MakeItemsList(includeCash: true, includeResources: false, autoAction);
		case AutoAction.BottlePickup:
			return new List<CashOrRes> { CashOrRes.MakeForResource((Label)"small-bottles") };
		default:
			return MakeItemsList(includeCash: true, includeResources: false, autoAction);
		}
		static List<CashOrRes> MakeItemsList(bool includeCash, bool includeResources, AutoAction action)
		{
			List<CashOrRes> list = new List<CashOrRes>();
			if (includeResources)
			{
				list.AddRange(from resid in Game.ctx.players.Human.skills.GetUnlockedResourcesUnsafe()
					select CashOrRes.MakeForResource(resid) into item
					orderby item.res.GetName()
					select item);
				if (action == AutoAction.Buy || action == AutoAction.Sell)
				{
					List<Label> list2 = MakeListOfItemsThatCanBeBoughtOrSold(action == AutoAction.Buy);
					foreach (CashOrRes item in list)
					{
						if (!list2.Contains(item.res.resid))
						{
							item.disabled = true;
							item.text = TextUtil.ColorDisabled(item.text);
						}
					}
				}
			}
			if (includeCash)
			{
				list.Insert(0, CashOrRes.MakeForCash());
			}
			return list.OrderBy((CashOrRes x) => x.disabled).ToList();
		}
		static List<Label> MakeListOfItemsThatCanBeBoughtOrSold(bool buy)
		{
			List<Label> list = new List<Label>();
			foreach (Entity item2 in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
			{
				ProduceItemsPlayerCanBuySell(item2, buy, list);
			}
			return list;
		}
	}

	private IEnumerable<Destination> MakeDestinationsForItems()
	{
		PlayerInfo player = Game.ctx.players.Human;
		AutoAction action = actions.selected?.type ?? AutoAction.None;
		bool num = action == AutoAction.PickUp || action == AutoAction.DropOff || action == AutoAction.HaveCash;
		bool canGoToBuySell = action == AutoAction.Buy || action == AutoAction.Sell;
		bool canGoToFronts = action == AutoAction.FrontVisit || action == AutoAction.BottlePickup;
		bool canGoToRepair = action == AutoAction.VehicleRepair;
		bool isCash = items.selected.IsCash;
		if (num)
		{
			foreach (EntityID item2 in player.territory.GetAllControlledBuildingsUnsafe())
			{
				if (item2.FindEntity().components.modules.gambling == null || isCash)
				{
					Entity entity = item2.FindEntity();
					string text = BuildingUtil.FindBuildingName(item2);
					string text2 = Loc.Get("ui.deliveries.destination-biz.mo", "bizname", text);
					if (sequence.ContainsTarget(entity.Id))
					{
						yield return new Destination(entity, Destination.Type.Owned, Loc.Get("ui.deliveries.destination-repeat.mo", "name", text2));
					}
					else
					{
						yield return new Destination(entity, Destination.Type.Owned, text2);
					}
				}
			}
		}
		CashOrRes item = items.selected;
		if (canGoToFronts)
		{
			foreach (OutpostEntry item3 in player.outposts.GetOutpostEntriesUnsafe())
			{
				Entity entity2 = item3.outpostId.FindBuilding();
				bool num2 = !player.territory.IsControlled(entity2);
				bool flag = Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(BuildingUtil.FindOwnerOrManagerForAnyBuilding(entity2).Id).HasBuff(BuffConstants.RELBUFF_BOTTLE_RACK);
				if (num2 && (action != AutoAction.BottlePickup || flag))
				{
					string text3 = BuildingUtil.FindBuildingOwnerOrManagerName(entity2.Id);
					string text4 = Loc.Get("ui.deliveries.destination-front.mo", "name", text3);
					if (sequence.ContainsTarget(entity2.Id))
					{
						yield return new Destination(entity2, Destination.Type.Biz, Loc.Get("ui.deliveries.destination-repeat.mo", "name", text4));
					}
					else
					{
						yield return new Destination(entity2, Destination.Type.Front, text4);
					}
				}
			}
		}
		if (canGoToBuySell && !item.disabled)
		{
			foreach (Entity item4 in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
			{
				var (flag2, _, flag3, flag4) = CanDeliver(item4, item.res);
				if (((flag3 && action == AutoAction.Buy) || (flag4 && action == AutoAction.Sell)) && flag2)
				{
					string text5 = BuildingUtil.FindBuildingName(item4.Id);
					if (sequence.ContainsTarget(item4.Id))
					{
						yield return new Destination(item4, Destination.Type.Biz, Loc.Get("ui.deliveries.destination-repeat.mo", "name", text5));
					}
					else
					{
						yield return new Destination(item4, Destination.Type.Biz, text5);
					}
				}
			}
		}
		if (!canGoToRepair)
		{
			yield break;
		}
		foreach (Entity item5 in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			if (CanRepair(item5).valid)
			{
				string text6 = BuildingUtil.FindBuildingName(item5.Id);
				if (sequence.ContainsTarget(item5.Id))
				{
					yield return new Destination(item5, Destination.Type.Biz, Loc.Get("ui.deliveries.destination-repeat.mo", "name", text6));
				}
				else
				{
					yield return new Destination(item5, Destination.Type.Biz, text6);
				}
			}
		}
	}

	private static (bool valid, BuildingAndBusinessData bbd) CanDealWithBusinessOwner(Entity building)
	{
		BuildingAndBusinessData item = BuildingUtil.FindDataForBuilding(building);
		PlayerInfo human = Game.ctx.players.Human;
		if (human.territory.IsControlled(building))
		{
			return (valid: true, bbd: item);
		}
		if (!human.territory.IsScoped(building))
		{
			return (valid: false, bbd: default(BuildingAndBusinessData));
		}
		if (item.biz == null || !item.biz.data.biz.owner.IsReal)
		{
			return (valid: false, bbd: default(BuildingAndBusinessData));
		}
		Relationship relationshipFromSourceToPlayer = human.social.GetRelationshipFromSourceToPlayer(item.owner.Id);
		if (relationshipFromSourceToPlayer == null || !(relationshipFromSourceToPlayer.Evaluate().current > MIN_REL) || relationshipFromSourceToPlayer.convos < 1)
		{
			return (valid: false, bbd: default(BuildingAndBusinessData));
		}
		return (valid: true, bbd: item);
	}

	private static (bool valid, Entity biz, bool canBuy, bool canSell) CanDeliver(Entity building, Resource res)
	{
		var (flag, buildingAndBusinessData) = CanDealWithBusinessOwner(building);
		if (!flag)
		{
			return (valid: false, biz: null, canBuy: false, canSell: false);
		}
		var (flag2, flag3) = building.components.modules.CanPlayerBuyOrSell(PlayerID.HumanPlayer, res.resid, illegalOkay: true);
		if (!(flag2 || flag3))
		{
			return (valid: false, biz: null, canBuy: false, canSell: false);
		}
		if (IsResourceLocked(buildingAndBusinessData.owner, buildingAndBusinessData.building, res))
		{
			return (valid: false, biz: null, canBuy: false, canSell: false);
		}
		return (valid: true, biz: buildingAndBusinessData.biz, canBuy: flag2, canSell: flag3);
	}

	private static (bool valid, Entity biz) CanRepair(Entity building)
	{
		var (flag, buildingAndBusinessData) = CanDealWithBusinessOwner(building);
		if (!flag)
		{
			return (valid: false, biz: null);
		}
		if (building.components.modules.FindVehicleModuleOrNull() == null)
		{
			return (valid: false, biz: null);
		}
		return (valid: true, biz: buildingAndBusinessData.biz);
	}

	private static bool IsResourceLocked(Entity owner, Entity building, Resource res)
	{
		PlayerInfo human = Game.ctx.players.Human;
		bool flag = human.social.AreIllegalItemsLocked(owner, building);
		bool num = res.GetIsIllegal() && flag;
		bool flag2 = !human.skills.HasResourceUnlocked(res.resid);
		return num || flag2;
	}

	private static void ProduceItemsPlayerCanBuySell(Entity building, bool buy, List<Label> resources)
	{
		var (flag, buildingAndBusinessData) = CanDealWithBusinessOwner(building);
		if (!flag)
		{
			return;
		}
		foreach (BuySellElement item2 in building.components.modules.ProduceAllItemsPlayerCanBuyOrSell(PlayerID.HumanPlayer, buy, !buy))
		{
			if (!resources.Contains(item2.item.id))
			{
				Entity owner = buildingAndBusinessData.owner;
				Entity building2 = buildingAndBusinessData.building;
				MfgItem item = item2.item;
				if (!IsResourceLocked(owner, building2, item.FindResource()))
				{
					resources.Add(item2.item.id);
				}
			}
		}
	}

	private IEnumerable<AmtChoice> MakeAmts()
	{
		if (items.Count == 0)
		{
			return new List<AmtChoice>();
		}
		Action selected = actions.selected;
		if (selected.type == AutoAction.FrontVisit || selected.type == AutoAction.BottlePickup || selected.type == AutoAction.VehicleRepair)
		{
			return new List<AmtChoice>
			{
				new AmtChoice
				{
					type = AmtChoiceType.Everything,
					text = Loc.Get("ui.deliveries.res.mo.agreed")
				}
			};
		}
		if (selected.type == AutoAction.PickUp || selected.type == AutoAction.DropOff)
		{
			return new List<AmtChoice>
			{
				new AmtChoice
				{
					type = AmtChoiceType.Everything,
					text = Loc.Get("ui.deliveries.res.mo.all")
				},
				new AmtChoice
				{
					type = AmtChoiceType.Amount,
					text = Loc.Get("ui.deliveries.res.mo.qty")
				},
				new AmtChoice
				{
					type = AmtChoiceType.AllBut,
					text = Loc.Get("ui.deliveries.res.mo.excess")
				},
				new AmtChoice
				{
					type = AmtChoiceType.EnsureAmount,
					text = Loc.Get("ui.deliveries.res.mo.ensure")
				}
			};
		}
		if (selected.type == AutoAction.Buy || selected.type == AutoAction.Sell)
		{
			return new List<AmtChoice>
			{
				new AmtChoice
				{
					type = AmtChoiceType.Everything,
					text = Loc.Get("ui.deliveries.res.mo.all")
				},
				new AmtChoice
				{
					type = AmtChoiceType.Amount,
					text = Loc.Get("ui.deliveries.res.mo.qty")
				},
				new AmtChoice
				{
					type = AmtChoiceType.EnsureAmount,
					text = Loc.Get("ui.deliveries.res.mo.ensure")
				}
			};
		}
		return new List<AmtChoice>
		{
			new AmtChoice
			{
				type = AmtChoiceType.Everything,
				text = Loc.Get("ui.deliveries.res.mo.all")
			},
			new AmtChoice
			{
				type = AmtChoiceType.Amount,
				text = Loc.Get("ui.deliveries.res.mo.qty")
			}
		};
	}
}
