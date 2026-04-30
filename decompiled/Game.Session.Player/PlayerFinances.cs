using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player.AI;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Victory;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerFinances : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	private PlayerFinanceData _findata;

	public PlayerFinanceData Data => _findata;

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
	}

	public override void OnPreRelease()
	{
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		base.OnPreRelease();
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_findata = _data.finances;
	}

	private void OnNewGame(SessionEvent sev)
	{
		_findata.moneyLedger.OnNewTurn(Game.ctx.clock.Now);
	}

	internal MoneyPerTurnListing GetMoneyThisTurn()
	{
		return _findata.moneyLedger.ThisTurn;
	}

	public void OnGlobalTurnSetAdvanced()
	{
		SimTime now = Game.ctx.clock.Now;
		_findata.moneyLedger.OnNewTurn(now);
		_findata.isStoredCacheDirty = true;
	}

	public void OnPlayerTurnStarted()
	{
		if (_player.IsHuman)
		{
			CollectCrewSalaries();
			CollectCrewFamilySupport();
			MaybeDisplayAnnualSummary();
			_findata.isStoredCacheDirty = true;
		}
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	private void MaybeDisplayAnnualSummary()
	{
		GeneratorSettings generator = Game.serv.globals.settings.general.generator;
		GameClock clock = Game.ctx.clock;
		bool flag = clock.Now.YearsInt != clock.Previous.YearsInt;
		SimTime endOfGame = generator.GetEndOfGame();
		bool num = clock.Now >= endOfGame;
		bool skipEnd = clock.SkipEnd;
		VictoryController controller = Game.ctx.hud.victory.Controller;
		if (num && !skipEnd)
		{
			controller.ShowGameOver();
		}
		else if (flag)
		{
			controller.ShowEndOfYear();
		}
		else
		{
			controller.MaybeShowDemoSummary();
		}
	}

	[Conditional("UNITY_EDITOR")]
	private void VerifyLedgerIntegrity()
	{
		Fixnum cash = _findata.moneyLedger.CurrentMoney.cash;
		Fixnum fixnum = (Fixnum)(from s in _findata.GetMoneyStoredPerLocationUnsafe(_player)
			select s.amt).Sum((Money m) => (float)m.cash);
		if (cash != fixnum)
		{
			Logger.Error($"Fixing cash discrepancy of $ {fixnum - cash}. In storage: {fixnum}. Recorded in ledger: {cash}");
			_findata.moneyLedger.ThisTurn.startMoney = (_findata.moneyLedger.ThisTurn.endMoney = new Money(fixnum));
		}
	}

	private InventoryModule FindInventoryToPaySalaryOrNull(CrewAssignment crew, Price salary)
	{
		InventoryModule inventory = GetInventory(_player.territory.Safehouse);
		if (inventory.data.CanChangeMoney(salary))
		{
			return inventory;
		}
		foreach (EntityID item in _player.territory.GetAllControlledBuildingsUnsafe())
		{
			InventoryModule inventory2 = GetInventory(item);
			if (inventory2.data.CanChangeMoney(salary))
			{
				return inventory2;
			}
		}
		Entity target = crew.GetTarget();
		InventoryModule inventoryModule = ((target != null) ? GetInventory(target) : null);
		if (inventoryModule != null && inventoryModule.data.CanChangeMoney(salary))
		{
			return inventoryModule;
		}
		return null;
	}

	internal (Price salary, string explanation) GetCrewSalary(EntityID peepId, bool isDead, bool explain)
	{
		ModQuery query = new ModQuery(_pid, peepId, peepId);
		CrewSettings crew = Game.serv.globals.settings.people.social.crew;
		ModValue modValue = ((peepId == _player.social.PlayerPeepId) ? crew.turnCostBoss : (isDead ? crew.turnCostDead : crew.turnCostCrew));
		Fixnum cash = modValue.Evaluate(query);
		string item = ((explain && cash.IsNotZero) ? modValue.Explain(query, addHeader: true) : "");
		return (salary: new Price(cash), explanation: item);
	}

	private void CollectCrewSalaries()
	{
		IEnumerable<CrewAssignment> allCrew = _player.crew.AllCrew;
		using (ListPool<EntityID>.PooledBlockList pooledBlockList = ListPool<EntityID>.Allocate())
		{
			foreach (CrewAssignment item2 in allCrew)
			{
				if (!item2.IsValid || item2.IsDead)
				{
					continue;
				}
				Price item = GetCrewSalary(item2.peepId, isDead: false, explain: false).salary;
				Entity peep = item2.GetPeep();
				InventoryModule inventoryModule = FindInventoryToPaySalaryOrNull(item2, item);
				if (inventoryModule != null)
				{
					DoChangeMoney(inventoryModule.data, item, MoneyReason.CrewSalary, peep.Id);
					peep.components.agent.RememberCrewSalary(item.cash, item.cash);
					continue;
				}
				if (item2.IsInBuilding)
				{
					pooledBlockList.Add(item2.peepId);
				}
				NotifyOfSalaryFailure(item, item2, item2.IsInBuilding);
				peep.components.agent.RememberCrewSalary(item.cash, 0);
			}
			foreach (EntityID item3 in pooledBlockList)
			{
				_player.crew.UnassignCrewFromBuilding(item3);
			}
		}
		void NotifyOfSalaryFailure(Price s, CrewAssignment crew, bool willBeFired)
		{
			string text = Loc.Price(s.Abs);
			string fullName = crew.GetPeep().data.person.FullName;
			string text2 = Loc.Get("ui.tickers.crew.fail-salary", "name", fullName, "salarystr", text);
			if (willBeFired)
			{
				string text3 = BuildingUtil.FindBuildingName(crew.BuildingID);
				if (text3 != null)
				{
					text2 = text2 + "\n\n" + Loc.Get("ui.tickers.crew.mgmt-to-unassigned", "bizname", text3);
				}
			}
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.SALARY_PROBLEM, TickerTitle.SALARY_PROBLEM, text2, crew.targetId);
			AILog.LogProblem(_pid, crew.peepId, "Failed to pay salary of " + text);
		}
	}

	private void CollectCrewFamilySupport()
	{
		InventoryModule inventory = GetInventory(_player.territory.Safehouse);
		foreach (PlayerCrewData.FamilyPayment item in _player.crew.GetAllSupportPaymentsUnsafe())
		{
			Price perTurn = item.perTurn;
			if (perTurn.IsNonZero)
			{
				if (inventory.data.CanChangeMoney(item.perTurn))
				{
					DoChangeMoney(inventory.data, item.perTurn, MoneyReason.DeadCrewFamily);
				}
				else
				{
					NotifyOfPaymentFailure(item.perTurn, item.peepId);
				}
			}
		}
		void NotifyOfPaymentFailure(Price s, EntityID peepId)
		{
			string text = Loc.Price(s);
			string fullName = peepId.FindEntity().data.person.FullName;
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.SALARY_PROBLEM, TickerTitle.SALARY_PROBLEM, Loc.Get("ui.tickers.crew.fail-family-support", "name", fullName, "salarystr", text));
			AILog.LogProblem(_pid, peepId, "Failed to pay family support of " + text);
		}
	}

	private InventoryModule GetInventory(Entity e)
	{
		return e.components.modules.inventory;
	}

	private InventoryModule GetInventory(EntityID eid)
	{
		return GetInventory(eid.FindEntity());
	}

	private Entity GetPlayerPeepVehicle()
	{
		return _player.crew.GetCrewForPlayerPeep().GetVehicle();
	}

	private Entity GetPlayerSafehouse()
	{
		return _player.territory.Safehouse.FindEntity();
	}

	public Money GetMoney(Entity container)
	{
		return GetInventory(container)?.data.money ?? Money.ZERO;
	}

	public Money GetMoney(EntityID containerId)
	{
		return GetMoney(containerId.FindEntity());
	}

	public Money GetMoneyTotal()
	{
		return GetMoneyThisTurn().endMoney;
	}

	public Money GetMoneyHighWatermark()
	{
		return Data.moneyLedger.HighWatermark;
	}

	public bool CanChangeMoney(Entity container, Price delta)
	{
		InventoryModuleData inventoryModuleData = GetInventory(container)?.data;
		if (inventoryModuleData == null)
		{
			return false;
		}
		Money money = inventoryModuleData.money;
		Money money2 = money + delta;
		if (!money2.IsPositiveOrZero)
		{
			return money2.cash >= money.cash;
		}
		return true;
	}

	public bool CanChangeMoneyOnCrew(VisitState visit, Price delta)
	{
		return CanChangeMoney(visit.vehicle, delta);
	}

	public bool CanChangeMoneyOnPlayer(Price delta)
	{
		return CanChangeMoney(GetPlayerPeepVehicle(), delta);
	}

	public bool CanChangeMoneyOnSafehouse(Price delta)
	{
		return CanChangeMoney(GetPlayerSafehouse(), delta);
	}

	public void DoChangeMoney(Entity container, Price delta, MoneyReason reason, EntityID? target = null)
	{
		if (container != null)
		{
			InventoryModuleData inventoryModuleData = GetInventory(container)?.data;
			if (inventoryModuleData != null)
			{
				DoChangeMoney(inventoryModuleData, delta, reason, target);
			}
		}
	}

	public void DoChangeMoney(InventoryModuleData inv, Price delta, MoneyReason reason, EntityID? target = null)
	{
		inv.DoChangeMoney(this, delta);
		_findata.moneyLedger.Add(reason, delta, target);
		RefreshPlayerFinances();
	}

	public void RefreshPlayerFinances()
	{
		_findata.isStoredCacheDirty = true;
		Game.ctx.events.EnqueueOnce(SessionEventType.PlayerFinancesChanged, _pid);
	}

	public void DoChangeMoneyOnCrew(VisitState visit, Price delta, MoneyReason reason, EntityID? target = null)
	{
		DoChangeMoney(visit.vehicle, delta, reason, target);
	}

	public void DoChangeMoneyOnPlayerPeep(Price delta, MoneyReason reason, EntityID? target = null)
	{
		DoChangeMoney(GetPlayerPeepVehicle(), delta, reason, target);
	}

	public void DoChangeMoneyOnSafehouse(Price delta, MoneyReason reason)
	{
		DoChangeMoney(GetPlayerSafehouse(), delta, reason);
	}

	[Conditional("UNITY_EDITOR")]
	public void ValidateThisIsOwnedInventory(InventoryModule inv)
	{
		using List<PlayerFinanceData.MoneyStoredStruct>.Enumerator enumerator = _findata.GetMoneyStoredPerLocationUnsafe(_player).GetEnumerator();
		while (enumerator.MoveNext() && ModulesUtil.GetInventory(enumerator.Current.eid) != inv)
		{
		}
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("finances", "add-cash", CheatAddCash));
		Game.ctx.console.Add(this, new DebugConsoleEntry("finances", "remove-cash", CheatRemoveCash));
		Game.ctx.console.Add(this, new DebugConsoleEntry("finances", "remove-all-cash", CheatRemoveAllCash));
		Game.ctx.console.Add(this, new DebugConsoleEntry("motherlode", CheatMotherlode));
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	private string CheatAddCash(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<amount>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid amount", args, 2, "<amount>");
		}
		DoChangeMoneyOnPlayerPeep(new Price(result), MoneyReason.Other);
		return $"Added ${result} to the player's vehicle";
	}

	private string CheatRemoveCash(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<amount>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid amount", args, 2, "<amount>");
		}
		DoChangeMoneyOnPlayerPeep(new Price(-result), MoneyReason.Other);
		return $"Removed -${result} to the player's vehicle";
	}

	private string CheatRemoveAllCash(string[] args)
	{
		DoChangeMoneyOnPlayerPeep(new Price(-GetMoney(GetPlayerPeepVehicle()).cash), MoneyReason.Other);
		return "Removed all money from the player's vehicle";
	}

	private string CheatMotherlode(string[] args)
	{
		DoChangeMoneyOnPlayerPeep(new Price(50000), MoneyReason.Other);
		return "Added $50000 to the player's vehicle";
	}
}
