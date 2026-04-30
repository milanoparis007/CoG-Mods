using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Player.Commands;
using Game.Session.Quests;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.Combat;
using Game.UI.Session.Popups;
using Game.UI.Session.Tickers;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public class ConvoCallbacks
{
	private ConversationController _ctrl;

	private Dictionary<string, Func<ConvoButton, OnShowResult>> _onshows = new Dictionary<string, Func<ConvoButton, OnShowResult>>();

	private Dictionary<string, Func<ConvoButton, OnClickResult>> _onclicks = new Dictionary<string, Func<ConvoButton, OnClickResult>>();

	private Dictionary<string, Func<ConvoButton, OnPreshowResult>> _onpreshows = new Dictionary<string, Func<ConvoButton, OnPreshowResult>>();

	public static List<string> AllCallbackNames;

	private const float CASH_FRACT = 0.8f;

	private static int NUMBER_OF_SOURCES_TO_REVEAL;

	internal ConversationController Ctrl => _ctrl;

	internal ConversationModel Model => _ctrl.Model;

	internal VisitState Visit => _ctrl.Model.visit;

	internal PlayerInfo Player => _ctrl.Model.visit.GetPlayer();

	internal void Initialize(ConversationController ctrl)
	{
		_ctrl = ctrl;
		InitializeCallbacks();
	}

	internal void Release()
	{
		_ctrl = null;
	}

	private static IEnumerable<MethodInfo> FindAllOnShowFunctions()
	{
		return TypeUtils.GetMethodsBySig(typeof(ConvoCallbacks), typeof(OnShowResult), typeof(ConvoButton));
	}

	private static IEnumerable<MethodInfo> FindAllOnClickFunctions()
	{
		return TypeUtils.GetMethodsBySig(typeof(ConvoCallbacks), typeof(OnClickResult), typeof(ConvoButton));
	}

	private static IEnumerable<MethodInfo> FindAllOnPreshowFunctions()
	{
		return TypeUtils.GetMethodsBySig(typeof(ConvoCallbacks), typeof(OnPreshowResult), typeof(ConvoButton));
	}

	static ConvoCallbacks()
	{
		NUMBER_OF_SOURCES_TO_REVEAL = 5;
		AllCallbackNames = new List<string>();
		AllCallbackNames.AddRange(from m in FindAllOnShowFunctions()
			select m.Name);
		AllCallbackNames.AddRange(from m in FindAllOnClickFunctions()
			select m.Name);
		AllCallbackNames.AddRange(from m in FindAllOnPreshowFunctions()
			select m.Name);
	}

	private void InitializeCallbacks()
	{
		foreach (MethodInfo item in FindAllOnShowFunctions())
		{
			_onshows.Add(item.Name, MakeOnShowCallback(this, item));
		}
		foreach (MethodInfo item2 in FindAllOnClickFunctions())
		{
			_onclicks.Add(item2.Name, MakeOnClickCallback(this, item2));
		}
		foreach (MethodInfo item3 in FindAllOnPreshowFunctions())
		{
			_onpreshows.Add(item3.Name, MakeOnPreshowCallback(this, item3));
		}
	}

	private static Func<ConvoButton, OnShowResult> MakeOnShowCallback(object me, MethodInfo m)
	{
		return (ConvoButton button) => (OnShowResult)m.Invoke(me, new object[1] { button });
	}

	private static Func<ConvoButton, OnClickResult> MakeOnClickCallback(object me, MethodInfo m)
	{
		return (ConvoButton button) => (OnClickResult)m.Invoke(me, new object[1] { button });
	}

	private static Func<ConvoButton, OnPreshowResult> MakeOnPreshowCallback(object me, MethodInfo m)
	{
		return (ConvoButton button) => (OnPreshowResult)m.Invoke(me, new object[1] { button });
	}

	public OnShowResult ProcessOnShow(ConvoButton button)
	{
		return ProcessCallback(_onshows, button.state.def.onShow, button);
	}

	public OnClickResult ProcessOnClick(ConvoButton button)
	{
		return ProcessCallback(_onclicks, button.state.def.onClick, button);
	}

	public OnPreshowResult ProcessOnPreshow(ConvoButton button)
	{
		return ProcessCallback(_onpreshows, button.state.def.onPreshow, button);
	}

	private TOut ProcessCallback<TIn, TOut>(Dictionary<string, Func<TIn, TOut>> dict, string key, TIn input)
	{
		if (key == null)
		{
			return default(TOut);
		}
		Func<TIn, TOut> func = dict.FindOrNull(key);
		if (func == null)
		{
			Logger.Error("Unknown callback name: " + key);
			return default(TOut);
		}
		return func(input);
	}

	internal OnClickResult Goodbye(ConvoButton _)
	{
		return OnClickResult.END_CONVERSATION;
	}

	private void ShowSelectionDialog<T>(T data, string header, Label onSelection, Label onNone) where T : ConvoData, IConvoDataWithSelector
	{
		if (data == null || !data.HasAny)
		{
			TimerUtil.RunNextFrame(delegate
			{
				_ctrl.JumpToConvoState(onNone, data);
			});
		}
		else
		{
			List<EntityID> entries = data.Entries;
			Game.serv.ui.AddPopup(new SelectEntityPopup(entries, Visit.npc.Id, header, callback));
		}
		void callback(EntityID target)
		{
			if (data.SelectTarget(target))
			{
				_ctrl.JumpToConvoState(onSelection, data);
			}
			else
			{
				_ctrl.OnBackButtonPress();
			}
		}
	}

	internal OnShowResult CancelBuySellSelection(ConvoButton button)
	{
		button.state.data = null;
		return OnShowResult.CONTINUE;
	}

	internal OnClickResult ShowNextBuySellState(ConvoButton button)
	{
		ConvoDataBuySell data = button.GetData<ConvoDataBuySell>();
		Label key = ((0 == 0) ? (data.playerBuys ? ConversationConstants.BUY_SELECT_CONVO : ConversationConstants.SELL_SELECT_CONVO) : (data.playerBuys ? ConversationConstants.BUY_TERRITORY_PROBLEM : ConversationConstants.SELL_TERRITORY_PROBLEM));
		_ctrl.JumpToConvoState(key, data);
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ShowBuySellPopup(ConvoButton button)
	{
		ConvoDataBuySell data = button.GetData<ConvoDataBuySell>();
		_ctrl.View.ShowItemPicker(_ctrl.Model.state, OkHandler, CancelHandler);
		return OnClickResult.PAUSE_CONVERSATION;
		void CancelHandler()
		{
			_ctrl.Model.SetForced(null);
			_ctrl.OnBackButtonPress();
		}
		void OkHandler(QtyAndDir qtyAndDir)
		{
			(bool success, Fixnum playerMoneyDelta) tuple = BuySellUtils.ExecuteHumanBuySell(Player, Visit, data, qtyAndDir, scheduled: false);
			Price price = tuple.playerMoneyDelta;
			bool item = tuple.success;
			Price playerDelta = price;
			Visit.ConsumeConvoActionsHelper();
			if (item)
			{
				Game.ctx.events.EnqueueOnce(SessionEventType.ConvoBuySellStep, PlayerID.HumanPlayer);
			}
			var (text, forced) = MakeBuySellSummaries(data, qtyAndDir, playerDelta);
			_ctrl.View.AddPlayerBlurb(text);
			_ctrl.Model.SetForced(forced);
			_ctrl.JumpToConvoState(ConversationConstants.TOPLEVEL_CONVO, null);
		}
	}

	private (string player, string npc) MakeBuySellSummaries(ConvoDataBuySell data, QtyAndDir qtyAndDir, Price playerDelta)
	{
		string text = Loc.FormatNumber(qtyAndDir.qty.Abs);
		string iconAndName = data.FindResource().GetIconAndName();
		string text2 = Loc.Price(playerDelta.Abs);
		string item;
		string item2;
		if (qtyAndDir.IsToPlayer)
		{
			item = Loc.Get("convo.buysell-summary-bought", "qty", text, "items", iconAndName, "money", text2);
			item2 = Loc.Get("convo.buysell-summary-bought-continue");
		}
		else
		{
			item = Loc.Get("convo.buysell-summary-sold", "qty", text, "items", iconAndName, "money", text2);
			item2 = Loc.Get("convo.buysell-summary-sold-continue");
		}
		return (player: item, npc: item2);
	}

	internal OnShowResult StorePriceMultiplier(ConvoButton button)
	{
		ConvoDataBuySell data = button.GetData<ConvoDataBuySell>();
		Fixnum multiplier = button.state.def.multiplier;
		data.UpdateMultiplier(multiplier);
		return OnShowResult.CONTINUE;
	}

	internal OnClickResult ExecutePriceMultiplier(ConvoButton button)
	{
		ConvoDataBuySell data = button.GetData<ConvoDataBuySell>();
		Model.visit.biz?.components.biz.SetDiscount(Model.visit.pid, data.FindResource(), data.multiplier);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreDemandToCloseOutpost(ConvoButton _)
	{
		DemandsTracker.BribeResult bribeInfo = Game.ctx.simman.demands.FindBribeToComplyData(Visit.pid, Demand.Target.MakeForBizOwner(Visit.npc));
		PlayerID outpost = Visit.building.data.building.outpost;
		return OnShowResult.SetButtonData(new ConvoDataCloseOutpost(Visit.GetBldgNodeID(), outpost, bribeInfo));
	}

	internal OnClickResult ExecuteCloseOutpostRequest(ConvoButton button)
	{
		ConvoDataCloseOutpost data = button.GetData<ConvoDataCloseOutpost>();
		Demand demand = Game.ctx.simman.demands.PerformRequestOwnerToComply(Model.visit, Demand.Type.CloseOutpost);
		data.demandState = demand.state;
		if (data.demandState == Demand.State.Compliant)
		{
			ExecuteCloseOutpost(data);
		}
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.PeepsThreatened, 1);
		return OnClickResult.CONTINUE;
	}

	private void ExecuteCloseOutpost(ConvoDataCloseOutpost data)
	{
		OutpostID outpost = new OutpostID(Visit.building.Id);
		data.owner.FindPlayer().outposts.RemoveOutpost(outpost, PlayerOutposts.RemovalReason.Stolen, Visit.pid, Visit.crew.peepId, removeMeAsNodeOwner: true);
		Game.ctx.simman.demands.ForceExpireAllDemands(Visit);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveCloseOutpost, EntityID.INVALID, Visit.GetPlayer().PID, data.owner));
		string message = Loc.Get("ui.tickers.outpost.ai-stolen", "groupname", data.GangName);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.FrontsAttacked, 1);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.OUTPOST_AI_STOLEN, TickerTitle.OUTPOST_AI_STOLEN, message, Visit.building.Id);
	}

	internal OnClickResult ExecuteCloseOutpostBribe(ConvoButton button)
	{
		ConvoDataCloseOutpost data = button.GetData<ConvoDataCloseOutpost>();
		Demand demand = Game.ctx.simman.demands.PerformBribeToComply(Model.visit, Demand.Type.CloseOutpost);
		data.demandState = demand.state;
		if (data.demandState == Demand.State.Compliant)
		{
			ExecuteCloseOutpost(data);
		}
		else
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.OutpostStealFailed, data.owner.FindPlayer().PID, Model.visit.building.Id);
		}
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteCloseOutpostAttack(ConvoButton button)
	{
		ConvoDataCloseOutpost data = button.GetData<ConvoDataCloseOutpost>();
		Demand demand = Game.ctx.simman.demands.PerformForceOwnerToComply(Model.visit, Demand.Type.CloseOutpost);
		data.demandState = demand.state;
		if (data.demandState == Demand.State.Compliant)
		{
			ExecuteCloseOutpost(data);
		}
		else
		{
			Game.ctx.events.EnqueueOnce(SessionEventType.OutpostStealFailed, data.owner.FindPlayer().PID, Model.visit.building.Id);
		}
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreCombat(ConvoButton _)
	{
		CrewAssignment crew = Visit.crew;
		AgentComponent agent = Visit.npc.components.agent;
		if (agent == null)
		{
			return OnShowResult.CONTINUE;
		}
		CrewAssignment target = agent.FindCrewAssignment();
		return OnShowResult.SetButtonData(new ConvoDataCombat
		{
			attacker = crew,
			target = target
		});
	}

	internal OnClickResult ExecuteCombat(ConvoButton button)
	{
		NodeID nid = button.GetData<ConvoDataCombat>().attacker.GetPeep().data.agent.nid;
		PlayerInfo human = Game.ctx.players.Human;
		List<Entity> mycrew = human.crew.FindAllDriversAtNode(nid).SelectIntoNewList((CrewAssignment crew) => crew.GetPeep());
		List<Entity> enemies = CombatManager.GetAttackTargetsAtNode(human.PID, nid).SelectIntoNewList((EntityID eid) => eid.FindEntity());
		Game.serv.ui.AddPopup(new CombatPopupPlanning(mycrew, enemies));
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult ExecuteBuildingCombat(ConvoButton _)
	{
		PlayerID pid = Model.visit.pid;
		Entity building = Model.visit.building;
		PlayerID pid2 = building.data.building.controlled.pid;
		Game.ctx.simman.combat.AttackBuilding(building, pid);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.BuildingsAttacked, 1);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult ExecuteHealReward(ConvoButton _)
	{
		CrewAssignment crew = Model.visit.crew;
		HumanCommandValidator humanCommandValidator = HumanCommandValidator.FindValidator(CommandType.Heal);
		if (humanCommandValidator.Validate(Model.visit.pid, crew).IsEnabled)
		{
			humanCommandValidator.OnHumanButtonClick(crew, deselectWhenDone: false);
		}
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreCopDonation(ConvoButton _)
	{
		var (price, days) = CopUtil.FindPrecinctOrNull(Visit.npc).ai.precinct.ProduceDonationInfo(Visit);
		return OnShowResult.SetButtonData(new ConvoDataCopsDonation(price, days));
	}

	internal OnClickResult ExecuteCopDonation(ConvoButton button)
	{
		ConvoDataCopsDonation data = button.GetData<ConvoDataCopsDonation>();
		CopUtil.FindPrecinctOrNull(Visit.npc).ai.precinct.StartHumanDonation(Visit, data.days, data.price);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.CopsBribed, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreCopFedHint(ConvoButton _)
	{
		PlayerInfo playerInfo = CopUtil.FindPrecinctOrNull(Visit.npc);
		return OnShowResult.SetButtonData(new ConvoDataCopsFedHint(nextAskDate: playerInfo.ai.precinct.GetFedHintStatus().expiration, rival: playerInfo.ai.precinct.PickRandomGangForFedHint(Visit.pid), precinct: playerInfo.PID));
	}

	internal OnClickResult ExecuteCopFedHint(ConvoButton button)
	{
		ConvoDataCopsFedHint data = button.GetData<ConvoDataCopsFedHint>();
		CopUtil.FindPrecinctOrNull(Visit.npc).ai.precinct.StartFedHintHuman(data.rival);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreCopJailOffer(ConvoButton _)
	{
		ArrestEntry arrestEntry = (from e in Game.ctx.simman.cops.FindAllArrestsFor(Visit.pid)
			where !e.paidOff
			select e).FirstOrDefault();
		return OnShowResult.SetButtonData(new ConvoDataCopsJailBribe(Visit.pid, arrestEntry.peepId, arrestEntry.trialDate, arrestEntry.payOffCost));
	}

	internal OnClickResult ExecuteCopJailOffer(ConvoButton button)
	{
		ConvoDataCopsJailBribe data = button.GetData<ConvoDataCopsJailBribe>();
		CopUtil.FindPrecinctOrNull(Visit.npc).ai.precinct.StartHumanPayoff(Visit, data.crewId, data.payOffCost);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.CopsBribed, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreTicketCrewIntroChoice(ConvoButton _)
	{
		return OnShowResult.SetButtonData(new ConvoDataCrewHire(Player.crew.CrewGrowth.GetBestCrewCandidateFrom(Visit.npc.Id), Visit.npc.Id));
	}

	internal OnClickResult ShowCrewHirePopup(ConvoButton button)
	{
		ConvoDataCrewHire data = button.GetData<ConvoDataCrewHire>();
		Entity candidate = data.targetId.FindEntity();
		string crewCandidateDescription = Player.crew.CrewGrowth.GetCrewCandidateDescription(Visit.npc, candidate);
		OkPopup.ShowOk(data.targetId, crewCandidateDescription, delegate
		{
			_ctrl.JumpToConvoState(ConversationConstants.HIRECREW_CONFIRM_CONVO, data);
		});
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ExecuteCrewHire(ConvoButton button)
	{
		ConvoDataCrewHire data = button.GetData<ConvoDataCrewHire>();
		Player.crew.HireNewCrewMemberUnassigned(data.targetId.FindEntity(), data.introducerId.FindEntity());
		string peepFullName = NameUtils.GetPeepFullName(data.targetId);
		string message = Loc.Get("ui.crewinfo.newhire", "name", peepFullName);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.CREWGAIN, TickerTitle.CREWGAIN, message, Game.ctx.players.Human.territory.Safehouse);
		Game.ctx.simman.hints.ShowCrewHint();
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreDemandToAllowBiz(ConvoButton _)
	{
		return DemandHelper(Demand.Type.AllowBizAccess);
	}

	public OnShowResult StoreDemandToStopAggro(ConvoButton _)
	{
		return DemandHelper(Demand.Type.StopAggro);
	}

	public OnShowResult StoreDemandToStopHarassing(ConvoButton _)
	{
		return DemandHelper(Demand.Type.StopHarassing);
	}

	public OnShowResult StoreDemandToStopStealingOutpost(ConvoButton _)
	{
		return DemandHelper(Demand.Type.StopStealingOutpost);
	}

	private OnShowResult DemandHelper(Demand.Type type)
	{
		Demand.Target target = Demand.Target.MakeForPlayer(Model.visit.npc.components.agent.GetPlayer().PID);
		return OnShowResult.SetButtonData(new ConvoDataDemand(type, target));
	}

	public OnClickResult ExecuteConsiderDemand(ConvoButton button)
	{
		ConvoDataDemand data = button.GetData<ConvoDataDemand>();
		data.target.pid.FindPlayer().ai.social.RespondToRequestToComply(PlayerID.HumanPlayer, data.type, Visit.GetBldgNodeID());
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.PeepsThreatened, 1);
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreDemandPayment(ConvoButton button)
	{
		Fixnum cash = Game.serv.globals.settings.people.social.demands.cashcost.Evaluate(Visit.MakeOwnerModQuery());
		ConvoDataDemand data = button.GetData<ConvoDataDemand>();
		data.price = new Price(cash);
		return OnShowResult.SetButtonData(data);
	}

	public OnClickResult ExecuteDemandPaymentCash(ConvoButton button)
	{
		ConvoDataDemand data = button.GetData<ConvoDataDemand>();
		PlayerInfo human = Game.ctx.players.Human;
		PlayerInfo playerInfo = data.target.pid.FindPlayer();
		human.finances.DoChangeMoneyOnPlayerPeep(data.price, MoneyReason.DemandPayment);
		playerInfo.finances.DoChangeMoneyOnPlayerPeep(-data.price, MoneyReason.DemandPayment);
		Game.ctx.simman.demands.ExtendDemandToAdd(human.PID, data.target, data.type, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	public OnClickResult ExecuteDemandPaymentTickets(ConvoButton button)
	{
		ConvoDataDemand data = button.GetData<ConvoDataDemand>();
		PlayerInfo human = Game.ctx.players.Human;
		human.social.SpendTickets(Model.visit);
		Game.ctx.simman.demands.ExtendDemandToAdd(human.PID, data.target, data.type, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreCasinoInformation(ConvoButton _)
	{
		Entity building = Visit.building;
		Fixnum fixnum = building.components.modules.gambling.FindMinimumOperationalValue(Visit);
		Fixnum cash = ModulesUtil.GetInventory(building).data.money.cash;
		Fixnum fixnum2 = cash - fixnum;
		return OnShowResult.SetButtonData(new ConvoDataGamblingCollect(vehAmt: Game.ctx.players.Human.finances.GetMoney(Visit.vehicle), delta: fixnum2, current: new Money(cash), operation: new Money(fixnum), casino: building.Id, selected: EntityID.INVALID));
	}

	internal OnClickResult ShowTransferMoneyPopup(ConvoButton button)
	{
		ConvoDataGamblingCollect data = button.GetData<ConvoDataGamblingCollect>();
		_ctrl.View.ShowMoneyPicker(data, OkHandler, CancelHandler);
		return OnClickResult.PAUSE_CONVERSATION;
		void CancelHandler()
		{
			_ctrl.Model.SetForced(null);
			_ctrl.JumpToConvoState(ConversationConstants.TOPLEVEL_GAMBLING_MANAGER, null);
		}
		void OkHandler(QtyAndDir delta)
		{
			Game.ctx.players.Human.gambling.ExecuteTransferGamblingHouseMoney(Visit, data, delta);
			Game.ctx.events.EnqueueOnce(SessionEventType.CasinoMoneyChange, PlayerID.HumanPlayer);
			_ctrl.View.AddPlayerBlurb(delta.toBldg ? Loc.Get("convo.gambling.collect.human-epilogue.to-building", "delta", Loc.Money(delta.qty)) : Loc.Get("convo.gambling.collect.human-epilogue.to-vehicle", "delta", Loc.Money(delta.qty)));
			_ctrl.Model.SetForced(Loc.Get("convo.gambling.collect.npc-epilogue"));
			_ctrl.JumpToConvoState(ConversationConstants.TOPLEVEL_GAMBLING_MANAGER, null);
		}
	}

	internal OnClickResult ExecuteBanGambler(ConvoButton button)
	{
		Entity gambler = button.GetData<ConvoDataGamblingCollect>().selected.FindEntity();
		GamblerState state = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
		Game.ctx.players.Human.gambling.DoRemoveAndBanGambler(gambler, state);
		BuildingUtil.FindOwnerOrManagerForAnyBuilding(Visit.building)?.components.agent.IncrementStat(CrewStats.GamblersKicked, 1);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnShowResult StoreDebtorRepayments(ConvoButton _)
	{
		PlayerGambling gambling = Game.ctx.players.Human.gambling;
		List<RepaymentChoiceAndSuccess> repayments = gambling.GetRepayments(Visit.npc);
		GamblerState gamblerState = gambling.FindGamblerState(Visit.npc);
		ModQuery query = gambling.MakeModQueryForGambler(gamblerState);
		return OnShowResult.SetButtonData(new ConvoDataGamblingDebtor(nextCredit: gambling.FindNextDebtLevel(gamblerState).cash.Evaluate(query), rollAmount: gamblerState.rollAmount = gambling.RollIncompleteRepayment(gamblerState), repayments: repayments, selected: Label.NULL, success: 0, gambler: Visit.npc.Id));
	}

	internal OnShowResult StorePayCashRepayment(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		return OnShowResult.SetButtonData(new ConvoDataGamblingDebtor(data.repayments, GamblingConstants.REPAYMENT_CASH, data.nextCredit, data.success, data.gamblerId, data.rollMoney));
	}

	internal OnShowResult StorePendingRepayment(ConvoButton _)
	{
		Label repaymentInProgress = Game.ctx.players.Human.gambling.FindGamblerState(Visit.npc).repaymentInProgress;
		return OnShowResult.SetButtonData(new ConvoDataGamblingDebtor(new List<RepaymentChoiceAndSuccess>(), repaymentInProgress, new Fixnum(0), new Fixnum(0), Visit.npc.Id, new Fixnum(0)));
	}

	internal OnClickResult DoAttachRepayment(ConvoButton button)
	{
		PlayerGambling gambling = Game.ctx.players.Human.gambling;
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		if (data.selected.IsNotSet)
		{
			return OnClickResult.END_CONVERSATION;
		}
		TimerUtil.RunNextFrame(delegate
		{
			gambling.SelectRepaymentForDebtor(Visit, data.selected, data.success);
		});
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GamblersExtorted, 1);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult DoExecuteGrantsOnRepayment(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		Entity gambler = data.gamblerId.FindEntity();
		GamblerState state = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
		if (data.selected.IsNotSet)
		{
			return OnClickResult.END_CONVERSATION;
		}
		TimerUtil.RunNextFrame(delegate
		{
			GamblingRepayment gamblingRepayment = Game.serv.globals.settings.gambling.FindRepaymentById(data.selected);
			string[] array = data.MakeReplacements(Model.visit, 0);
			gamblingRepayment.grants.ApplyAll(new GrantContext(Visit));
			Game.ctx.players.Human.gambling.DoRemoveAndBanGambler(gambler, state);
			TickerBar tickers = Game.ctx.hud.tickers;
			TickerIcon gAMBLING_UPDATE = TickerIcon.GAMBLING_UPDATE;
			TickerTitle cASINO_UPDATE = TickerTitle.CASINO_UPDATE;
			string ticker = gamblingRepayment.convo.ticker;
			object[] replacements = array;
			tickers.AddTextTicker(gAMBLING_UPDATE, cASINO_UPDATE, Loc.Get(ticker, replacements), gambler.Id);
		});
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult DoCleanupGamblerOnFail(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		Entity gambler = data.gamblerId.FindEntity();
		GamblerState state = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.players.Human.gambling.DoRemoveAndBanGambler(gambler, state);
		});
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult ExecuteExtendCredit(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		Entity gambler = data.gamblerId.FindEntity();
		GamblerState state = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.players.Human.gambling.PayForExtendCredit(state, Visit);
			Game.ctx.players.Human.gambling.DoExtendGamblerCredit(gambler);
		});
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GamblersForgiven, 1);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult DoAttachPayCash(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		Entity gambler = data.gamblerId.FindEntity();
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.players.Human.gambling.SelectPayCash(gambler);
		});
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GamblersExtorted, 1);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult ExecuteRepayCashLess(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		Entity gambler = data.gamblerId.FindEntity();
		GamblerState state = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.players.Human.finances.DoChangeMoney(Visit.vehicle, new Price(data.rollMoney), MoneyReason.GamblingDebt);
			Game.ctx.players.Human.gambling.DoRemoveAndBanGambler(gambler, state);
		});
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult ExecuteRepayCashFull(ConvoButton button)
	{
		ConvoDataGamblingDebtor data = button.GetData<ConvoDataGamblingDebtor>();
		Entity gambler = data.gamblerId.FindEntity();
		GamblerState state = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.players.Human.finances.DoChangeMoney(Visit.vehicle, new Price(-state.cash.cash), MoneyReason.GamblingDebt);
			Game.ctx.players.Human.gambling.DoRemoveAndBanGambler(gambler, state);
		});
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.PeepsThreatened, 1);
		Game.ctx.events.SendImmediate(SessionEventType.UIDebtorChange);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult ShowCasinoBuildPopup(ConvoButton _)
	{
		ConvoDataGamblingHouseSelection data = GamblingHouseSelectionUtil.MakeCasinoBuildData(Visit);
		ShowSelectionDialog(data, Loc.Get("convo.select.gambling.header"), ConversationConstants.TICKET_GAMBLING_HOUSE_TYPE_SELECT, ConversationConstants.TICKET_GAMBLING_HOUSE_NONE);
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ExecuteGrantGamblingHouse(ConvoButton button)
	{
		ConvoDataGamblingHouseSelection data = button.GetData<ConvoDataGamblingHouseSelection>();
		Entity building = data.selected.targetId.FindEntity();
		TimerUtil.RunNextFrame(delegate
		{
			PlayerInfo human = Game.ctx.players.Human;
			human.gambling.PayForStartCasino(Visit, data.gamblingModuleId);
			human.territory.ScopeOutAndTakeOverResidence(building);
			human.gambling.InstallGamblingModule(building, data.gamblingModuleId);
			Game.ctx.players.Human.crew.CrewGrowth.OnPlayerTurnStarted(Game.ctx.players.Human.crew);
			Game.ctx.simman.hints.ShowBuildingGainedHint();
			PersonInfoUtil.TweenCameraToEntity(building.Id);
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerBuildingTakeoverImmediate, building.Id, PlayerID.HumanPlayer));
		});
		return OnClickResult.END_CONVERSATION;
	}

	internal OnShowResult StoreGangConvoInitiative(ConvoButton _)
	{
		return OnShowResult.SetButtonData(new ConvoDataGangConvoInit(Visit.npc.data.agent.pid.FindPlayer().PID));
	}

	internal OnClickResult ExecuteGangConvoInitiativeAgree(ConvoButton button)
	{
		ConvoDataGangConvoInit data = button.GetData<ConvoDataGangConvoInit>();
		PlayerInfo playerInfo = data.other.FindPlayer();
		switch (data.topic)
		{
		case ConvoInitiative.Topic.TiedHouse:
		{
			Entity biz = BuildingUtil.FindBizForBuilding(data.targetId);
			playerInfo.ai.business.FinishTakingHumanTiedHouse(Visit, biz, data.days, data.price);
			break;
		}
		case ConvoInitiative.Topic.TruceRequest:
			Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
			CombatAdvisor.StartTruceSymmetric(Visit.pid, Visit.crew.peepId, playerInfo.PID, data.days);
			Game.ctx.events.SendImmediate(SessionEventType.AchieveTruceRequest);
			break;
		case ConvoInitiative.Topic.ExpansionHalt:
			Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
			TerritoryAdvisor.StartExpansionTreatySymmetric(Visit.pid, Visit.crew.peepId, playerInfo.PID, data.days);
			Game.ctx.events.SendImmediate(SessionEventType.AchieveTruceRequest);
			break;
		case ConvoInitiative.Topic.JointWar:
			Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
			CombatAdvisor.StartJointWarSymmetric(Visit.pid, Visit.crew.peepId, playerInfo.PID, data.days, data.targetId.FindEntity().data.agent.pid);
			Game.ctx.events.SendImmediate(SessionEventType.AchieveTruceRequest);
			break;
		case ConvoInitiative.Topic.StolenOutpost:
			Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
			TerritoryAdvisor.StartOutpostStealAgreementSymmetric(Visit.pid, Visit.crew.peepId, playerInfo.PID, data.days);
			Game.ctx.events.SendImmediate(SessionEventType.AchieveTruceRequest);
			break;
		}
		playerInfo.ai.social.FinishConvoInitiative();
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteGangConvoInitiativeDecline(ConvoButton button)
	{
		button.GetData<ConvoDataGangConvoInit>().other.FindPlayer().ai.social.FinishConvoInitiative();
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreHumanAsksForTruce(ConvoButton _)
	{
		PlayerInfo playerInfo = Visit.npc.data.agent.pid.FindPlayer();
		SocialAdvisor.RequestParams requestParams = playerInfo.ai.social.ComputeRequestParametersForHumanAskingUs(Visit, ConvoInitiative.Topic.TruceRequest);
		return OnShowResult.SetButtonData(new ConvoDataGangRequests(PlayerID.HumanPlayer, playerInfo.PID, requestParams.accept, new Price(requestParams.cost), requestParams.days));
	}

	internal OnClickResult ExecuteGangConvoBuyTruce(ConvoButton button)
	{
		ConvoDataGangRequests data = button.GetData<ConvoDataGangRequests>();
		PlayerID pid = Visit.npc.data.agent.pid;
		Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
		CombatAdvisor.StartTruceSymmetric(Visit.pid, Visit.crew.peepId, pid, data.days);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GangTruces, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreHumanAsksForExpansionTreaty(ConvoButton _)
	{
		PlayerInfo playerInfo = Visit.npc.data.agent.pid.FindPlayer();
		SocialAdvisor.RequestParams requestParams = playerInfo.ai.social.ComputeRequestParametersForHumanAskingUs(Visit, ConvoInitiative.Topic.ExpansionHalt);
		return OnShowResult.SetButtonData(new ConvoDataGangRequests(PlayerID.HumanPlayer, playerInfo.PID, requestParams.accept, new Price(requestParams.cost), requestParams.days));
	}

	internal OnClickResult ExecuteGangConvoBuyExpansionTreaty(ConvoButton button)
	{
		ConvoDataGangRequests data = button.GetData<ConvoDataGangRequests>();
		PlayerID pid = Visit.npc.data.agent.pid;
		Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
		TerritoryAdvisor.StartExpansionTreatySymmetric(Visit.pid, Visit.crew.peepId, pid, data.days);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GangTruces, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreHumanAsksForJointWar(ConvoButton button)
	{
		ConvoDataGangRequests data = button.GetData<ConvoDataGangRequests>();
		PlayerInfo playerInfo = Visit.npc.data.agent.pid.FindPlayer();
		SocialAdvisor.RequestParams requestParams = playerInfo.ai.social.ComputeRequestParametersForHumanAskingUs(Visit, ConvoInitiative.Topic.JointWar, data.thirdParty);
		return OnShowResult.SetButtonData(new ConvoDataGangRequests(PlayerID.HumanPlayer, playerInfo.PID, requestParams.accept, new Price(requestParams.cost), requestParams.days, data.thirdParty));
	}

	internal OnClickResult ExecuteGangConvoBuyJointWar(ConvoButton button)
	{
		ConvoDataGangRequests data = button.GetData<ConvoDataGangRequests>();
		PlayerID pid = Visit.npc.data.agent.pid;
		Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
		CombatAdvisor.StartJointWarSymmetric(Visit.pid, Visit.crew.peepId, pid, data.days, data.thirdParty);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GangTruces, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreHumanAsksForOutpostNoStealAgreement(ConvoButton _)
	{
		PlayerInfo playerInfo = Visit.npc.data.agent.pid.FindPlayer();
		SocialAdvisor.RequestParams requestParams = playerInfo.ai.social.ComputeRequestParametersForHumanAskingUs(Visit, ConvoInitiative.Topic.StolenOutpost);
		return OnShowResult.SetButtonData(new ConvoDataGangRequests(PlayerID.HumanPlayer, playerInfo.PID, requestParams.accept, new Price(requestParams.cost), requestParams.days));
	}

	internal OnClickResult ExecuteGangConvoBuyOutpostNoStealAgreement(ConvoButton button)
	{
		ConvoDataGangRequests data = button.GetData<ConvoDataGangRequests>();
		PlayerID pID = Visit.npc.data.agent.pid.FindPlayer().PID;
		Visit.GetPlayer().finances.DoChangeMoneyOnCrew(Visit, data.price, MoneyReason.Other);
		TerritoryAdvisor.StartOutpostStealAgreementSymmetric(Visit.pid, Visit.crew.peepId, pID, data.days);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GangTruces, 1);
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreGoonBuyoutCost(ConvoButton _)
	{
		PlayerInfo playerInfo = Model.visit.npc.data.agent.pid.FindPlayer();
		ModQuery query = Model.visit.MakeOwnerModQuery();
		int num = Game.serv.globals.settings.npc.goons.FindBuyoutDef(playerInfo.ai.goon.GoonType)?.cash.Evaluate(query).RoundCoarse() ?? (-100);
		return OnShowResult.SetButtonData(new ConvoDataGoonBuyout(playerInfo, num));
	}

	public OnClickResult ExecuteGoonHireAsCrew(ConvoButton button)
	{
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveGoonHired, Visit.npc.Id, Visit.pid));
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GoonDeals, 1);
		return ExecuteGoonBuyout(button, addToHumanCrew: true);
	}

	public OnClickResult ExecuteGoonPayToMove(ConvoButton button)
	{
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveGoonPaidOff, Visit.npc.Id, Visit.pid));
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GoonDeals, 1);
		return ExecuteGoonBuyout(button, addToHumanCrew: false);
	}

	private OnClickResult ExecuteGoonBuyout(ConvoButton button, bool addToHumanCrew)
	{
		ConvoDataGoonBuyout data = button.GetData<ConvoDataGoonBuyout>();
		Game.ctx.players.Human.finances.DoChangeMoneyOnCrew(Model.visit, data.price, MoneyReason.Other);
		PlayerInfo human = Game.ctx.players.Human;
		PlayerInfo playerInfo = data.goon.FindPlayer();
		Node crewNode = Model.visit.GetCrewNode();
		Entity playerPeep = playerInfo.social.GetPlayerPeep();
		playerInfo.crew.RemoveSingletonGoonPeep(playerPeep);
		(Relationship, Fixnum, int) relationshipAndTickets = PersonInfoUtil.GetRelationshipAndTickets(playerPeep.Id);
		relationshipAndTickets.Item1.DoSpendTickets(relationshipAndTickets.Item1.GetTicketsAvailable());
		if (addToHumanCrew)
		{
			human.crew.AddToCrewUnassigned(playerPeep, null, isBoss: false);
			human.crew.CreateVehicleAndAssignCrew(human.territory.GetHeadquartersNode(), playerPeep);
			Game.ctx.simman.hints.ShowCrewHint();
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BUYOUT_HIREASCREW, TickerTitle.BUYOUT, Loc.Get("ui.tickers.buyout-hireascrew"), crewNode.id);
		}
		else
		{
			Game.ctx.simman.hints.ShowBuyoutHint();
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.BUYOUT_PAYTOMOVE, TickerTitle.BUYOUT, Loc.Get("ui.tickers.buyout-paytomove"), crewNode.id);
		}
		return OnClickResult.END_CONVERSATION;
	}

	public OnShowResult StoreGoonHitmanTarget(ConvoButton _)
	{
		PlayerInfo playerInfo = Model.visit.npc.data.agent.pid.FindPlayer();
		PlayerInfo target = Game.ctx.players.Human.meetings.FindPotentialHitmanTarget(playerInfo.PID);
		ModQuery query = Model.visit.MakeOwnerModQuery();
		Price price = new Price(Game.serv.globals.settings.npc.goons.goonCosts.hitman.cash.Evaluate(query));
		return OnShowResult.SetButtonData(new ConvoDataGoonHitman(playerInfo, target, price));
	}

	public OnClickResult ExecuteGoonHitman(ConvoButton button)
	{
		ConvoDataGoonHitman data = button.GetData<ConvoDataGoonHitman>();
		Game.ctx.players.Human.finances.DoChangeMoneyOnCrew(Model.visit, data.price, MoneyReason.Other);
		data.goon.FindPlayer().ai.combat.AddHitmanTarget(Model.visit.pid, data.target);
		Game.ctx.events.SendImmediate(SessionEventType.AchieveGoonHitman);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GoonDeals, 1);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnShowResult StoreTicketSkillChoice(ConvoButton _)
	{
		List<SkillDef> list = Player.skills.FindAllDistinctSkillsToLearn(Model.visit);
		Label selected = Label.NULL;
		if (list.Count == 1)
		{
			selected = list[0].id;
		}
		Game.serv.globals.settings.skills.ValidateAllSkillsHaveQuests(list);
		return OnShowResult.SetButtonData(new ConvoDataLearnSkill(list.Select((SkillDef x) => x.id).ToList(), Visit, selected));
	}

	internal OnClickResult ExecuteTicketSkillsInfo(ConvoButton button)
	{
		ConvoDataLearnSkill data = button.GetData<ConvoDataLearnSkill>();
		SkillDef skill = Game.serv.globals.settings.skills.GetSkill(data.skillId);
		string name = skill.GetName();
		string desc = skill.GetDesc();
		List<string> list = new HashSet<string>(PlayerSkills.ModuleUnlocksCache.GetUnlockedModuleNames(data.skillId)).OrderBy((string s) => s).ToList();
		string text = ((list.Count > 0) ? string.Join(", ", list) : "");
		string text2 = ((list.Count > 0) ? Loc.Get("convo.ticket-skills-info.unlocks", "list", text) : "");
		OkPopup.Show(Loc.Get("convo.ticket-skills-info.tmpl", "name", name, "desc", desc, "unlocks", text2));
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ExecuteTicketSkillsStartDeliveryQuest(ConvoButton button)
	{
		ConvoDataLearnSkill data = button.GetData<ConvoDataLearnSkill>();
		Player.skills.FindSkillDef(data.skillId);
		Player.skills.StartQuestFromSkillConvo(data.skillId, Visit.npc);
		Player.skills.DoPayForSkill(Visit, data.skillId);
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreSafehouseInfo(ConvoButton _)
	{
		return OnShowResult.SetButtonData(new ConvoDataSafehouseCheck());
	}

	public OnShowResult StoreLootDrop(ConvoButton _)
	{
		PlayerInfo playerInfo = Visit.npc.components.agent?.GetPlayer();
		if (playerInfo == null)
		{
			return OnShowResult.CONTINUE;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(playerInfo.territory.Safehouse);
		if (inventory == null || inventory.CalculateUsedCapacity().cubicfeet <= 0)
		{
			return OnShowResult.CONTINUE;
		}
		ResourceAndQty resourceAndQty = Game.ctx.scenario.MakeSeededRng((uint)(Visit.npc.Id.index ^ Game.ctx.clock.Now.days)).PickElement(inventory.data.contents);
		Resource resource = resourceAndQty.FindResource();
		return OnShowResult.SetButtonData(new ConvoDataLootDrop(resource.GetName(), resource.GetIcon(), resource.resid, resourceAndQty.qty));
	}

	public OnClickResult ExecuteLootDrop(ConvoButton button)
	{
		PlayerInfo playerInfo = Visit.npc.components.agent?.GetPlayer();
		PlayerInfo player = Visit.peep.components.agent.GetPlayer();
		if (playerInfo == null)
		{
			return OnClickResult.END_CONVERSATION;
		}
		InventoryModule inventory = ModulesUtil.GetInventory(playerInfo.territory.Safehouse);
		if (inventory == null || inventory.CalculateUsedCapacity().cubicfeet <= 0)
		{
			return OnClickResult.END_CONVERSATION;
		}
		ConvoDataLootDrop data = button.GetData<ConvoDataLootDrop>();
		Resource resource = Game.ctx.simman.FindResource(data.resId);
		InventoryModule inventory2 = ModulesUtil.GetInventory(Visit.crew.GetVehicle());
		Fixnum fixnum = Fixnum.Min(inventory2.HowManyResourcesCanFit(resource), data.qty);
		inventory2.data.Increment(resource.resid, fixnum);
		inventory.data.Increment(resource.resid, -fixnum);
		PlayerSkills skills = player.skills;
		if (player.IsHuman && !skills.HasResourceUnlocked(resource.resid))
		{
			skills.UnlockResource(resource.resid, startup: false);
		}
		data.received = fixnum;
		playerInfo.territory.SafehouseData.lastThiefLootDrop = Game.ctx.clock.Now;
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreGoonReward(ConvoButton _)
	{
		return OnShowResult.SetButtonData(new ConvoDataGoonRewards(Visit.npc.data.agent.pid.FindPlayer(), Visit));
	}

	public OnClickResult ExecuteGoonRewardStart(ConvoButton button)
	{
		ConvoDataGoonRewards data = button.GetData<ConvoDataGoonRewards>();
		if (data.cost.IsNonZero)
		{
			Player.finances.DoChangeMoney(Visit.vehicle, data.cost, MoneyReason.GoonLootTableBuyIn);
		}
		Price delta = new Price(new Fixnum((0f - (float)data.cost.cash) * 0.8f));
		PlayerInfo playerInfo = Visit.npc.data.agent.pid.FindPlayer();
		playerInfo.finances.DoChangeMoneyOnSafehouse(delta, MoneyReason.BusinessIncome);
		playerInfo.ai.Data.goon.rewards.AcceptOfferedReward();
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.GoonDeals, 1);
		return OnClickResult.CONTINUE;
	}

	public OnClickResult ExecuteGoonRewardTake(ConvoButton button)
	{
		ConvoDataGoonRewards data = button.GetData<ConvoDataGoonRewards>();
		Game.serv.globals.settings.npc.goons.FindLootTableEntry(data.goontype, data.reward).grants.ApplyAll(new GrantContext(Visit));
		Visit.npc.data.agent.pid.FindPlayer().ai.Data.goon.rewards.CollectReward();
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveGoonRewardTaken, Visit.npc.Id, Visit.pid));
		return OnClickResult.END_CONVERSATION;
	}

	internal OnClickResult StoreModuleIntro(ConvoButton button)
	{
		ConvoDataNPCSelection convoDataNPCSelection = TicketModuleIntroductions.MakeModuleBasedIntro(Visit);
		if (convoDataNPCSelection != null)
		{
			convoDataNPCSelection.selected = convoDataNPCSelection.entries.FirstOrDefaultFast();
		}
		else
		{
			Logger.Warning("Missing convo data in StoreModuleIntro");
		}
		button.state.data = convoDataNPCSelection;
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult StoreTicketBoostGoon(ConvoButton button)
	{
		ConvoDataNPCSelection convoDataNPCSelection = TicketGoonBoosts.MakeGoonBoostData(Visit);
		if (convoDataNPCSelection != null)
		{
			convoDataNPCSelection.selected = convoDataNPCSelection.entries.FirstOrDefaultFast();
		}
		else
		{
			Logger.Warning("Missing convo data in StoreTicketBoostGoon");
		}
		button.state.data = convoDataNPCSelection;
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ShowTicketIntroPopup(ConvoButton button)
	{
		ConvoDataNPCSelection data = TicketIntroductions.MakeBizIntroData(Visit);
		button.state.data = data;
		ShowSelectionDialog(data, Loc.Get("convo.select.intro.header"), ConversationConstants.TICKET_INTRO_CONFIRM, ConversationConstants.TICKET_INTRO_NONE);
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ExecuteTicketIntro(ConvoButton button)
	{
		TicketIntroductions.PerformIntro(button.GetData<ConvoDataNPCSelection>().selected.targetId, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteNPCSelectionZoom(ConvoButton button)
	{
		WorldPos? worldPos = BoardUtil.FindBoardPositionFor(button.GetData<ConvoDataNPCSelection>().selected.targetId);
		if (!worldPos.HasValue)
		{
			return OnClickResult.CONTINUE;
		}
		HUDUtil.GoTo(worldPos.Value, zoomIn: true, showFx: true);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ShowTicketBoostPopup(ConvoButton button)
	{
		Func<Entity, List<Entity>> filter = Game.ctx.players.Human.social.TicketActionFindBoostTargets;
		return FindBoostTargets(button, filter);
	}

	internal OnClickResult FindBoostTargets(ConvoButton button, Func<Entity, List<Entity>> filter)
	{
		ConvoDataNPCSelection data = TicketBoosts.MakeReputationBoostData(Visit, filter);
		bool valueOrDefault = Visit.npc?.data.agent?.pid.FindPlayer()?.IsJustGoon == true;
		button.state.data = data;
		Label onSelection = (valueOrDefault ? ConversationConstants.TICKET_GOONBOOST_CONFIRM : ConversationConstants.TICKET_NPCBOOST_CONFIRM);
		Label onNone = (valueOrDefault ? ConversationConstants.TICKET_GOONBOOST_NONE : ConversationConstants.TICKET_NPCBOOST_NONE);
		ShowSelectionDialog(data, Loc.Get("convo.select.boost.header"), onSelection, onNone);
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ExecuteTicketBoost(ConvoButton button)
	{
		ConvoDataNPCSelection data = button.GetData<ConvoDataNPCSelection>();
		Player.social.TicketActionPerformBoost(data.selected.targetId);
		string peepFullName = NameUtils.GetPeepFullName(data.selected.targetId);
		string message = Loc.Get("convo.select.boost.result", "name", peepFullName);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.SOCIAL, TickerTitle.DEFAULT, message, data.selected.targetId);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteTicketGoonBoost(ConvoButton button)
	{
		TicketIntroductions.PerformGoonBoostSocialAction(button.GetData<ConvoDataNPCSelection>().selected.targetId.FindEntity().Id, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	public OnClickResult DoPerformCampaignAction(ConvoButton button)
	{
		ConvoDataPolitician data = button.GetData<ConvoDataPolitician>();
		Game.serv.globals.settings.politics.FindPlayerCandidateAction(data.selectedActionId);
		Game.ctx.simman.politics.GetWardForID(data.wardId).currElection.PerformPlayerCampaignAction(PlayerID.HumanPlayer, data.selectedActionId);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.POLITICS, TickerTitle.POLITICS, Loc.Get("ui.tickers.politics.player-campaign-action", "candidate", data.candidateId.FindEntity().data.person.FullName));
		Visit.crew.GetPeep().components.agent.IncrementStat(CrewStats.PoliticalEvents, 1);
		Game.ctx.events.EnqueueOnce(SessionEventType.ElectionInteraction);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnShowResult StoreQuestActive(ConvoButton _)
	{
		QuestManager quests = Game.ctx.quests;
		QuestUUID questUUID = quests.FindActiveOrWaitingQuestForTarget(Visit.npc.Id);
		return OnShowResult.SetButtonData(new ConvoDataQuestActive(questUUID, quests.FindQuestDefAndTarget(questUUID).def?.id));
	}

	internal OnClickResult ExecuteRewardChoice(ConvoButton button)
	{
		ConvoDataQuestActive data = button.GetData<ConvoDataQuestActive>();
		Game.ctx.quests.OnRewardChoiceFinished(Visit, data.quuid, button.index, out var afterchoice);
		if (afterchoice != null)
		{
			Model.SetForced(Loc.Get(afterchoice));
		}
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteQuestDeliveryChoice(ConvoButton button)
	{
		EntityID target = Visit.npc.Id;
		ConvoDataQuestActive data = button.GetData<ConvoDataQuestActive>();
		QuestUUID questUUID = Game.ctx.quests.FindActiveQuestForTarget(target);
		if (questUUID.IsNotSet || questUUID != data.quuid)
		{
			return OnClickResult.END_CONVERSATION;
		}
		ResOrCash item = data.delivered;
		if (item.money.IsZero && item.raq.qty.IsZero)
		{
			return OnClickResult.END_CONVERSATION;
		}
		DoChangeGoodsInInventories();
		OnDeliverGoodsDone();
		return OnClickResult.PAUSE_CONVERSATION;
		void DoChangeGoodsInInventories()
		{
			InventoryModule inventory = ModulesUtil.GetInventory(Visit.vehicle);
			InventoryModule inventory2 = ModulesUtil.GetInventory(Visit.building);
			if (item.IsCash)
			{
				Price price = new Price(item.money.cash);
				Visit.GetPlayer().finances.DoChangeMoney(Visit.vehicle, -price, MoneyReason.QuestDemand);
				inventory2.data.DoChangeMoney(null, price);
			}
			else
			{
				inventory.data.Increment(item.raq.Negative);
			}
		}
		void OnDeliverGoodsDone()
		{
			DeliveryGoal.Summary ctx = new DeliveryGoal.Summary
			{
				delivered = data.delivered,
				visit = Visit
			};
			Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.ConvoQuestDeliveryStep, EntityID.INVALID, PlayerID.HumanPlayer, ctx));
			Game.ctx.quests.UpdateQuestDuringVisit(data.quuid, Visit);
			QuestWaitingRecord questWaitingRecord = Game.ctx.quests.FindWaitingQuestUnsafe(data.quuid);
			if (questWaitingRecord != null && questWaitingRecord.IsReadyForPlayerChoice)
			{
				_ctrl.JumpToConvoState(ConversationConstants.SHOW_QUEST_CHOICES, data);
			}
			else if (Game.ctx.quests.FindCompletedQuestForTarget(target, data.questid).IsSet)
			{
				_ctrl.JumpToConvoState(ConversationConstants.DELIVERY_QUEST_DONE_CONVO, data);
			}
			else
			{
				_ctrl.JumpToConvoState(ConversationConstants.DELIVERY_QUEST_PENDING_CONVO, data);
			}
		}
	}

	internal OnShowResult StoreQReqInfo(ConvoButton _)
	{
		QuestRequest questRequest = Game.ctx.quests.Requests.FindExisting(Model.visit);
		QuestDefinition def = Game.ctx.quests.FindQuestDefinition(questRequest.questid);
		return OnShowResult.SetButtonData(new ConvoDataQuestRequest(Model.visit.npc.Id, def));
	}

	internal OnClickResult ShowQReqBlurbOrDecision(ConvoButton button)
	{
		ConvoDataQuestRequest data = button.GetData<ConvoDataQuestRequest>();
		data.blurbsleft.RemoveAndReturnOrDefault(0);
		data.blurbsleft.RemoveAndReturnOrDefault(0);
		bool num = data.blurbsleft.Count <= 2;
		Label key = (num ? ConversationConstants.QREQ_DECISION_POINT : ConversationConstants.QREQ_SINGLE_BLURB);
		string text = Loc.Get(data.blurbsleft.FirstOrDefaultFast());
		if (num)
		{
			text += DescribeQuestGoals(data.questId);
		}
		Model.SetForced(text);
		_ctrl.JumpToConvoState(key, data);
		return OnClickResult.PAUSE_CONVERSATION;
		string DescribeQuestGoals(string questId)
		{
			QuestDefinition questDefinition = Game.ctx.quests.FindQuestDefinition(questId);
			if (questDefinition == null)
			{
				return "";
			}
			return "\n" + Game.ctx.quests.DescribeFutureQuestGoals(questDefinition, Model.visit?.npc?.Id ?? EntityID.INVALID, header: true);
		}
	}

	internal OnClickResult ExecuteStartQReq(ConvoButton button)
	{
		ConvoDataQuestRequest data = button.GetData<ConvoDataQuestRequest>();
		Game.ctx.quests.StartQuest(data.questId, Model.visit.npc.Id, fromRequest: true);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteExpirationFinish(ConvoButton button)
	{
		ConvoDataQuestActive data = button.GetData<ConvoDataQuestActive>();
		QuestExpirationDefinition questExpirationDefinition = data?.GetDef().expiration;
		if (questExpirationDefinition == null || data.quuid.IsNotSet)
		{
			return OnClickResult.END_CONVERSATION;
		}
		Game.ctx.quests.CompleteExpiredWaitingQuest(data.quuid, Visit);
		Model.SetForced(Loc.Get(questExpirationDefinition.locepilogue));
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult StorePickResEvent(ConvoButton button)
	{
		ResEventCandidate? resEventCandidate = Game.ctx.simman.resevents.FindPossibleResEventToCreate(PlayerID.HumanPlayer, Visit.npc);
		ConvoDataResEvent data = (resEventCandidate.HasValue ? new ConvoDataResEvent(resEventCandidate.Value) : new ConvoDataResEvent());
		button.state.data = data;
		if (resEventCandidate.HasValue)
		{
			PersonInfoUtil.TweenCameraToEntity(resEventCandidate.Value.building.FindEntity());
		}
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteResEventIntro(ConvoButton button)
	{
		ConvoDataResEvent data = button.GetData<ConvoDataResEvent>();
		if (data != null && data.IsValid)
		{
			Game.ctx.simman.resevents.AssignResEventHost(data.eventId, data.hostNpc.FindEntity(), data.building.FindEntity(), PlayerID.HumanPlayer);
		}
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteCommitToResEvent(ConvoButton _)
	{
		Game.ctx.simman.resevents.SetAttendenceRegistered(Visit);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteAttendResEvent(ConvoButton _)
	{
		ResEventData resEventData = Game.ctx.simman.resevents.SetAttendanceDone(Visit);
		if (resEventData.HasChosenResult && resEventData.chosenResult.targetNpc.IsValid)
		{
			PersonInfoUtil.TweenCameraToEntity(resEventData.chosenResult.targetNpc);
		}
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.ResVisited, 1);
		Game.ctx.events.SendImmediate(SessionEventType.AchieveResAttend);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteProduceResEventResult(ConvoButton _)
	{
		Game.ctx.simman.resevents.ProcessChosenResult(Visit);
		return OnClickResult.CONTINUE;
	}

	public OnClickResult ExecuteStartScheme(ConvoButton button)
	{
		ConvoDataScheme data = button.GetData<ConvoDataScheme>();
		SchemeDef scheme = Game.serv.globals.settings.schemes.FindSchemeById(data.selected);
		Game.ctx.players.Human.schemes.DoPaySchemeStartupCost(scheme);
		Game.ctx.players.Human.schemes.StartSchemeForCrew(data.selected, Model.visit.npc);
		return OnClickResult.END_CONVERSATION;
	}

	internal OnShowResult StoreStartOutsideOutpost(ConvoButton _)
	{
		ModQuery query = Visit.MakeOwnerModQuery();
		OutpostSettings outposts = Game.serv.globals.settings.people.social.outposts;
		int num = outposts.buildCost.Evaluate(query).RoundCoarse();
		int num2 = outposts.monthlyCost.Evaluate(query).RoundCoarse();
		string expinstall = outposts.buildCost.Explain(query, addHeader: true);
		string expmonthly = outposts.monthlyCost.Explain(query, addHeader: true);
		return OnShowResult.SetButtonData(new ConvoDataStartOutpost(inside: false, num, num2, expinstall, expmonthly));
	}

	internal OnShowResult StoreStartInsideOutpost(ConvoButton _)
	{
		ModQuery query = Visit.MakeOwnerModQuery();
		OutpostSettings outposts = Game.serv.globals.settings.people.social.outposts;
		Fixnum zERO = Fixnum.ZERO;
		int num = outposts.monthlyCost.Evaluate(query).RoundCoarse();
		string expinstall = outposts.buildCost.Explain(query, addHeader: true);
		string expmonthly = outposts.monthlyCost.Explain(query, addHeader: true);
		return OnShowResult.SetButtonData(new ConvoDataStartOutpost(inside: false, zERO, num, expinstall, expmonthly));
	}

	internal OnClickResult ExecuteOutpostCostInfo(ConvoButton button)
	{
		ConvoDataStartOutpost data = button.GetData<ConvoDataStartOutpost>();
		string text = Loc.Get("convo.outpost-info");
		if (data.install.cash.IsNotZero)
		{
			text += Loc.Get("convo.outpost.cost-startup", "startup", data.expinstall);
		}
		if (data.monthly.cash.IsNotZero)
		{
			text += Loc.Get("convo.outpost.cost-monthly", "monthly", data.expmonthly);
		}
		OkPopup.Show(text);
		return OnClickResult.PAUSE_CONVERSATION;
	}

	internal OnClickResult ExecuteStartOutpost(ConvoButton button)
	{
		ConvoDataStartOutpost data = button.GetData<ConvoDataStartOutpost>();
		if (!Player.finances.CanChangeMoneyOnCrew(Visit, data.install))
		{
			Logger.Warning("Not sure how we got here, but we can't afford a new outpost");
			return OnClickResult.END_CONVERSATION;
		}
		Player.finances.DoChangeMoneyOnCrew(Visit, data.install, MoneyReason.FrontCreated, Visit.building.Id);
		Player.outposts.SetOutpost(Visit.building);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.OutpostsStarted, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteCancelOutpost(ConvoButton _)
	{
		Player.outposts.RemoveOutpost(new OutpostID(Visit.building), PlayerOutposts.RemovalReason.PlayerFiredOwner, PlayerID.HumanPlayer, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult ExecuteOutpostSelectExpansion(ConvoButton _)
	{
		PlayerOutposts outposts = Model.visit.GetPlayer().outposts;
		OutpostID outpostId = new OutpostID(Model.visit.building.Id);
		if (!outposts.CanStartNewOutpostExpansion(outpostId))
		{
			return OnShowResult.CONTINUE;
		}
		(Node, OutpostSettings.ExpansionDef) tuple = outposts.PickBestExpansion(outpostId, Model.visit);
		return OnShowResult.SetButtonData(new ConvoDataOutpostExpansion(outpostId, tuple.Item2?.id ?? default(Label), tuple.Item1?.id ?? default(NodeID), Model.visit));
	}

	internal OnClickResult ExecuteOutpostZoomToExpansion(ConvoButton button)
	{
		PersonInfoUtil.TweenCameraToNode(button.GetData<ConvoDataOutpostExpansion>().targetNodeId);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteOutpostPayForExpansion(ConvoButton button)
	{
		ConvoDataOutpostExpansion data = button.GetData<ConvoDataOutpostExpansion>();
		PlayerInfo player = Model.visit.GetPlayer();
		player.finances.DoChangeMoneyOnCrew(Model.visit, data.monthlyCost, MoneyReason.FrontMaintenance);
		player.outposts.DoStartNewOutpostExpansion(data.outpostId, data.FindExpansion());
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreForceClosedDemand(ConvoButton _)
	{
		ConvoDataStoreForceClosed convoDataStoreForceClosed = new ConvoDataStoreForceClosed();
		PlayerID enemy = Visit.biz.components.biz.PickTradingAIAtWarWith(Visit.pid);
		if (enemy.IsValid)
		{
			convoDataStoreForceClosed.enemy = enemy;
		}
		return OnShowResult.SetButtonData(convoDataStoreForceClosed);
	}

	internal OnClickResult ExecuteForceClosedDemand(ConvoButton button)
	{
		ConvoDataStoreForceClosed data = button.GetData<ConvoDataStoreForceClosed>();
		data.enemy.FindPlayer();
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.PeepsThreatened, 1);
		Visit.GetPlayer().territory.ForceCloseBusiness(Visit.building, data.enemy, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreTicketBuildingChoice(ConvoButton _)
	{
		return OnShowResult.SetButtonData(new ConvoDataTicketBuilding(Player.territory.FindTakeoverData(Visit)));
	}

	internal OnClickResult ExecuteBuildingZoom(ConvoButton button)
	{
		HUDUtil.GoTo(button.GetData<ConvoDataTicketBuilding>().takeover.FindBuilding().data.board.worldpos, zoomIn: true, showFx: true);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteBuildingTakeover(ConvoButton button)
	{
		ConvoDataTicketBuilding data = button.GetData<ConvoDataTicketBuilding>();
		Player.territory.PerformTakeover(Visit.crew, data.takeover);
		return OnClickResult.CONTINUE;
	}

	internal OnPreshowResult StoreTicketResourceRevealChoice(ConvoButton _)
	{
		List<EntityID> source = BuildingUtil.FindUnscopedConstructionSellers(Visit.building);
		List<EntityID> source2 = BuildingUtil.FindUnscopedContainerSellers(Visit.building);
		WorldPos buildingLoc = Visit.building.components.board.GetNode().pos;
		List<EntityID> constructionRevs = source.OrderBy((EntityID x) => (x.FindEntity().components.board.GetNode().pos - buildingLoc).Magnitude).Take(NUMBER_OF_SOURCES_TO_REVEAL).ToList();
		source2 = source2.OrderBy((EntityID x) => (x.FindEntity().components.board.GetNode().pos - buildingLoc).Magnitude).Take(NUMBER_OF_SOURCES_TO_REVEAL).ToList();
		return OnPreshowResult.SetButtonData(new ConvoDataTicketResourceReveal(constructionRevs, source2));
	}

	internal OnClickResult ExecuteContainerReveal(ConvoButton button)
	{
		return ExecuteReveal(SpecialCheckHasLegalRevealSpecific.RevealType.Containers, button);
	}

	internal OnClickResult ExecuteConstructionReveal(ConvoButton button)
	{
		return ExecuteReveal(SpecialCheckHasLegalRevealSpecific.RevealType.Construction, button);
	}

	private OnClickResult ExecuteReveal(SpecialCheckHasLegalRevealSpecific.RevealType type, ConvoButton button)
	{
		ConvoDataTicketResourceReveal data = button.GetData<ConvoDataTicketResourceReveal>();
		PlayerInfo human = Game.ctx.players.Human;
		foreach (EntityID item in (type == SpecialCheckHasLegalRevealSpecific.RevealType.Containers) ? data.containers : data.constructions)
		{
			Entity entity = item.FindEntity();
			Node node = entity.components.board.GetNode();
			if (!node.known.Get(PlayerID.HumanPlayer))
			{
				human.meetings.MarkNodeAsKnown(node, expectedSeen: true, instant: false);
			}
			Game.ctx.players.Human.territory.ScopeOutBuildingWithFeedback(entity, Visit.crew.peepId);
			Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(BuildingUtil.FindOwnerForAnyBuilding(item).Id).IncrementConvoCount();
		}
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreGangTiedHouseProposal(ConvoButton _)
	{
		ConvoDataTiedHouseProposal convoDataTiedHouseProposal = new ConvoDataTiedHouseProposal();
		PlayerInfo playerInfo = Visit.npc.data.agent.pid.FindPlayer();
		SocialActionInfo? socialActionInfo = playerInfo?.ai?.business?.FindTiedHouseConvoMemory(Visit.pid);
		if (socialActionInfo.HasValue)
		{
			EntityID entityCtx = socialActionInfo.Value.entityCtx;
			(Price cost, int days) tiedHouseParameters = BuildingUtil.FindBizForOwner(entityCtx).components.biz.GetTiedHouseParameters(entityCtx, Visit.pid, playerInfo.PID);
			Price item = tiedHouseParameters.cost;
			int item2 = tiedHouseParameters.days;
			convoDataTiedHouseProposal.other = playerInfo.PID;
			convoDataTiedHouseProposal.owner = entityCtx;
			convoDataTiedHouseProposal.price = item;
			convoDataTiedHouseProposal.days = item2;
		}
		return OnShowResult.SetButtonData(convoDataTiedHouseProposal);
	}

	internal OnShowResult StoreOwnerTiedHouseProposal(ConvoButton _)
	{
		ConvoDataTiedHouseProposal convoDataTiedHouseProposal = new ConvoDataTiedHouseProposal();
		(Price cost, int days) tiedHouseParameters = Visit.biz.components.biz.GetTiedHouseParameters(Visit.npc.Id, Visit.pid, null);
		Price item = tiedHouseParameters.cost;
		int item2 = tiedHouseParameters.days;
		convoDataTiedHouseProposal.other = PlayerID.System;
		convoDataTiedHouseProposal.owner = Visit.npc.Id;
		convoDataTiedHouseProposal.price = item;
		convoDataTiedHouseProposal.days = item2;
		return OnShowResult.SetButtonData(convoDataTiedHouseProposal);
	}

	internal OnClickResult ExecutePayForTiedHouseProposal(ConvoButton button)
	{
		ConvoDataTiedHouseProposal data = button.GetData<ConvoDataTiedHouseProposal>();
		Entity biz = BuildingUtil.FindBizForOwner(data.owner);
		if (data.IsGangProposal)
		{
			Game.ctx.players.Human.territory.PayToTakeOverTiedHouse(Visit, biz, data.price, data.days);
		}
		else
		{
			Game.ctx.players.Human.territory.PayForNewTiedHouse(Visit, biz, data.price, data.days);
		}
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreDataTradeLocked(ConvoButton _)
	{
		ConvoDataTradeLocked convoDataTradeLocked = new ConvoDataTradeLocked();
		BizComponent.TradeRestrictions tradeRestrictions = Visit.biz.components.biz.FindTradeRestrictions(Player.PID);
		if (tradeRestrictions.IsTiedHouseLocked)
		{
			convoDataTradeLocked.Set(tradeRestrictions.tiedHouseLock, ConvoDataTradeLocked.Reason.TiedHouse);
		}
		else if (tradeRestrictions.IsTerritoryLocked)
		{
			convoDataTradeLocked.Set(tradeRestrictions.territoryLock, ConvoDataTradeLocked.Reason.Territory);
		}
		else if (tradeRestrictions.IsForcedClosed)
		{
			convoDataTradeLocked.Set(tradeRestrictions.forcedClosedBy, ConvoDataTradeLocked.Reason.ForcedClosed);
		}
		else
		{
			convoDataTradeLocked.Set(Player.PID, ConvoDataTradeLocked.Reason.None);
		}
		return OnShowResult.SetButtonData(convoDataTradeLocked);
	}

	internal OnClickResult ExecuteAddTiedHouseSocialHistory(ConvoButton button)
	{
		button.GetData<ConvoDataTradeLocked>().otherPlayer.FindPlayer().ai?.business?.AddTiedHouseConvoMemory(Visit.pid, Visit.crew.peepId, Visit.npc.Id);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreCollectionFromOutpost(ConvoButton _)
	{
		OutpostEntry outpostEntryUnsafe = Player.outposts.GetOutpostEntryUnsafe(Visit.building);
		Price collected = new Price(outpostEntryUnsafe?.money.collected ?? Fixnum.ZERO);
		Price expenses = new Price(outpostEntryUnsafe?.money.expenses ?? Fixnum.ZERO);
		return OnShowResult.SetButtonData(new ConvoDataTributeCollect(outpostEntryUnsafe.outpostId, collected, expenses));
	}

	internal OnClickResult ExecuteCollectionFromOutpost(ConvoButton button)
	{
		ConvoDataTributeCollect data = button.GetData<ConvoDataTributeCollect>();
		Player.outposts.DoCollectFromOutpost(Visit, data.outpost);
		return OnClickResult.CONTINUE;
	}

	internal OnShowResult StoreTributeInfo(ConvoButton _)
	{
		OutpostID outpost = Player.outposts.FindBestOutpostForTribute(Visit.GetBldgNode());
		if (outpost.IsNotValid)
		{
			Logger.Warning("Missing outpost for tribute convo! This shouldn't have gotten this far.");
		}
		Entity building = outpost.FindBuilding();
		Price cashamt = Player.outposts.FindMonthlyTribute(Visit);
		string name = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building)?.data.person?.FullName;
		return OnShowResult.SetButtonData(new ConvoDataTributeStart(outpost, cashamt, name));
	}

	internal OnClickResult ExecuteDemandProtectionVerbal(ConvoButton button)
	{
		ConvoDataTributeStart data = button.GetData<ConvoDataTributeStart>();
		Demand demand = Game.ctx.simman.demands.PerformRequestOwnerToComply(Model.visit, Demand.Type.Protection);
		data.demandState = demand.state;
		if (data.demandState == Demand.State.Compliant)
		{
			ExecuteTributeConfirm(button);
		}
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteDemandProtectionAlreadyCompliant(ConvoButton button)
	{
		Demand demand = Game.ctx.simman.demands.FindOrNull(Model.visit.pid, Demand.Target.MakeForBizOwner(Model.visit.npc));
		if (demand != null && demand.state == Demand.State.Compliant)
		{
			ExecuteTributeConfirm(button);
		}
		else
		{
			Logger.Warning($"Invalid demand state, expected compliant, got {demand}");
		}
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteDemandProtectionAttack(ConvoButton button)
	{
		ConvoDataTributeStart data = button.GetData<ConvoDataTributeStart>();
		Demand demand = Game.ctx.simman.demands.PerformForceOwnerToComply(Model.visit, Demand.Type.Protection);
		data.demandState = demand.state;
		if (data.demandState == Demand.State.Compliant)
		{
			ExecuteTributeConfirm(button);
		}
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.PeepsThreatened, 1);
		return OnClickResult.CONTINUE;
	}

	private OnClickResult ExecuteTributeConfirm(ConvoButton button)
	{
		ConvoDataTributeStart data = button.GetData<ConvoDataTributeStart>();
		Player.outposts.StartPayingTribute(Visit.building.Id, data.outpost, data.cashamt, Visit.crew.peepId);
		Visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.BusinessesExtorted, 1);
		return OnClickResult.CONTINUE;
	}

	internal OnClickResult ExecuteTributeCancel(ConvoButton _)
	{
		Player.outposts.StopPayingTribute(Visit.building.Id, Visit.crew.peepId);
		return OnClickResult.CONTINUE;
	}

	public OnShowResult StoreVehicleCosts(ConvoButton _)
	{
		VehicleModule vehicleModule = Visit.building.components.modules.FindVehicleModuleOrNull();
		ModQuery q = Visit.MakeCrewModQuery();
		Price item = vehicleModule.FindRepairCost(Visit, q, explain: false).price;
		Price item2 = vehicleModule.FindBuyBackPrice(Visit, q, explain: false).price;
		return OnShowResult.SetButtonData(new ConvoDataVehicleCosts(item, item2));
	}

	public OnClickResult ExecuteVehicleRepair(ConvoButton button)
	{
		ConvoDataVehicleCosts data = button.GetData<ConvoDataVehicleCosts>();
		Visit.building.components.modules.FindVehicleModuleOrNull().StartRepairVehicleDuringVisit(Visit, data.repairPrice);
		return OnClickResult.CONTINUE;
	}

	public OnClickResult ExecuteVehicleBuyFromPlayer(ConvoButton button)
	{
		ConvoDataVehicleCosts data = button.GetData<ConvoDataVehicleCosts>();
		Visit.building.components.modules.FindVehicleModuleOrNull().PerformVehicleBuyFromPlayer(Visit, data.buyBackPrice);
		return OnClickResult.END_CONVERSATION;
	}

	public OnShowResult StoreVehicleForSale(ConvoButton _)
	{
		VehicleModule.VehicleForSale? vehicleForSale = Visit.building.components.modules.FindVehicleModuleOrNull().FindVehicleForSaleOrNull(Visit);
		if (!vehicleForSale.HasValue)
		{
			return OnShowResult.SetButtonData(new ConvoDataVehicleSales(foundVehicle: false, canAfford: false, default(VehicleModule.VehicleForSale)));
		}
		bool canAfford = Visit.GetPlayer().finances.CanChangeMoneyOnCrew(Visit, vehicleForSale.Value.salePrice);
		return OnShowResult.SetButtonData(new ConvoDataVehicleSales(foundVehicle: true, canAfford, vehicleForSale.Value));
	}

	public OnClickResult ExecuteVehicleSellToPlayer(ConvoButton button)
	{
		ConvoDataVehicleSales data = button.GetData<ConvoDataVehicleSales>();
		Visit.building.components.modules.FindVehicleModuleOrNull()?.PerformVehicleSellToPlayer(Visit, data.info);
		return OnClickResult.CONTINUE;
	}
}
