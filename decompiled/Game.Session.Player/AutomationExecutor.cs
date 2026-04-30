using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Overlays;
using Game.Session.Player.AI;
using Game.Session.Player.Commands;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Deliveries;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class AutomationExecutor : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public const int MAX_ATTEMPTS_TO_SPEND_ALL_POINTS = 5;

	private AutomationData _autodata;

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_autodata = _data.automation;
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			if (_player.commands.PeepHasTask(item.peepId))
			{
				continue;
			}
			AutomationSequence autoOrNull = GetAutoOrNull(item);
			if (autoOrNull == null || autoOrNull.IsAutoNotActive)
			{
				continue;
			}
			for (int i = 0; i < 5; i++)
			{
				if (CanExecuteOneMore(item))
				{
					RunAutomationStep(item, autoOrNull);
				}
			}
		}
	}

	public void OnPlayerTurnEnded()
	{
	}

	private bool CanExecuteOneMore(CrewAssignment crew)
	{
		if (!_player.commands.PeepHasTask(crew.peepId))
		{
			return CommandAutomationStep.CanExecuteOneMore(crew);
		}
		return false;
	}

	private void RunAutomationStep(CrewAssignment crew, AutomationSequence seq)
	{
		if (seq.steps.Count == 0)
		{
			return;
		}
		if (seq.nextstep >= seq.steps.Count)
		{
			seq.nextstep = 0;
		}
		AutomationStep orDefaultFast = seq.steps.GetOrDefaultFast(seq.nextstep);
		if (orDefaultFast == null || SkipThisStep(crew, orDefaultFast))
		{
			seq.IncrementStep();
			return;
		}
		Game.ctx.events.SendImmediate(SessionEventType.AchieveAutomationExecuted);
		Deictics vars = new Deictics
		{
			targetBuilding = orDefaultFast.target
		};
		if (!ScriptDispatcher.RunScript(ScriptNames.AUTOMATION_SCRIPT, _pid, crew.GetPeep(), vars))
		{
			seq.IncrementStep();
		}
	}

	private bool SkipThisStep(CrewAssignment crew, AutomationStep step)
	{
		if (!step.enabled)
		{
			return true;
		}
		if (step.action == AutoAction.Buy && step.skipIfFullOnBuy && IsFullOf(crew, step.items))
		{
			return true;
		}
		if (step.action == AutoAction.Sell && step.skipIfEmptyOnSell && IsEmptyOf(crew, step.items))
		{
			return true;
		}
		return false;
	}

	private bool IsEmptyOf(CrewAssignment crew, MovedItems items)
	{
		return ((crew.GetVehicle()?.components.modules?.inventory)?.data.Get(items.res).qty ?? ((Fixnum)0)) <= 0;
	}

	private bool IsFullOf(CrewAssignment crew, MovedItems items)
	{
		return !((crew.GetVehicle()?.components.modules?.inventory)?.HasEnoughCapacityToAdd(items.FindResource(), 1) ?? false);
	}

	public bool HasAutomation(EntityID vehicle)
	{
		return GetAutoOrNull(vehicle) != null;
	}

	public bool HasAutomation(CrewAssignment crew)
	{
		return GetAutoOrNull(crew) != null;
	}

	public IEnumerable<AutomationSequence> GetAllSequences()
	{
		return _autodata.sequences;
	}

	public AutomationSequence GetAutoOrNull(AutomationID id)
	{
		return GetAuto(null, id);
	}

	public AutomationSequence GetAutoOrNull(EntityID vehicle)
	{
		return GetAuto(vehicle, null);
	}

	public AutomationSequence GetAutoOrNull(CrewAssignment crew)
	{
		return GetAutoOrNull(crew.VehicleID);
	}

	private AutomationSequence GetAuto(EntityID? vehicle, AutomationID? id)
	{
		if (id.HasValue)
		{
			return _autodata.GetOrNull(id.Value);
		}
		if (vehicle.HasValue)
		{
			return _autodata.GetOrNull(vehicle.Value);
		}
		return null;
	}

	public AutomationID MakeNewAutomationSequence()
	{
		AutomationID automationID = new AutomationID
		{
			id = ++_autodata.gensym
		};
		_autodata.sequences.Add(new AutomationSequence
		{
			id = automationID,
			name = Loc.Get("ui.deliveries.route-no", "id", automationID.id)
		});
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerAutomationAddedSequence, PlayerID.HumanPlayer);
		return automationID;
	}

	public void DestroyAutomationSequence(AutomationID id)
	{
		GetAutoOrNull(id);
		int index = _autodata.IndexOf(id);
		_autodata.sequences.RemoveAt(index);
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerAutomationRemovedSequence, PlayerID.HumanPlayer);
	}

	public void SetAutomationCrew(AutomationID id, CrewAssignment crew)
	{
		GetAutoOrNull(id).vehicle = crew.VehicleID;
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerAutomationReassignedSequence, PlayerID.HumanPlayer);
	}

	public void ClearAutomationCrew(CrewAssignment crew)
	{
		GetAutoOrNull(crew).vehicle = EntityID.INVALID;
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerAutomationReassignedSequence, PlayerID.HumanPlayer);
	}

	public void ClearAutomationCrew(AutomationID id)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		autoOrNull.FindCrew();
		autoOrNull.vehicle = EntityID.INVALID;
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerAutomationReassignedSequence, PlayerID.HumanPlayer);
	}

	public bool TryClearAutomationCrew(CrewAssignment crew)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(crew);
		if (autoOrNull != null)
		{
			ClearAutomationCrew(autoOrNull.id);
		}
		return autoOrNull != null;
	}

	public string GetName(AutomationID id)
	{
		return GetAutoOrNull(id)?.name;
	}

	public void SetName(AutomationID id, string name)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		if (autoOrNull != null)
		{
			autoOrNull.name = name;
		}
	}

	public void AddAutoStep(AutomationID id, AutomationStep step)
	{
		GetAutoOrNull(id).steps.Add(step);
	}

	public void SwapSteps(AutomationID id, int a, int b)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		List<AutomationStep> steps = autoOrNull.steps;
		if (a >= 0 && b >= 0 && a < steps.Count && b < steps.Count && a != b)
		{
			steps.Swap(a, b);
		}
		else
		{
			Logger.Warning($"Invalid indices passed to swap: {a}, {b} out of {steps.Count} steps");
		}
		if (autoOrNull.nextstep == a)
		{
			autoOrNull.IncrementStep();
		}
		if (autoOrNull.nextstep == b)
		{
			autoOrNull.IncrementStep();
		}
	}

	public void MoveStep(AutomationID id, int a, int b)
	{
		if (a != b)
		{
			List<AutomationStep> steps = GetAutoOrNull(id).steps;
			bool num = a >= 0 && b >= 0 && a < steps.Count && b < steps.Count && a != b;
			AutomationStep item = steps[a];
			if (num)
			{
				steps.RemoveAt(a);
				steps.Insert(b, item);
			}
			else
			{
				Logger.Warning($"Invalid indices passed to move: {a} to {b}, out of {steps.Count} steps.");
			}
		}
	}

	public void RemoveStep(AutomationID id, int index)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		autoOrNull.steps.RemoveAt(index);
		if (autoOrNull.nextstep > index)
		{
			autoOrNull.DecrementStep();
		}
	}

	public void ReplaceStep(AutomationID id, int index, AutomationStep newstep)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		autoOrNull.steps[index] = newstep;
		if (autoOrNull.nextstep == index)
		{
			autoOrNull.IncrementStep();
		}
	}

	public void RemoveAllSteps(AutomationID id)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		autoOrNull.Stop();
		autoOrNull.steps.Clear();
	}

	public void ToggleExecution(AutomationID id, bool activate)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(id);
		if (autoOrNull != null)
		{
			if (activate && autoOrNull.IsAutoNotActive)
			{
				autoOrNull.Reset();
			}
			if (!activate && autoOrNull.IsAutoActive)
			{
				autoOrNull.Stop();
			}
			autoOrNull.SendAutomationChangedEvent(_pid);
		}
	}

	public void PerformCurrentStep(CrewAssignment crew, EntityID buildingId)
	{
		AutomationSequence autoOrNull = GetAutoOrNull(crew);
		if (autoOrNull == null)
		{
			return;
		}
		AutomationStep nextStep = autoOrNull.GetNextStep();
		if (nextStep == null)
		{
			autoOrNull.Stop();
			return;
		}
		Entity entity = buildingId.FindEntity();
		if (entity == null)
		{
			autoOrNull.Stop();
			return;
		}
		if (entity != nextStep.target.FindEntity())
		{
			entity = nextStep.target.FindEntity();
		}
		switch (nextStep.action)
		{
		case AutoAction.PickUp:
		case AutoAction.DropOff:
			DoPickOrDrop(nextStep, crew, entity);
			break;
		case AutoAction.HaveCash:
			DoEnsureCashOnHand(nextStep, crew, entity);
			break;
		case AutoAction.Buy:
		case AutoAction.Sell:
			DoBuySellAutomationStep(nextStep, crew, entity);
			break;
		case AutoAction.FrontVisit:
			DoFrontVisit(nextStep, crew, entity);
			break;
		case AutoAction.BottlePickup:
			DoBottlePickup(nextStep, crew, entity);
			break;
		case AutoAction.VehicleRepair:
			DoVehicleRepair(nextStep, crew, entity);
			break;
		default:
			Logger.Warning("Unknown automation action", nextStep.action);
			break;
		case AutoAction.None:
			break;
		}
		autoOrNull.IncrementStep();
	}

	private void DoFrontVisit(AutomationStep step, CrewAssignment crew, Entity building)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(building);
		string bizname = buildingAndBusinessData.biz.data.biz.bizname;
		string fullName = buildingAndBusinessData.owner.data.person.FullName;
		string text = Loc.Get("ui.deliveries.actions.collect-front.desc", "bizname", bizname, "peepname", fullName);
		if (_player.outposts.GetOutpostEntryUnsafe(building) == null)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_URGENT, TickerTitle.OUTPOST_NOTICE, Loc.Get("ui.tickers.outpost.automation-front-fail", "name", text), building.Id);
			return;
		}
		OutpostID outpost = new OutpostID(building);
		Entity vehicle = crew.GetVehicle();
		MoneyStatus moneyStatus = _player.outposts.FindOutpostCollectionStatus(outpost);
		bool flag = _player.outposts.CanCrewCollectFromOutpost(vehicle, outpost);
		string text2 = Loc.Price(new Price(moneyStatus.Delta), abs: true);
		if (moneyStatus.NeedsSupport && flag)
		{
			_player.outposts.DoCollectFromOutpost(vehicle, outpost);
		}
		else if (moneyStatus.NeedsSupport && !flag)
		{
			if (Game.serv.saveload.prefs.game.ShouldShowDeliveryTicker(isWarning: true))
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_URGENT, TickerTitle.OUTPOST_NOTICE, Loc.Get("ui.tickers.outpost.automation-money-fail", "name", text, "amt", text2), building.Id);
			}
		}
		else if (moneyStatus.NeedsCollect)
		{
			_player.outposts.DoCollectFromOutpost(vehicle, outpost);
		}
	}

	private bool DoPickOrDrop(AutomationStep step, CrewAssignment crew, Entity bldg)
	{
		if (!bldg.data.building.controlled.Is(_pid))
		{
			Logger.Warning("Automation: only owner should be able to pick/drop from building", bldg);
			return false;
		}
		if (!step.items.All && step.items.qty <= 0)
		{
			Logger.Warning("Automation: missing items to pick/drop");
			return false;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(crew);
		InventoryModule inventory2 = ModulesUtil.GetInventory(bldg);
		InventoryModule inventoryModule = ((step.action == AutoAction.PickUp) ? inventory2 : inventory);
		InventoryModule inventoryModule2 = ((step.action == AutoAction.PickUp) ? inventory : inventory2);
		int? num = step.items.type switch
		{
			AmtChoiceType.Everything => null, 
			AmtChoiceType.Amount => step.items.qty, 
			AmtChoiceType.AllBut => MathUtil.ClampMin(GetAmountIn(inventoryModule, step.items) - step.items.qty, 0), 
			AmtChoiceType.EnsureAmount => MathUtil.ClampMin(step.items.qty - GetAmountIn(inventoryModule2, step.items), 0), 
			_ => step.items.qty, 
		};
		if (step.items.iscash)
		{
			return ModulesUtil.TransferCashBetweenPlayerInventories(_pid, inventoryModule, inventoryModule2, num).IsPositive;
		}
		return ModulesUtil.TransferResourceBetweenPlayerInventories(_pid, inventoryModule, inventoryModule2, step.items.res, num).IsPositive;
		static int GetAmountIn(InventoryModule inv, MovedItems items)
		{
			if (!items.iscash)
			{
				return inv.data.Get(items.res).qty.IntFloor();
			}
			return inv.data.money.cash.IntFloor();
		}
	}

	private bool DoEnsureCashOnHand(AutomationStep step, CrewAssignment crew, Entity bldg)
	{
		if (!bldg.data.building.controlled.Is(_pid))
		{
			Logger.Warning("Automation: only owner should be able to ensure cash from building", bldg);
			return false;
		}
		if (!step.items.All && step.items.qty <= 0)
		{
			Logger.Warning("Automation: missing quantity in ensure cash");
			return false;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(crew);
		InventoryModule inventory2 = ModulesUtil.GetInventory(bldg);
		Fixnum cash = inventory.data.money.cash;
		InventoryModule source;
		InventoryModule target;
		int? num;
		if (step.items.All)
		{
			source = inventory2;
			target = inventory;
			num = null;
		}
		else
		{
			Fixnum fixnum = step.items.qty - cash;
			bool num2 = fixnum > 0;
			source = (num2 ? inventory2 : inventory);
			target = (num2 ? inventory : inventory2);
			num = (int)fixnum.Abs;
		}
		return ModulesUtil.TransferCashBetweenPlayerInventories(_pid, source, target, num).IsPositive;
	}

	private void DoVehicleRepair(AutomationStep step, CrewAssignment crew, Entity bldg)
	{
		VehicleModule vehicleModule = bldg.components.modules.FindVehicleModuleOrNull();
		BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(bldg);
		Fixnum? fixnum = crew.GetVehicle()?.data.mobile.health;
		if (vehicleModule == null || !fixnum.HasValue || !Game.serv.globals.settings.people.vehicleSettings.FindHealthInfo(fixnum.Value).showbar)
		{
			return;
		}
		VisitState visitState = new VisitState(crew, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		ModQuery q = visitState.MakeCrewModQuery();
		Price item = vehicleModule.FindRepairCost(visitState, q, explain: false).price;
		if (visitState.GetPlayer().finances.CanChangeMoneyOnCrew(visitState, item))
		{
			visitState.GetPlayer().finances.DoChangeMoneyOnCrew(visitState, item, MoneyReason.VehicleMaintenance);
			visitState.vehicle.components.mobile.SetHealthToMax(crew);
			if (Game.serv.saveload.prefs.game.ShouldShowDeliveryTicker(isWarning: false))
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.REPAIR_SUCCESS, TickerTitle.DEFAULT, Loc.Get("ui.tickers.vehicle.automation-success", "name", crew.GetPeep().data.person.FullName), bldg.Id);
			}
		}
		else if (Game.serv.saveload.prefs.game.ShouldShowDeliveryTicker(isWarning: true))
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.REPAIR_FAIL, TickerTitle.DELIVERIES, Loc.Get("ui.tickers.vehicle.automation-fail"), bldg.Id);
		}
	}

	private void DoBottlePickup(AutomationStep step, CrewAssignment crew, Entity bldg)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(bldg);
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(bldg);
		Relationship relationshipFromSourceToPlayer = Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(entity.Id);
		if (relationshipFromSourceToPlayer.HasBuff(BuffConstants.RELBUFF_BOTTLE_RACK) && !relationshipFromSourceToPlayer.HasBuff(BuffConstants.RELBUFF_BOTTLE_COOLDOWN))
		{
			ModulesUtil.GetInventory(Game.ctx.players.Human.territory.Safehouse.FindEntity())?.TryAddResourcesIfSpaceAvailable((Label)"small-bottles", 20);
			relationshipFromSourceToPlayer.AddBuff(BuffConstants.RELBUFF_BOTTLE_COOLDOWN, crew.peepId);
			if (Game.serv.saveload.prefs.game.ShouldShowDeliveryTicker(isWarning: false))
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BOTTLE_RETURN, TickerTitle.DEFAULT, Loc.Get("ui.tickers.bottle.automation-success", "name", buildingAndBusinessData.biz.data.biz.bizname), bldg.Id);
			}
		}
	}

	internal void RemoveBuildingFromDeliveries(Entity building)
	{
		foreach (CrewAssignment item in _player.crew.GetLiving())
		{
			AutomationSequence autoOrNull = GetAutoOrNull(item);
			if (autoOrNull != null && autoOrNull.ContainsTarget(building.Id))
			{
				RemoveBuildingFromDeliveries(item, building, autoOrNull);
			}
		}
	}

	private void RemoveBuildingFromDeliveries(CrewAssignment crew, Entity building, AutomationSequence seq)
	{
		while (seq.ContainsTarget(building.Id))
		{
			int index = seq.FindIndexOfTarget(building.Id);
			RemoveStep(seq.id, index);
		}
		if (_player.IsHuman)
		{
			string text = BuildingUtil.FindBuildingName(building);
			string fullName = crew.GetPeep().data.person.FullName;
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DELIVERIES, TickerTitle.DELIVERIES, Loc.Get("ui.tickers.delivery-step-removed", "bizname", text, "name", fullName), building.Id, TickerPersistType.Persist);
		}
	}

	private bool DoBuySellAutomationStep(AutomationStep step, CrewAssignment crew, Entity bldg)
	{
		if (!VerifyBuySellAutomationStep(step, crew, bldg))
		{
			return false;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(crew);
		InventoryModule inventory2 = ModulesUtil.GetInventory(bldg);
		Entity biz = BuildingUtil.FindBizForBuilding(bldg);
		InventoryModule source = ((step.action == AutoAction.Buy) ? inventory2 : inventory);
		InventoryModule target = ((step.action == AutoAction.Buy) ? inventory : inventory2);
		int? num = (step.items.All ? ((int?)null) : new int?(step.items.qty));
		Fixnum qty = ModulesUtil.FindHowMuchCanBeTransferredOut(source, step.items.res, num);
		Fixnum desiredQty = ModulesUtil.FindHowMuchCanBeTransferredIn(target, step.items.res, qty);
		Fixnum fixnum = BuySellUtils.FindMaxAffordedToBuySell(_player, bldg, biz, target, step.items.res, desiredQty, step.action == AutoAction.Buy, crew);
		if (step.items.type == AmtChoiceType.EnsureAmount)
		{
			if (step.action == AutoAction.Buy)
			{
				ResourceAndQty resourceAndQty = inventory.data.Get(step.items.res);
				int val = MathUtil.ClampMin((int)(num - resourceAndQty.qty).Value, 0);
				fixnum = Math.Min((int)fixnum, val);
			}
			else if (step.action == AutoAction.Sell)
			{
				ResourceAndQty resourceAndQty2 = inventory.data.Get(step.items.res);
				int? num2 = num;
				Fixnum qty2 = resourceAndQty2.qty;
				Fixnum? fixnum2 = num2;
				int val2 = MathUtil.ClampMin((int)(qty2 - fixnum2).Value, 0);
				fixnum = Math.Min((int)fixnum, val2);
			}
		}
		QtyAndDir qtyAndDir = new QtyAndDir(fixnum, step.action == AutoAction.Sell);
		(bool success, Fixnum playerMoneyDelta) tuple = BuySellUtils.ExecuteHumanBuySell(_player, crew, bldg, step.items.FindResource(), qtyAndDir, recurring: true);
		bool item = tuple.success;
		Fixnum item2 = tuple.playerMoneyDelta;
		bool isZero = qtyAndDir.qty.IsZero;
		bool insufficient = num.HasValue && qtyAndDir.qty < num.Value;
		bldg.components.delivery.OnAutomatedDelivery(_pid, Game.ctx.clock.Now, qtyAndDir, insufficient, isZero);
		if (_pid.IsHumanPlayer)
		{
			ModulesUtil.ScheduledDeliveryResult result = new ModulesUtil.ScheduledDeliveryResult
			{
				building = bldg,
				res = step.items.FindResource(),
				cash = item2,
				qtyAndDir = qtyAndDir
			};
			if (Game.serv.saveload.prefs.game.ShouldShowDeliveryTicker(isWarning: false))
			{
				ModulesUtil.ShowDeliveryTicker(result);
			}
		}
		return item;
	}

	private bool VerifyBuySellAutomationStep(AutomationStep step, CrewAssignment crew, Entity bldg)
	{
		if (bldg.data.building.controlled.Is(_pid))
		{
			Logger.Warning("Automation: only non-owner should be able to buy/sell from building", bldg);
			return false;
		}
		if (!step.items.All && step.items.qty <= 0)
		{
			Logger.Warning("Automation: missing item quantity to buy/sell");
			return false;
		}
		if (step.items.iscash)
		{
			Logger.Warning("Can't buy/sell cash, this isn't a currency exchange");
			return false;
		}
		NodeID nid = crew.GetPeep().data.agent.nid;
		var (flag, flag2) = CopUtil.HasBlockingCop(_pid, nid);
		if (flag || flag2)
		{
			string key = (flag ? "ui.tickers.delivery-cops-here" : "ui.tickers.delivery-cops-near");
			string bizname = BuildingUtil.FindBizForBuilding(bldg).data.biz.bizname;
			string name = GetAutoOrNull(crew).name;
			if (Game.serv.saveload.prefs.game.ShouldShowDeliveryTicker(isWarning: true))
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DELIVERIES, TickerTitle.DELIVERIES, Loc.Get(key, "bizname", bizname, "delivery", name), bldg.Id);
			}
			return false;
		}
		return true;
	}

	private static ArrowChainLink MakeForStep(EntityID from, EntityID to, AutomationStep step)
	{
		Resource res = (step.items.iscash ? null : step.items.FindResource());
		return new ArrowChainLink(from, to, res, step);
	}

	public ArrowChain MakeArrowsForCrew(CrewAssignment crew)
	{
		ArrowChain arrowChain = new ArrowChain();
		AutomationSequence autoOrNull = GetAutoOrNull(crew);
		if (autoOrNull != null)
		{
			AddArrowsForSequence(arrowChain, autoOrNull, null, onlyTarget: false, inDeliv: true);
		}
		return arrowChain;
	}

	public ArrowChain MakeArrowsForBuilding(Entity building, bool controlled)
	{
		return MakeArrowsForBuildings(building, !controlled);
	}

	public ArrowChain MakeArrowsForAllBuildings()
	{
		return MakeArrowsForBuildings(null, onlyTarget: false);
	}

	private ArrowChain MakeArrowsForBuildings(Entity target, bool onlyTarget)
	{
		ArrowChain arrowChain = new ArrowChain();
		foreach (AutomationSequence sequence in _autodata.sequences)
		{
			if (sequence != null && (target == null || sequence.ContainsTarget(target.Id)))
			{
				AddArrowsForSequence(arrowChain, sequence, target, onlyTarget);
			}
		}
		return arrowChain;
	}

	private static void AddArrowsForSequence(ArrowChain results, AutomationSequence seq, Entity target, bool onlyTarget, bool inDeliv = false)
	{
		EntityID vehicle = seq.vehicle;
		if (inDeliv)
		{
			for (int i = 0; i < seq.steps.Count; i++)
			{
				AutomationStep automationStep = seq.steps[i];
				AutomationStep automationStep2 = seq.steps[MathUtil.Modulus(i - 1, seq.steps.Count)];
				if ((!onlyTarget || target == null || !(automationStep.target != target.Id)) && !(automationStep2.target == EntityID.INVALID) && !(automationStep.target == EntityID.INVALID) && !vehicle.IsNotValid)
				{
					if (i == seq.nextstep)
					{
						MakeArrowToOrFrom(results, vehicle, automationStep);
					}
					MakeArrowBetween(results, automationStep2, automationStep);
				}
			}
			return;
		}
		foreach (AutomationStep step in seq.steps)
		{
			if ((!onlyTarget || target == null || !(step.target != target.Id)) && !vehicle.IsNotValid)
			{
				MakeArrowToOrFrom(results, vehicle, step);
			}
		}
	}

	private static void MakeArrowToOrFrom(ArrowChain results, EntityID veh, AutomationStep step)
	{
		if (step.action == AutoAction.Buy || step.action == AutoAction.PickUp)
		{
			results.Add(MakeForStep(step.target, veh, step));
		}
		if (step.action == AutoAction.Sell || step.action == AutoAction.DropOff)
		{
			results.Add(MakeForStep(veh, step.target, step));
		}
	}

	private static void MakeArrowBetween(ArrowChain results, AutomationStep step1, AutomationStep step2)
	{
		results.Add(MakeForStep(step1.target, step2.target, step1));
	}

	internal int CountForTarget(EntityID entityId, bool includeActive, bool includePaused)
	{
		int num = 0;
		foreach (AutomationSequence sequence in _autodata.sequences)
		{
			if (sequence.ContainsTarget(entityId) && (sequence.IsAutoActive ? includeActive : includePaused))
			{
				num++;
			}
		}
		return num;
	}
}
