using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using Game.UI.Util;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerGambling : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	public struct GamblingHouseStatus
	{
		public Entity manager;

		public Money cashCurrent;

		public Money cashNeededToOperate;

		public bool isManagerPresent;

		public bool isCashSufficient;

		public bool isNotDamaged;

		public bool IsOpenForBusiness
		{
			get
			{
				if (isManagerPresent && isCashSufficient)
				{
					return isNotDamaged;
				}
				return false;
			}
		}
	}

	private PlayerGamblingData _gdata;

	public PlayerGamblingData Data => _gdata;

	public GamblingSettings Settings => Game.serv.globals.settings.gambling;

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
		_gdata = _data.gambling ?? _gdata;
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		MaybeInformHumanPlayerAboutGambling();
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public Fixnum GetCornersToStartGambling()
	{
		return Settings.startup.minCorners.Evaluate(new ModQuery(_pid));
	}

	public bool HasEnoughCornersToStartGambling()
	{
		return _player.territory.OwnedNodeCount >= GetCornersToStartGambling();
	}

	private void MaybeInformHumanPlayerAboutGambling()
	{
		if (_player.IsHuman && !_gdata.shownGamblingTicker && HasEnoughCornersToStartGambling())
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.CASINO_AVAILABLE, TickerTitle.CASINO_AVAILABLE, Loc.Get("ui.tickers.casino-available"), default(TickerTarget), TickerPersistType.Persist);
			_gdata.shownGamblingTicker = true;
		}
	}

	private void OnNewGame(SessionEvent sev)
	{
	}

	public IEnumerable<EntityID> GetAllMyGamblingHouses()
	{
		return _player.territory.GetAllControlledBuildingsUnsafe().Where(IsOwnedGamblingHouse);
	}

	public IEnumerable<EntityID> GetAllMyGamblingHousesInPrecinct(PlayerInfo precinct)
	{
		return from myCasino in GetAllMyGamblingHouses()
			where CopUtil.FindPrecinctOrNull(myCasino.FindEntity().components.board.GetNode()) == precinct
			select myCasino;
	}

	public int CountAllMyGamblingHousesInPrecinct(PlayerInfo precinct)
	{
		return GetAllMyGamblingHousesInPrecinct(precinct).Count();
	}

	public bool CanBecomeGamblingHouse(Entity building)
	{
		BuildingComponent building2 = building.components.building;
		ResidenceComponent residence = building.components.residence;
		if (CopUtil.FindPrecinctOrNull(building.components.board.GetNode()) == null)
		{
			Logger.Warning("Called CheckCasinoInPrecinct with a visitstate that has a null precinct?");
		}
		if (!building2.IsControlledBy(_pid) && building2.IsResidenceBuildingType && !residence.IsGamblingHouse)
		{
			if (residence.IsEventHostingSpace)
			{
				return residence.CanUnmarkAsReservedEventSpace;
			}
			return true;
		}
		return false;
	}

	public bool CanInstallGamblingModule(Entity building)
	{
		BuildingComponent building2 = building.components.building;
		ResidenceComponent residence = building.components.residence;
		if (building2.IsControlledBy(_pid) && building2.IsResidenceBuildingType)
		{
			return !residence.IsGamblingHouse;
		}
		return false;
	}

	public bool IsOwnedGamblingHouse(EntityID buildingId)
	{
		if (buildingId.IsValid)
		{
			return IsOwnedGamblingHouse(buildingId.FindEntity());
		}
		return false;
	}

	public bool IsOwnedGamblingHouse(Entity building)
	{
		if (BuildingUtil.IsGamblingHouse(building))
		{
			return building.components.building.IsControlledBy(_pid);
		}
		return false;
	}

	public bool InstallGamblingModule(Entity building, Label moduleId)
	{
		ResidenceComponent residence = building.components.residence;
		ModulesComponent modules = building.components.modules;
		bool num = CanInstallGamblingModule(building);
		bool flag = ModulesUtil.FindModuleDef(moduleId) is GamblingModuleConfig;
		if (!num || !flag)
		{
			return false;
		}
		if (residence.IsEventHostingSpace)
		{
			residence.UnmarkAsEventHostingSpace();
		}
		if (ModulesUtil.GetInventory(building) == null)
		{
			modules?.InstallSmallInventory();
		}
		modules.InstallModuleManually(moduleId, Game.ctx.clock.Now);
		residence.MarkAsGamblingHouse();
		return true;
	}

	public bool RemoveGamblingModule(Entity building)
	{
		ResidenceComponent residence = building.components.residence;
		ModulesComponent modules = building.components.modules;
		bool flag = _player.territory.IsControlled(building);
		bool isGamblingHouse = residence.IsGamblingHouse;
		bool flag2 = modules.gambling != null;
		if (!flag || !flag2 || !isGamblingHouse)
		{
			return false;
		}
		List<Label> ids = new List<Label>
		{
			modules.gambling.config.id,
			modules.inventory.config.id
		};
		List<AmenityData> amenities = modules.gambling.data.amenities;
		for (int num = amenities.Count - 1; num >= 0; num--)
		{
			DoDestroyAmenity(amenities[num], modules.gambling);
		}
		building.components.modules.RemoveModules(ids, shutdown: false);
		residence.UnmarkAsGamblingHouse();
		return true;
	}

	public bool AmenityIsFunded(Entity building, AmenityData data, ModQuery query)
	{
		Money money = ModulesUtil.GetInventory(building).data.money;
		bool result = false;
		foreach (AmenityData amenity in building.components.modules.gambling.data.amenities)
		{
			Fixnum fixnum = amenity.GetAmenityDef().minOperationCost.Evaluate(query);
			if (money.cash > fixnum)
			{
				money -= (Price)fixnum;
				if (amenity == data)
				{
					result = true;
					break;
				}
			}
		}
		return result;
	}

	public (string description, string stats) DescribeAmenity(AmenityData.LastTurnResults lastTurn, AmenityDef def, GamblingModule module, VisitState visit, bool useLastTurn = true)
	{
		AmenityData.LastTurnResults lastTurnResults = lastTurn;
		string text = Loc.Get(def.locdesc) + "\n\n";
		if (def.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Mod)
		{
			return (description: text, stats: "");
		}
		string text2 = "";
		if (useLastTurn)
		{
			text2 = Loc.Get("ui.ownedcasino.amenity.mo-money", "lastturn", TextUtil.ColorGreenRed(lastTurnResults.Delta.cash, Loc.Price(lastTurnResults.Delta))) + "\n\n";
		}
		ModQuery query = module.MakeManagerBasedModQuery(Game.ctx.players.Human, visit.building);
		AmenityTuning amenityTuning = new AmenityTuning(def, query);
		string text3 = ((amenityTuning.explainMinBet == "") ? "" : (amenityTuning.explainMinBet + "\n"));
		string text4 = ((amenityTuning.explainMaxBet == "") ? "" : (amenityTuning.explainMaxBet + "\n"));
		string text5 = Loc.Get("ui.ownedcasino.amenity.mo-bets", "min", Loc.Money(amenityTuning.minBet), "minExplain", text3, "max", Loc.Money(amenityTuning.maxBet), "maxExplain", text4) + "\n";
		string text6 = ((amenityTuning.explainWinProb == "") ? "" : ("\n" + Loc.Get("ui.ownedcasino.amenity.mod.format", "explain", amenityTuning.explainWinProb)));
		string text7 = ((def.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Slots || def.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.AOE) ? (Loc.Get("ui.ownedcasino.amenity.mo-payouts", "payout", Loc.FormatNumber(amenityTuning.winMult), "percent", Loc.Percentage(amenityTuning.winProb)) + text6 + "\n\n") : "");
		string text8 = ((def.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Slots || def.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Rake) ? (Loc.Get("ui.ownedcasino.amenity.mo-retention", "dropChance", Loc.Percentage(def.behavior.slots.slotDropChance.Evaluate(query)), "fillChance", Loc.Percentage(def.behavior.slots.slotFillChance.Evaluate(query))) + "\n\n") : "");
		string text9 = "";
		if (useLastTurn && def.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Rake)
		{
			text9 = Loc.Get("ui.ownedcasino.amenity.mo-extra.rake", "income", TextUtil.ColorGreenRed(lastTurnResults.Delta.cash, Loc.Price(lastTurnResults.deltaGamblingIncome)), "players", Loc.FormatNumber(lastTurnResults.numberOfPlayers));
		}
		else if (useLastTurn)
		{
			text9 = Loc.GetPluralized("ui.ownedcasino.amenity.mo-extra", lastTurnResults.numberOfPayouts, "income", TextUtil.ColorGreenRed(lastTurnResults.deltaGamblingIncome.cash, Loc.Price(lastTurnResults.deltaGamblingIncome)), "players", Loc.FormatNumber(lastTurnResults.numberOfPlayers), "payoutCount", Loc.FormatNumber(lastTurnResults.numberOfPayouts), "payout", TextUtil.ColorGreenRed(lastTurnResults.deltaGamblingPayouts.cash, Loc.Price(lastTurnResults.deltaGamblingPayouts, abs: true)));
		}
		Fixnum amt = def.minOperationCost.Evaluate(query);
		string text10 = ((def.minOperationCost.Explain(query, addHeader: false) == "") ? "" : (Loc.Get("ui.ownedcasino.amenity.mod.format", "explain", def.minOperationCost.Explain(query, addHeader: false)) + "\n\n"));
		string text11 = Loc.Get("ui.ownedcasino.money", "needed", Loc.Money(amt)) + text10;
		Fixnum fixnum = def.weeklyCost.Evaluate(query);
		string text12 = ((def.weeklyCost.Explain(query, addHeader: false) == "") ? "" : ("\n" + Loc.Get("ui.ownedcasino.amenity.mod.format", "explain", def.weeklyCost.Explain(query, addHeader: false))));
		string text13 = "\n\n" + Loc.Get("ui.ownedcasino.weeklycost", "cost", TextUtil.ColorGreenRed(fixnum, Loc.Money(fixnum))) + text12 + "\n\n";
		string item = text + text5 + text7;
		string item2 = text8 + text11 + text13 + text2 + text9;
		return (description: item, stats: item2);
	}

	public GamblingHouseStatus GetGamblingHouseStatus(Entity building)
	{
		GamblingModule gambling = building.components.modules.gambling;
		if (gambling == null)
		{
			return default(GamblingHouseStatus);
		}
		InventoryModule inventory = ModulesUtil.GetInventory(building);
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building);
		bool flag = building.components.building.HasDamage();
		Fixnum fixnum = inventory?.data.money.cash ?? default(Fixnum);
		Fixnum fixnum2 = gambling.FindMinimumOperationalValue(_pid, building);
		return new GamblingHouseStatus
		{
			manager = entity,
			cashCurrent = new Money(fixnum),
			cashNeededToOperate = new Money(fixnum2),
			isManagerPresent = (entity != null),
			isCashSufficient = (fixnum >= fixnum2),
			isNotDamaged = !flag
		};
	}

	public GamblerState FindGamblerState(EntityID gamblerId)
	{
		return _gdata.FindStateForGambler(gamblerId);
	}

	public GamblerState FindGamblerState(Entity gambler)
	{
		return _gdata.FindStateForGambler(gambler.Id);
	}

	public List<GamblerState> GetAllGamblerStates()
	{
		return _gdata.states;
	}

	public bool IsGamblingSomewhere(EntityID gamblerId)
	{
		return FindGamblerState(gamblerId) != null;
	}

	public bool IsGamblingSomewhere(Entity gambler)
	{
		return IsGamblingSomewhere(gambler.Id);
	}

	public void RememberFormerGambler(EntityID gamblerId)
	{
		_gdata.formerGamblers.Add(gamblerId);
	}

	public bool IsFormerGambler(EntityID gamblerId)
	{
		return _gdata.formerGamblers.Contains(gamblerId);
	}

	public void DoBanGambler(EntityID gamblerId)
	{
		_gdata.bannedGamblers.Add(gamblerId);
	}

	public bool IsBannedGambler(EntityID gamblerId)
	{
		return _gdata.bannedGamblers.Contains(gamblerId);
	}

	public bool AddGambler(Entity gamblingHouse, AmenityData amenity, Entity gambler, Money startingCash)
	{
		if (IsGamblingSomewhere(gambler))
		{
			return false;
		}
		amenity.gamblers.Add(gambler.Id);
		_gdata.states.Add(new GamblerState(gamblingHouse, gambler, startingCash));
		PlayerMeetWithGambler(gambler);
		AddXpFromGambler();
		return true;
		void AddXpFromGambler()
		{
			BuildingUtil.FindOwnerOrManagerForAnyBuilding(gamblingHouse)?.components.agent.AddXP(XPSource.FromGambler);
		}
	}

	public bool RemoveGambler(AmenityData amenity, Entity gambler)
	{
		GamblerState gamblerState = FindGamblerState(gambler);
		if (gamblerState == null)
		{
			return false;
		}
		amenity.gamblers.Remove(gambler.Id);
		_gdata.states.Remove(gamblerState);
		if (!IsFormerGambler(gambler.Id))
		{
			RememberFormerGambler(gambler.Id);
		}
		return true;
	}

	private void PlayerMeetWithGambler(Entity gambler)
	{
		_player.social.FindOrMakeRelationshipsWith(gambler.Id);
	}

	public void UpdateGamblersAtAmenityDestroyed(AmenityData amenity)
	{
		for (int num = amenity.gamblers.Count - 1; num >= 0; num--)
		{
			Entity entity = amenity.gamblers[num].FindEntity();
			GamblerState gamblerState = FindGamblerState(entity);
			if (entity.data.person.HasResAssigned)
			{
				UnassignDebtorFromResidenceOnMap(entity, entity.data.person.resassigned.FindEntity());
			}
			RemoveResidenceFromAssigned(gamblerState.gamblerResidence);
			RemoveGambler(amenity, entity);
		}
	}

	public void DoRemoveAndBanGambler(Entity gambler, GamblerState state)
	{
		if (gambler.data.person.HasResAssigned)
		{
			UnassignDebtorFromResidenceOnMap(gambler, gambler.data.person.resassigned.FindEntity());
		}
		RemoveGambler(state.FindAmenity(), gambler);
		DoBanGambler(gambler.Id);
		RemoveResidenceFromAssigned(state.gamblerResidence);
	}

	public void RemoveResidenceFromAssigned(EntityID residenceID)
	{
		_gdata.assignedHomes.Remove(residenceID);
	}

	public void AddResidenceToAssigned(EntityID residenceID)
	{
		_gdata.assignedHomes.Add(residenceID);
	}

	public bool HasFormerGamblerRelative(EntityID peepId)
	{
		return FindFirstFormerGamblerRelative(peepId).IsValid;
	}

	public EntityID FindFirstFormerGamblerRelative(EntityID peepId)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(peepId);
		if (listOrNull != null)
		{
			foreach (Relationship datum in listOrNull.data)
			{
				if (datum.IsAnyFamily && IsFormerGambler(datum.to))
				{
					return datum.to;
				}
			}
		}
		return EntityID.INVALID;
	}

	public DebtLevelDef FindCurrentDebtLevel(Entity Gambler)
	{
		return FindCurrentDebtLevel(FindGamblerState(Gambler));
	}

	public DebtLevelDef FindCurrentDebtLevel(GamblerState state)
	{
		if (!state.DebtIsDue)
		{
			return null;
		}
		return Settings.FindDebtById(state.currentDebtDue);
	}

	public DebtLevelDef FindNextDebtLevel(GamblerState state)
	{
		GamblingSettings gsettings = Settings;
		if (state.DebtIsDue)
		{
			return GetNextDebtLevelSafe(gsettings.FindDebtById(state.currentDebtDue));
		}
		if (state.debtsForgivenIds.Count == 0)
		{
			return gsettings.debtLevels[0];
		}
		return GetNextDebtLevelSafe(gsettings.FindDebtById(state.debtsForgivenIds.LastOrDefaultFast()));
		DebtLevelDef GetNextDebtLevelSafe(DebtLevelDef debt)
		{
			int num = gsettings.debtLevels.FindIndex((DebtLevelDef x) => x == debt);
			num = MathUtil.ClampMax(num + 1, gsettings.debtLevels.Count - 1);
			return gsettings.debtLevels[num];
		}
	}

	public List<EntityID> FindBannableGamblers(EntityID building)
	{
		return FindBannableGamblers(building.FindEntity().components.modules.gambling);
	}

	public List<EntityID> FindBannableGamblers(GamblingModule casino)
	{
		List<EntityID> list = new List<EntityID>();
		GamblingSettings settings = Settings;
		foreach (AmenityData amenity in casino.data.amenities)
		{
			foreach (EntityID gambler in amenity.gamblers)
			{
				GamblerState gamblerState = FindGamblerState(gambler);
				if (gamblerState != null && !gamblerState.DebtIsDue)
				{
					AmenityDef amenityDef = settings.FindAmenityById(amenity.defID);
					Fixnum fixnum = amenityDef.behavior.minBetPerPlayer.Evaluate(MakeModQueryForGambler(gambler.FindEntity()));
					if (gamblerState.cash.cash >= fixnum * Settings.bets.numMinBetsBeforeBan || amenityDef.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Rake)
					{
						list.Add(gambler);
					}
				}
			}
		}
		return list;
	}

	public bool DoChangeGamblerMoney(Entity gambler, Price delta)
	{
		GamblerState gamblerState = FindGamblerState(gambler);
		gamblerState.IncrementCash(delta);
		var (flag, debt) = FindNewDebtLevel(gamblerState);
		if (flag)
		{
			MarkGamblerInDebt(gamblerState, debt);
		}
		return flag;
	}

	private (bool crossed, DebtLevelDef newdebt) FindNewDebtLevel(GamblerState state)
	{
		List<DebtLevelDef> debtLevels = Settings.debtLevels;
		Entity gambler = state.FindGambler();
		List<Label> debtsForgivenIds = state.debtsForgivenIds;
		foreach (DebtLevelDef item in debtLevels)
		{
			if (!debtsForgivenIds.Contains(item.id))
			{
				Fixnum abs = item.EvaluateCashForGambler(_pid, gambler).Abs;
				if (state.cash.cash.Abs >= abs && state.cash.cash < 0)
				{
					return (crossed: true, newdebt: item);
				}
			}
		}
		return (crossed: false, newdebt: null);
	}

	private void MarkGamblerInDebt(GamblerState state, DebtLevelDef debt)
	{
		state.currentDebtDue = debt.id;
		Entity gambler = state.FindGambler();
		if (state.gamblerResidence.IsValid)
		{
			Entity residence = state.gamblerResidence.FindEntity();
			AssignDebtorToResidenceOnMap(gambler, residence);
		}
		else
		{
			Entity entity = FindRandomResidenceForGambler(state.FindGamblingHouse());
			state.gamblerResidence = entity.Id;
			AddResidenceToAssigned(entity.Id);
			AssignDebtorToResidenceOnMap(gambler, entity);
		}
		if (debt.initial)
		{
			LetGamblerInheritConnectionsFromFamily();
		}
		if (_player.IsHuman)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DEBTOR_NEW, TickerTitle.CASINO_UPDATE, Loc.Get("ui.tickers.debtor-new", "name", gambler.data.person.FullName, "den", BuildingUtil.GetGamblingHouseName(state.FindGamblingHouse())), gambler.Id, TickerPersistType.DebtorPersist);
		}
		void LetGamblerInheritConnectionsFromFamily()
		{
			EntityID id = gambler.Id;
			RelationshipTracker rels = Game.ctx.simman.rels;
			List<EntityID> list = new List<EntityID>();
			foreach (Relationship item in rels.GetListOrNull(id).data.Where((Relationship rel) => rel.IsAnyFamily))
			{
				RelationshipList listOrNull = rels.GetListOrNull(item.to);
				list.AddRange(listOrNull.data.Select((Relationship r) => r.to));
			}
			int num = 0;
			foreach (EntityID item2 in list)
			{
				if (!(id == item2) && !rels.HasAny(id, item2))
				{
					rels.GetOrMakeSymmetrical(id, item2, RelationshipType.Acquaintance, warnOnExisting: true);
					num++;
				}
			}
		}
	}

	public void SelectRepaymentForDebtor(VisitState visit, Label repaymentId, Fixnum probability)
	{
		bool success = _gdata.rng.CheckProbability(probability);
		SelectRepaymentHelper(repaymentId, visit.npc, success);
	}

	public void SelectPayCash(Entity gambler)
	{
		Label rEPAYMENT_CASH = GamblingConstants.REPAYMENT_CASH;
		SelectRepaymentHelper(rEPAYMENT_CASH, gambler, success: true);
	}

	private void SelectRepaymentHelper(Label repaymentId, Entity gambler, bool success)
	{
		GamblerState gamblerState = FindGamblerState(gambler);
		int deltaDays = (int)Settings.FindRepaymentById(repaymentId).timeDayz.Evaluate(MakeModQueryForGambler(gambler));
		gamblerState.success = success;
		gamblerState.repaymentInProgress = repaymentId;
		gamblerState.repaymentDay = Game.ctx.clock.Now.IncrementDays(deltaDays);
	}

	public void PayForExtendCredit(GamblerState gambler, VisitState visit)
	{
		Price delta = new Price(gambler.cash.cash);
		Entity vehicle = visit.vehicle;
		_player.finances.DoChangeMoney(vehicle, delta, MoneyReason.GamblingPaidDebt);
	}

	public bool CanPayForStartCasino(VisitState visit, GamblingModuleConfig def)
	{
		ModQuery query = new ModQuery(visit.pid);
		Fixnum fixnum = def.gambling.purchaseCost.Evaluate(query);
		return visit.GetPlayer().finances.CanChangeMoney(visit.vehicle, fixnum);
	}

	public bool CanPayForUpgradeCasino(VisitState visit)
	{
		ModQuery query = new ModQuery(visit.pid);
		Fixnum fixnum = (ModulesUtil.FindModuleDef(visit.building.components.modules.gambling.config.gambling.upgradeModule) as GamblingModuleConfig).gambling.upgradeCost.Evaluate(query);
		return visit.GetPlayer().finances.CanChangeMoney(visit.building, fixnum);
	}

	public void PayForUpgradeCasino(VisitState visit)
	{
		ModQuery query = new ModQuery(visit.pid);
		GamblingModuleConfig obj = ModulesUtil.FindModuleDef(visit.building.components.modules.gambling.config.gambling.upgradeModule) as GamblingModuleConfig;
		Entity building = visit.building;
		Fixnum fixnum = obj.gambling.upgradeCost.Evaluate(query);
		_player.finances.DoChangeMoney(building, fixnum, MoneyReason.GamblingOperation);
	}

	public void PayForStartCasino(VisitState visit, Label gamblingModuleId)
	{
		GamblingModuleConfig gamblingModuleConfig = ModulesUtil.FindModuleDef(gamblingModuleId) as GamblingModuleConfig;
		ModQuery query = new ModQuery(_player.PID, EntityID.INVALID, visit.crew.peepId);
		Price delta = new Price(gamblingModuleConfig.gambling.purchaseCost.Evaluate(query));
		Entity vehicle = visit.vehicle;
		_player.finances.DoChangeMoney(vehicle, delta, MoneyReason.GamblingOperation);
	}

	public void PayWeeklyCost(Entity building, Price cost)
	{
		_player.finances.DoChangeMoney(building, cost, MoneyReason.GamblingOperation);
	}

	public Fixnum RollIncompleteRepayment(GamblerState state)
	{
		return new Fixnum(_gdata.rng.Generate(0f - (float)state.cash.cash * 0.1f, 0f - (float)state.cash.cash));
	}

	public void DoExtendGamblerCredit(Entity gambler)
	{
		GamblerState gamblerState = FindGamblerState(gambler);
		gamblerState.cash = new Money(0);
		gamblerState.debtsForgivenIds.Add(gamblerState.currentDebtDue);
		gamblerState.currentDebtDue = Label.NULL;
		gamblerState.repayments.Clear();
		Entity residence = gambler.data.person.resassigned.FindEntity();
		UnassignDebtorFromResidenceOnMap(gambler, residence);
		if (_player.IsHuman)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DEBTOR_FORGIVE, TickerTitle.CASINO_UPDATE, Loc.Get("ui.tickers.debtor-forgive", "name", gambler.data.person.FullName), new TickerTarget(gamblerState.gamblingHouseId));
		}
	}

	public Entity FindRandomResidenceForGambler(Entity gamblingHouse)
	{
		Node node = gamblingHouse.components.board.GetNode();
		ListPool<Entity>.PooledBlockList buildings = ListPool<Entity>.Allocate();
		try
		{
			Game.ctx.board.nodes.VisitNeighborhoodBFS(node, 100, delegate(Node n)
			{
				FindResidences(n, buildings);
			}, null, null, (Node _) => buildings.Count > 20, onlyBizNodes: true);
			return _gdata.rng.PickElementOrDefault(buildings);
		}
		finally
		{
			if (buildings != null)
			{
				((IDisposable)buildings).Dispose();
			}
		}
		void FindResidences(Node node2, List<Entity> list)
		{
			foreach (EntityID item in node2.contained)
			{
				Entity entity = item.FindEntity();
				_ = entity?.components.residence;
				if (IsValidResForGambler(entity))
				{
					list.Add(entity);
				}
			}
		}
	}

	public bool IsValidResForGambler(Entity building)
	{
		if (building?.components.residence?.IsNotAssigned == true && !_gdata.assignedHomes.Contains(building.Id))
		{
			if (building == null)
			{
				return false;
			}
			return !building.components.building.IsSafehouse;
		}
		return false;
	}

	public void ExecuteTransferGamblingHouseMoney(VisitState visit, ConvoDataGamblingCollect _, QtyAndDir delta)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(visit.building);
		InventoryModule inventory2 = ModulesUtil.GetInventory(visit.vehicle);
		InventoryModule target = (delta.toBldg ? inventory : inventory2);
		InventoryModule source = (delta.toBldg ? inventory2 : inventory);
		ModulesUtil.TransferCashBetweenPlayerInventories(_pid, source, target, delta.qty);
	}

	public void AssignDebtorToResidenceOnMap(Entity gambler, Entity residence)
	{
		residence.components.residence.MarkDebtorResidence(gambler);
		gambler.components.person.SetResAssignment(residence);
		if (!residence.components.building.IsScopedBy(_pid))
		{
			PlayerInfo human = Game.ctx.players.Human;
			human.meetings.MarkNodeAsKnown(residence.components.board.GetNode(), expectedSeen: true, instant: false);
			human.territory.ScopeOutBuilding(residence, procgen: false, setControlled: false);
		}
	}

	public void UnassignDebtorFromResidenceOnMap(Entity gambler, Entity residence)
	{
		residence.components.residence.UnmarkDebtorResidence(gambler);
		gambler.components.person.ClearResAssignment();
	}

	public void RequestAOEProcessing(GamblingModule module, InventoryModule inventory, AmenityData amenity, ModQuery query)
	{
		AmenityDef amenityDef = amenity.GetAmenityDef();
		Node node = query.FindNode();
		AmenityTuning amenityTuning = new AmenityTuning(amenityDef, query);
		int num = BuildingUtil.GetNumCustomersForAmenities(query.FindBuildingForTarget(), query)?[amenity] ?? 0;
		Fixnum fixnum = 0;
		Fixnum fixnum2 = 0;
		int num2 = 0;
		RandomRangeF range = new RandomRangeF((float)amenityTuning.minBet, (float)amenityTuning.maxBet, 1);
		for (int i = 0; i < num; i++)
		{
			Fixnum fixnum3 = (Fixnum)module.data.rng.Generate(range);
			if (module.data.rng.CheckProbability(amenityTuning.winProb))
			{
				fixnum2 -= fixnum3 * amenityTuning.winMult;
				num2++;
			}
			else
			{
				fixnum += fixnum3;
			}
		}
		amenity.LogTurn(num, fixnum, fixnum2, num2);
		_player.finances.DoChangeMoney(inventory.data, new Price(fixnum + fixnum2), MoneyReason.GamblingAOE);
		node.heat.IncrementFromGambling(_player.PID, amenityTuning.heatPer * num);
	}

	public void RequestRegularProcessing(GamblingModule module, InventoryModule inventory, AmenityData amenity, ModQuery query)
	{
		GamblingBets bets = Settings.bets;
		AmenityDef def = Settings.FindAmenityById(amenity.defID);
		Node node = query.FindNode();
		AmenityTuning amenityTuning = new AmenityTuning(def, query);
		Fixnum fixnum = 0;
		Fixnum fixnum2 = 0;
		int num = 0;
		foreach (EntityID gambler2 in amenity.gamblers)
		{
			Entity gambler = gambler2.FindEntity();
			GamblerState gamblerState = FindGamblerState(gambler);
			if (gamblerState.DebtIsDue)
			{
				continue;
			}
			ModQuery query2 = new ModQuery(_player.PID, gambler2, query.crewPeepId, node.id);
			int num2 = (amenityTuning.minBet * bets.minBetModifier.Evaluate(query2)).IntFloor();
			int num3 = (amenityTuning.maxBet * bets.maxBetModifier.Evaluate(query2)).IntCeiling();
			RandomRange range = new RandomRange(num2, num3 + 1, 1);
			Fixnum fixnum3 = module.data.rng.Generate(range);
			Fixnum cash = gamblerState.cash.cash;
			bool flag = cash > 0;
			if (flag)
			{
				fixnum3 = MathUtil.ClampMax(fixnum3, cash);
			}
			if (module.data.rng.CheckProbability(amenityTuning.winProb))
			{
				Fixnum fixnum4 = fixnum3 * amenityTuning.winMult;
				Fixnum fixnum5 = fixnum4;
				if (cash < 0)
				{
					fixnum5 = MathUtil.ClampMin(fixnum4 + cash, 0);
				}
				fixnum2 -= fixnum5;
				num++;
				DoChangeGamblerMoney(gambler, new Price(fixnum4));
			}
			else
			{
				if (flag)
				{
					fixnum += fixnum3;
				}
				DoChangeGamblerMoney(gambler, new Price(-fixnum3));
			}
		}
		amenity.LogTurn(amenity.gamblers.Count, fixnum, fixnum2, num);
		_player.finances.DoChangeMoney(inventory.data, new Price(fixnum + fixnum2), MoneyReason.GamblingRegulars);
		node.heat.IncrementFromGambling(_player.PID, amenityTuning.heatPer * amenity.gamblers.Count);
	}

	public void RequestRakeProcessing(GamblingModule module, InventoryModule inventory, AmenityData amenity, ModQuery query)
	{
		if (amenity.gamblers.Count < 2)
		{
			return;
		}
		GamblingBets bets = Settings.bets;
		AmenityDef amenityDef = Settings.FindAmenityById(amenity.defID);
		Node node = query.FindNode();
		AmenityTuning amenityTuning = new AmenityTuning(amenityDef, query);
		List<(Entity, Fixnum)> list = new List<(Entity, Fixnum)>();
		List<float> list2 = new List<float>();
		Fixnum fixnum = 0;
		foreach (EntityID gambler in amenity.gamblers)
		{
			Entity entity = gambler.FindEntity();
			GamblerState gamblerState = FindGamblerState(entity);
			if (!gamblerState.DebtIsDue)
			{
				ModQuery query2 = new ModQuery(_player.PID, gambler, query.crewPeepId, node.id);
				int num = (amenityTuning.minBet * bets.minBetModifier.Evaluate(query2)).IntFloor();
				int num2 = (amenityTuning.maxBet * bets.maxBetModifier.Evaluate(query2)).IntCeiling();
				RandomRange range = new RandomRange(num, num2 + 1, 1);
				Fixnum fixnum2 = module.data.rng.Generate(range);
				Fixnum cash = gamblerState.cash.cash;
				if (cash > 0)
				{
					fixnum2 = MathUtil.ClampMax(fixnum2, cash);
				}
				float item = (float)amenityDef.behavior.rake.rakeEdge.Evaluate(query2);
				list.Add((entity, fixnum2));
				list2.Add(item);
				fixnum += fixnum2;
			}
		}
		float num3 = (float)amenityDef.behavior.rake.rakePercent.Evaluate(query) / 100f;
		Fixnum fixnum3 = (Fixnum)((float)fixnum * num3);
		Fixnum cash2 = fixnum - fixnum3;
		(Entity, Fixnum) tuple = _gdata.rng.PickElement(list, list2);
		cash2 -= tuple.Item2;
		DoChangeGamblerMoney(tuple.Item1, new Price(cash2));
		foreach (var item2 in list)
		{
			var (entity2, fixnum4) = item2;
			var (entity3, fixnum5) = tuple;
			if (entity2 != entity3 || fixnum4 != fixnum5)
			{
				DoChangeGamblerMoney(item2.Item1, new Price(-item2.Item2));
			}
		}
		Fixnum fixnum6 = 0;
		int numberOfPayouts = 0;
		amenity.LogTurn(amenity.gamblers.Count, fixnum3, fixnum6, numberOfPayouts);
		_player.finances.DoChangeMoney(inventory.data, new Price(fixnum3), MoneyReason.GamblingRegulars);
		node.heat.IncrementFromGambling(_player.PID, amenityTuning.heatPer * amenity.gamblers.Count);
	}

	public bool RequestNewGambler(Entity building, AmenityData amenity, ModQuery query)
	{
		AmenityDef def = amenity.GetAmenityDef();
		Fixnum probability = def.behavior.slots.slotFillChance.Evaluate(query);
		if (!_gdata.rng.CheckProbability(probability))
		{
			return false;
		}
		List<Entity> list = (from peep in FindPlayerConnectionsToBecomeGamblers()
			where !IsGamblingSomewhere(peep) && !IsBannedGambler(peep.Id)
			select peep).ToList();
		_gdata.rng.Shuffle(list);
		Entity entity = list.FirstOrDefaultFast();
		if (entity == null)
		{
			return false;
		}
		Fixnum cash = GetStartingCash();
		AddGambler(building, amenity, entity, new Money(cash));
		return true;
		HashSet<Entity> FindPlayerConnectionsToBecomeGamblers()
		{
			HashSet<Entity> hashSet = new HashSet<Entity>();
			List<Relationship> allPlayerRelationshipsUnsafe = _player.social.GetAllPlayerRelationshipsUnsafe();
			SimTime now = Game.ctx.clock.Now;
			foreach (Relationship item in allPlayerRelationshipsUnsafe)
			{
				Entity entity2 = item.to.FindEntity();
				if (entity2 != null && !(item.Evaluate().current < 0))
				{
					if (PlayerSocial.IsEligibleGambler(now, entity2))
					{
						hashSet.Add(entity2);
					}
					foreach (Relationship datum in Game.ctx.simman.rels.GetListOrNull(item.to).data)
					{
						Entity entity3 = datum.to.FindEntity();
						if (entity3 != null && !hashSet.Contains(entity3) && PlayerSocial.IsEligibleGambler(now, entity3))
						{
							hashSet.Add(entity3);
						}
					}
				}
			}
			return hashSet;
		}
		Fixnum GetStartingCash()
		{
			int num = def.behavior.slots.slotStartingCashMin.Evaluate(query).RoundCoarse();
			int num2 = def.behavior.slots.slotStartingCashMax.Evaluate(query).RoundCoarse();
			RandomRange range = new RandomRange(num, num2 + 1, 1);
			return _gdata.rng.Generate(range);
		}
	}

	public bool MaybeLoseGambler(AmenityData amenity, EntityID gamblerId, ModQuery query)
	{
		Fixnum probability = amenity.GetAmenityDef().behavior.slots.slotDropChance.Evaluate(query);
		if (!_gdata.rng.CheckProbability(probability))
		{
			return false;
		}
		Entity entity = gamblerId.FindEntity();
		GamblerState gamblerState = FindGamblerState(entity);
		if (gamblerState.DebtIsDue)
		{
			return false;
		}
		if (gamblerState.cash.cash < 0)
		{
			return false;
		}
		bool num = gamblerState.debtsForgivenIds.Count > 0;
		Money money = gamblerState.cash - gamblerState.startCash;
		if (num)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GAMBLING_UPDATE, TickerTitle.CASINO_UPDATE, Loc.Get("ui.tickers.debtor-escape.debtor", "name", entity.data.person.FullName), new TickerTarget(query.FindBuildingForTarget().Id));
		}
		else if (money.IsPositive)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GAMBLING_UPDATE, TickerTitle.CASINO_UPDATE, Loc.Get("ui.tickers.debtor-escape.simple", "name", entity.data.person.FullName, "delta", Loc.Money(money)), new TickerTarget(query.FindBuildingForTarget().Id));
		}
		RemoveGambler(amenity, entity);
		return true;
	}

	public List<RepaymentChoiceAndSuccess> GetRepayments(Entity gambler)
	{
		GamblerState gamblerState = FindGamblerState(gambler);
		if (gamblerState.repayments.Count == 0)
		{
			gamblerState.repayments.AddRange(GenerateRepayments(gambler, gamblerState.currentDebtDue));
		}
		return gamblerState.repayments;
	}

	private List<RepaymentChoiceAndSuccess> GenerateRepayments(Entity gambler, Label debtId)
	{
		VisitState visit = new VisitState(_player.crew.GetCrewForPlayerPeep(), gambler, Game.ctx.clock.Now, _pid);
		visit.building = FindGamblerState(gambler).gamblerResidence.FindEntity();
		List<GamblingRepayChoice> repayments = Settings.FindDebtById(debtId).repayments;
		IRandom identityRNGUnchanging = gambler.components.ident.GetIdentityRNGUnchanging((uint)Game.ctx.clock.Now.YearsInt);
		List<RepaymentChoiceAndSuccess> list = new List<RepaymentChoiceAndSuccess>();
		foreach (GamblingRepayChoice item in repayments)
		{
			if (_gdata.rng.CheckProbability(item.appearChance))
			{
				List<GamblingRepayment> list2 = item.GetAllIds().Select(Settings.FindRepaymentById).ToList()
					.Where(CheckVisReqs)
					.ToList();
				if (list2.Count != 0)
				{
					GamblingRepayment gamblingRepayment = identityRNGUnchanging.PickElement(list2);
					list.Add(new RepaymentChoiceAndSuccess(gamblingRepayment.id, item.success));
				}
			}
		}
		return list;
		bool CheckVisReqs(GamblingRepayment repayment)
		{
			return repayment.visreqs?.AllPass(visit) ?? true;
		}
	}

	public void ThrowTickerIfRepaymentStatusChanged(EntityID gamblerId)
	{
		Entity entity = gamblerId.FindEntity();
		if (FindGamblerState(entity).WasRepaymentDayReached() && _player.IsHuman)
		{
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DEBTOR_PAID, TickerTitle.CASINO_UPDATE, Loc.Get("ui.tickers.gambler-repayment-ready", "name", entity.data.person.FullName), entity.Id);
		}
	}

	public void ProcessCasinoRaid(Entity casino)
	{
		InventoryModule inventory = ModulesUtil.GetInventory(casino);
		if (inventory != null)
		{
			_player.finances.DoChangeMoney(inventory.data, new Price(-inventory.data.money.cash), MoneyReason.SafehouseRaid);
			bool flag = casino.components.modules.gambling.data.amenities.Count != 0;
			string text = "?";
			if (flag)
			{
				AmenityData amenityData = _gdata.rng.PickElement(casino.components.modules.gambling.data.amenities);
				text = Loc.Get(amenityData.GetAmenityDef().locname);
				DoDestroyAmenity(amenityData, casino.components.modules.gambling);
			}
			if (casino.data.building.controlled.pid == PlayerID.HumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BLDG_DAMAGE_TAKEN, TickerTitle.CASINO_RAIDED, flag ? Loc.Get("ui.tickers.casino-raided", "amenity", text) : Loc.Get("ui.tickers.casino-raided.no-amenity"), new TickerTarget(casino.Id));
			}
		}
	}

	public void ProcessCasinoAttack(Entity casino, PlayerID player)
	{
		_player.territory.ProcessBuildingAttack(casino, player);
		InventoryModule inventory = ModulesUtil.GetInventory(casino);
		if (!(casino.data.building.controlled.pid != _player.PID) && inventory != null)
		{
			_player.finances.DoChangeMoney(inventory.data, new Price(-inventory.data.money.cash), MoneyReason.SafehouseRaid);
			GamblingModule gambling = casino.components.modules.gambling;
			bool flag = casino.components.modules.gambling.data.amenities.Count != 0;
			string text = "?";
			if (flag)
			{
				AmenityData amenityData = _gdata.rng.PickElement(casino.components.modules.gambling.data.amenities);
				text = Loc.Get(amenityData.GetAmenityDef().locname);
				DoDestroyAmenity(amenityData, gambling);
			}
			string playerGroupName = Game.ctx.players.WithID(player).social.PlayerGroupName;
			if (casino.data.building.controlled.pid == PlayerID.HumanPlayer)
			{
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BLDG_DAMAGE_TAKEN, TickerTitle.CASINO_ATTACKED, flag ? Loc.Get("ui.tickers.casino-attacked", "gangname", playerGroupName, "amenity", text) : Loc.Get("ui.tickers.casino-attacked.no-amenity", "gangname", playerGroupName), new TickerTarget(casino.Id));
			}
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.GangWarAction, player.FindPlayer().crew.GetCrewForPlayerPeep().peepId, player, _player.PID));
		}
	}

	public void DoCreateAmenity(AmenityDef def, VisitState visit, GamblingModule module, bool forceFree = false)
	{
		ModQuery query = module.MakeManagerBasedModQuery(_player, visit.building);
		int maxGamblers = def.behavior.slots.slotCount.Evaluate(query).IntCeiling();
		int deltaDays = def.buildDayz.Evaluate(query).IntCeiling();
		Fixnum fixnum = def.buildCost.Evaluate(query);
		AmenityData item = new AmenityData
		{
			type = def.behavior.type,
			gamblers = new List<EntityID>(),
			defID = def.id,
			maxGamblers = maxGamblers,
			lastTurnResults = default(AmenityData.LastTurnResults),
			enableTime = Game.ctx.clock.Now.IncrementDays(deltaDays)
		};
		if (!forceFree)
		{
			_player.finances.DoChangeMoney(visit.building, fixnum, MoneyReason.GamblingOperation);
		}
		module.data.amenities.Add(item);
	}

	public void DoDestroyAmenity(AmenityData amenity, GamblingModule module)
	{
		UpdateGamblersAtAmenityDestroyed(amenity);
		module.data.amenities.Remove(amenity);
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
	}

	public ModQuery MakeModQueryForGambler(Entity gambler)
	{
		return MakeModQueryForGambler(FindGamblerState(gambler));
	}

	public ModQuery MakeModQueryForGambler(GamblerState gambler)
	{
		Entity entity = gambler.FindGamblingHouse();
		return new ModQuery
		{
			pid = entity.data.building.controlled.pid,
			crewPeepId = (BuildingUtil.FindOwnerOrManagerForAnyBuilding(entity)?.Id ?? _player.social.PlayerPeepId),
			nodeId = entity.components.board.GetNodeID(),
			targetId = gambler.gamblerId,
			time = Game.ctx.clock.Now
		};
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("gambling", "add-house", CheatAddGamblingHouse));
		Game.ctx.console.Add(this, new DebugConsoleEntry("gambling", "remove-house", CheatRemoveGamblingHouse));
		Game.ctx.console.Add(this, new DebugConsoleEntry("gambling", "add-debtor", CheatAddDebtorAndAssign));
		Game.ctx.console.Add(this, new DebugConsoleEntry("gambling", "remove-debtor", CheatRemoveDebtorAndUnassign));
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	private (Entity building, string error) CheatFindBuilding(int index)
	{
		Entity entity = Game.ctx.entityman.FindByIndex(index);
		if (entity == null)
		{
			return (building: null, error: "No such entity");
		}
		if (entity.components.building == null)
		{
			return (building: null, error: "Not a building");
		}
		if (!entity.components.building.IsResidenceBuildingType)
		{
			return (building: null, error: "Not a residence");
		}
		return (building: entity, error: null);
	}

	private string CheatSetUpChicagoTestingScenario(string[] _)
	{
		Entity building = Game.ctx.entityman.FindByIndex(9127);
		_player.territory.ScopeOutAndTakeOverResidence(building);
		InstallGamblingModule(building, (Label)"gambling-den-small");
		return "Done!";
	}

	private string CheatAddGamblingHouse(string[] args)
	{
		if (args.Length < 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 1, "<building-id> [<module-id>]");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid building id, number expected", args, 1, "<building-id> [<module-id>]");
		}
		string text = ((args.Length >= 4) ? args[3] : "gambling-den-small");
		var (entity, result2) = CheatFindBuilding(result);
		if (entity == null)
		{
			return result2;
		}
		if (IsOwnedGamblingHouse(entity))
		{
			return "Already a gambling house";
		}
		_player.territory.ScopeOutAndTakeOverResidence(entity);
		InstallGamblingModule(entity, (Label)text);
		return $"Added gambling module {text} at {entity}";
	}

	private string CheatRemoveGamblingHouse(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 1, "<building-id>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid building id, number expected", args, 1, "<building-id>");
		}
		var (entity, result2) = CheatFindBuilding(result);
		if (entity == null)
		{
			return result2;
		}
		if (!IsOwnedGamblingHouse(entity))
		{
			return "Not a gambling house";
		}
		_player.territory.ClearControlledAndResetBiz(entity, refreshRespect: true, attacked: false);
		return $"Removed gambling from {entity}";
	}

	private string CheatAddDebtorAndAssign(string[] args)
	{
		if (args.Length != 4)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 1, "<person-id> <building-id>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid person id, number expected", args, 1, "<person-id> <building-id>");
		}
		if (!int.TryParse(args[3], out var result2))
		{
			return DebugConsoleEntry.InvalidParam("Invalid building id, number expected", args, 1, "<person-id> <building-id>");
		}
		Entity entity = Game.ctx.entityman.FindByIndex(result);
		if (entity?.components.person == null)
		{
			return "Person id does not correspond to a person!";
		}
		Entity entity2 = Game.ctx.entityman.FindByIndex(result2);
		if (entity2?.components.building == null)
		{
			return "Building id does not correspond to a building!";
		}
		PlayerMeetWithGambler(entity);
		AssignDebtorToResidenceOnMap(entity, entity2);
		return $"Created debtor {entity} and moved in to {entity2}";
	}

	private string CheatRemoveDebtorAndUnassign(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 1, "<person-id>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid person id, number expected", args, 1, "<person-id>");
		}
		Entity entity = Game.ctx.entityman.FindByIndex(result);
		if (entity?.components.person == null)
		{
			return "Person id does not correspond to a person!";
		}
		Entity entity2 = entity.data.person.resassigned.FindEntity();
		if (entity2 == null)
		{
			return "This person is not assigned to a building";
		}
		UnassignDebtorFromResidenceOnMap(entity, entity2);
		return $"Unassigned debtor {entity} from building {entity2}";
	}
}
