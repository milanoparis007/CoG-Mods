using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.KB;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConversationController : HUDController<ConversationModel, ConversationDialog, ConversationController>
{
	private UISettings _settings;

	private ConvoCallbacks _callbacks;

	public override void Initialize(ConversationDialog view)
	{
		base.Initialize(view);
		_settings = Game.serv.globals.ui;
		_callbacks = new ConvoCallbacks();
		_callbacks.Initialize(this);
	}

	public override void Release()
	{
		if (base.View.IsShowing)
		{
			base.View.Hide();
		}
		_callbacks.Release();
		_callbacks = null;
		_settings = null;
		base.Release();
	}

	internal void OnDialogReset()
	{
		base.Model.Reset();
	}

	internal void OnDialogBeforeHide()
	{
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.ConversationEnded, base.Model?.visit?.npc.Id ?? EntityID.INVALID, PlayerID.HumanPlayer));
	}

	public void StartBizVisit(Entity biz, CrewAssignment crew, ConversationModel.Source source = ConversationModel.Source.None)
	{
		BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBiz(biz);
		base.Model.visit = new VisitState(crew, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		base.Model.source = source;
		base.View.OnControllerRefreshOrShow(this);
		Game.ctx.players.Human.social.OnConvoStartedWith(bbdata.owner);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.ConversationStarted, bbdata.owner.Id, PlayerID.HumanPlayer, base.Model.visit));
		Game.ctx.sfx.PlayConvoStart(base.Model.visit, null);
		if (!TryShiftClick())
		{
			StartTopLevelConversationTree(firstVisit: true);
		}
	}

	public void StartCasinoVisit(Entity house, CrewAssignment crew, ConversationModel.Source source = ConversationModel.Source.None)
	{
		PlayerInfo otherPlayer = house.data.building.controlled.pid.FindPlayer();
		BuildingAndBusinessData bbdata = BuildingUtil.MakeDataForBuildingAndOwner(house, BuildingUtil.FindOwnerOrManagerForAnyBuilding(house));
		VisitState visit = new VisitState(crew, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		Label toplevel = (house.components.building.IsControlledBy(PlayerID.HumanPlayer) ? ConversationConstants.TOPLEVEL_GAMBLING_MANAGER : ConversationConstants.TOPLEVEL_GANG_BUILDING);
		SpecialStartConvoHelper(toplevel, visit, otherPlayer, source, requireCrew: true);
	}

	public void StartDebtorVisit(Entity building, CrewAssignment crew)
	{
		BuildingAndBusinessData bbdata = BuildingUtil.MakeDataForBuildingAndOwner(building, building.components.residence.GetNpcResident());
		VisitState visitState = new VisitState(crew, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		Entity npcResident = building.components.residence.GetNpcResident();
		GamblerState gamblerState = Game.ctx.players.Human.gambling.FindGamblerState(npcResident);
		Label toplevel = DetermineDebtorTopLevel(visitState);
		visitState.npc = SwapDebtorWithFamilyIfFail(visitState) ?? visitState.npc;
		ConvoDataGamblingDebtor startDat = new ConvoDataGamblingDebtor(new List<RepaymentChoiceAndSuccess>(), gamblerState.repaymentInProgress.IsSet ? gamblerState.repaymentInProgress : Label.NULL, 0, 1, npcResident.Id, gamblerState.rollAmount);
		SpecialStartConvoHelper(toplevel, visitState, null, ConversationModel.Source.None, requireCrew: true, startDat);
	}

	public void StartCivicVisit(Entity building, CrewAssignment crew, ConversationModel.Source source = ConversationModel.Source.None)
	{
		BuildingAndBusinessData bbdata = BuildingUtil.MakeDataForBuildingAndOwner(building, building.data.civic?.npc.FindEntity() ?? building.data.residence.npcResident.FindEntity());
		VisitState visitState = new VisitState(crew, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		ConvoDataPolitician startDat = new ConvoDataPolitician((building.data.civic?.npc.FindEntity() ?? building.data.residence.npcResident.FindEntity()).Id, Label.NULL, Game.ctx.simman.politics.GetWardForBuilding(visitState.building.Id).id);
		Label toplevel = ConversationConstants.TOPLEVEL_POLITICIAN;
		Label? label = PickCivicInitiativeToplevel(visitState);
		if (label.HasValue)
		{
			toplevel = label.Value;
		}
		SpecialStartConvoHelper(toplevel, visitState, null, source, requireCrew: true, startDat);
	}

	private Label? PickCivicInitiativeToplevel(VisitState visit)
	{
		EntityID id = ((visit == null) ? base.Model.visit : visit).building.Id;
		PlayerKB kb = Game.ctx.players.Human.kb;
		if (!kb.QReqShouldStart(id))
		{
			if (!kb.QReqShouldFinish(id))
			{
				return null;
			}
			return ConversationConstants.TOPLEVEL_QREQ_COLLECT;
		}
		return ConversationConstants.TOPLEVEL_QREQ_START;
	}

	public Label DetermineDebtorTopLevel(VisitState visit)
	{
		GamblerState gamblerState = Game.ctx.players.Human.gambling.FindGamblerState(visit.npc);
		if (gamblerState.repaymentInProgress.IsNotSet)
		{
			return ConversationConstants.TOPLEVEL_DEBTOR;
		}
		if (!gamblerState.WasRepaymentDayReached())
		{
			return ConversationConstants.TOPLEVEL_REPAYMENT_PENDING;
		}
		if (gamblerState.repaymentInProgress == GamblingConstants.REPAYMENT_CASH)
		{
			return ConversationConstants.TOPLEVEL_REPAYMENT_CASH;
		}
		if (!gamblerState.success)
		{
			return ConversationConstants.TOPLEVEL_REPAYMENT_FAIL;
		}
		return ConversationConstants.TOPLEVEL_REPAYMENT_READY;
	}

	public Entity SwapDebtorWithFamilyIfFail(VisitState visit)
	{
		PlayerInfo human = Game.ctx.players.Human;
		Entity npc = visit.npc;
		GamblerState gamblerState = human.gambling.FindGamblerState(npc);
		Entity entity = null;
		if (gamblerState.WasRepaymentDayReached() && !gamblerState.success)
		{
			entity = FindFamilyForGambler(visit.npc);
			human.social.MeetBuildingOwner(entity.Id, oldfriends: false, human.social.PlayerPeepId);
		}
		return entity;
	}

	public Entity FindFamilyForGambler(Entity gambler)
	{
		List<Relationship> list = Game.ctx.simman.rels.GetListOrNull(gambler.Id).data.Where((Relationship x) => IsValidCover(x.to.FindEntity())).ToList();
		return gambler.data.ident.rng.PickElement(list).to.FindEntity();
	}

	private bool IsValidCover(Entity fam)
	{
		PersonData person = fam.data.person;
		if (person.IsAlive && person.GetAge(Game.ctx.clock.Now).YearsFloat >= 20f && person.business.IsNotValid && person.resassigned.IsNotValid)
		{
			return fam.data.agent.pid.id == 0;
		}
		return false;
	}

	private bool TryShiftClick()
	{
		if (KeyUtil.IsShiftDown && base.Model.IsBusinessVisitOK)
		{
			List<ConvoButtonDef> list = new List<ConvoButtonDef>();
			List<ConvoButtonDef> buttons = _settings.convos.FindOrNull(ConversationConstants.TOPLEVEL_CONVO).buttons;
			if (buttons != null)
			{
				list.AddRange(buttons);
			}
			list = list.Where((ConvoButtonDef x) => x.quickType == QuickInfoType.BuySell).ToList();
			foreach (ConvoButtonDef item in list)
			{
				ConvoButton convoButton = new ConvoButton(0, item, null);
				if (convoButton.IsVisible(base.Model.visit) && convoButton.IsEnabled(base.Model.visit))
				{
					base.Model.SetForced(null);
					base.Model.ResetStateCounter();
					OnButtonShow(convoButton);
					SetState(BuildConvoState(item.next.Evaluate(base.Model.visit, convoButton.state), null, convoButton.state.data));
					return true;
				}
			}
		}
		return false;
	}

	public void StartResEventVisit(Entity building, CrewAssignment crew)
	{
		Label tOPLEVEL_RESEVENT = ConversationConstants.TOPLEVEL_RESEVENT;
		building.components.residence.GetResEventOrNull();
		BuildingAndBusinessData bbdata = BuildingUtil.MakeDataForBuildingAndOwner(building, building.components.residence.GetNpcResident());
		VisitState visit = new VisitState(crew, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		SpecialStartConvoHelper(tOPLEVEL_RESEVENT, visit, null, ConversationModel.Source.None, requireCrew: true);
	}

	public void StartCrewVisit(Entity npc, CrewAssignment crew)
	{
		PlayerInfo playerInfo = npc.data.agent.pid.FindPlayer();
		Label toplevel;
		if (playerInfo.IsJustCop)
		{
			toplevel = ConversationConstants.TOPLEVEL_COP;
		}
		else if (playerInfo.IsJustFed)
		{
			toplevel = ConversationConstants.TOPLEVEL_FED;
		}
		else if (playerInfo.IsJustGang)
		{
			toplevel = ConversationConstants.TOPLEVEL_GANGS;
		}
		else if (playerInfo.IsJustGoon)
		{
			toplevel = ConversationConstants.TOPLEVEL_GOONS;
		}
		else
		{
			if (!playerInfo.IsHuman)
			{
				return;
			}
			toplevel = ConversationConstants.TOPLEVEL_CREW;
		}
		VisitState visit = new VisitState(crew, npc, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		SpecialStartConvoHelper(toplevel, visit, playerInfo, ConversationModel.Source.Peep, requireCrew: false);
	}

	public void SpecialStartConvoHelper(Label toplevel, VisitState visit, PlayerInfo otherPlayer, ConversationModel.Source source, bool requireCrew, ConvoData startDat = null)
	{
		base.Model.SetForced(null);
		base.Model.ResetStateCounter();
		base.Model.source = source;
		base.Model.visit = visit;
		base.View.OnControllerRefreshOrShow(this);
		Game.ctx.players.Human.social.OnConvoStartedWith(visit.npc);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.ConversationStarted, visit.npc.Id, PlayerID.HumanPlayer, visit));
		Game.ctx.sfx.PlayConvoStart(visit, otherPlayer);
		if (requireCrew && !base.Model.IsBusinessVisitOK)
		{
			SetState(null);
			return;
		}
		visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.Convos, 1);
		SetState(BuildConvoState(toplevel, null, startDat));
	}

	private void StartTopLevelConversationTree(bool firstVisit)
	{
		if (!base.Model.IsBusinessVisitOK)
		{
			SetState(null);
			return;
		}
		Label key = PickBusinessTopLevelCombo(base.Model.visit);
		if (firstVisit)
		{
			base.Model.SetForced(null);
			base.Model.ResetStateCounter();
			Label? label = PickInitiativeTopLevelCombo();
			if (label.HasValue)
			{
				key = label.Value;
			}
		}
		base.Model.visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.Convos, 1);
		SetState(BuildConvoState(key, null, null));
	}

	private static Label PickBusinessTopLevelCombo(VisitState visit)
	{
		BuildingComponent buildingComponent = visit?.building?.components.building;
		if (buildingComponent != null && buildingComponent.IsControlledByAnyPlayer())
		{
			PlayerID controllingPlayer = buildingComponent.GetControllingPlayer();
			if (!buildingComponent.IsSafehouse && controllingPlayer != visit.pid)
			{
				return ConversationConstants.TOPLEVEL_GANG_BUILDING;
			}
		}
		return ConversationConstants.TOPLEVEL_CONVO;
	}

	private Label? PickInitiativeTopLevelCombo()
	{
		EntityID id = base.Model.visit.building.Id;
		PlayerKB kb = Game.ctx.players.Human.kb;
		if (!kb.HealShouldPrompt(base.Model.visit))
		{
			if (!kb.QReqShouldStart(id))
			{
				if (!kb.QReqShouldFinish(id))
				{
					return null;
				}
				return ConversationConstants.TOPLEVEL_QREQ_COLLECT;
			}
			return ConversationConstants.TOPLEVEL_QREQ_START;
		}
		return ConversationConstants.TOPLEVEL_HEAL;
	}

	private ConvoState BuildConvoState(Label key, ConvoState parent, ConvoData data)
	{
		ConvoStateDef convoStateDef = _settings.convos.FindOrNull(key);
		if (convoStateDef == null)
		{
			return null;
		}
		return new ConvoState(convoStateDef, parent, data);
	}

	private void SetState(ConvoState state)
	{
		base.Model.state = state;
		base.Model.IncrementStateCounter();
		if (state?.def == null)
		{
			base.View.HideSubview();
		}
		else if (state.def.customView == ConvoStateDef.CustomViewType.None)
		{
			base.View.ShowConvoState(state);
		}
		else
		{
			Logger.Warning($"Unknown convo state {state.def} with data {state.data}");
		}
	}

	internal void JumpToConvoState(Label key, ConvoData data)
	{
		SetState(BuildConvoState(key, base.Model.state, data));
	}

	internal void OnButtonShow(ConvoButton button)
	{
		OnShowResult onShowResult = _callbacks.ProcessOnShow(button);
		if (onShowResult.newData != null)
		{
			button.state.data = onShowResult.newData;
		}
	}

	internal void OnButtonPreshow(ConvoButton button)
	{
		OnPreshowResult onPreshowResult = _callbacks.ProcessOnPreshow(button);
		if (onPreshowResult.newData != null)
		{
			button.state.data = onPreshowResult.newData;
		}
	}

	public void OnConvoButtonPress(ConvoState current, ConvoButton selected, string buttonText)
	{
		base.View.AddPlayerBlurb(buttonText);
		base.Model.SetForced(selected.ProduceForcedReaction(base.Model));
		OnClickResult onClickResult = _callbacks.ProcessOnClick(selected);
		selected.PerformSelectionSideEffects(base.Model);
		if (onClickResult.endConversation)
		{
			EndConversation();
		}
		else
		{
			if (onClickResult.pauseConversation)
			{
				return;
			}
			Label label = TryFindNextState(selected);
			if (label.IsNotSet)
			{
				StartTopLevelConversationTree(firstVisit: false);
				return;
			}
			ConvoStateDef convoStateDef = Game.serv.globals.ui.convos.FindOrNull(label);
			if (convoStateDef == null)
			{
				Label label2 = label;
				Logger.Warning("Unknown next state ID: " + label2.ToString());
				StartTopLevelConversationTree(firstVisit: false);
			}
			else
			{
				ConvoData data = selected.state.data?.CloneData();
				ConvoState state = new ConvoState(convoStateDef, current, data);
				SetState(state);
			}
		}
	}

	private Label TryFindNextState(ConvoButton selected)
	{
		Label? label = selected.state.def.next?.Evaluate(base.Model.visit, selected.state);
		if (!label.HasValue)
		{
			if (Game.ctx.simman.politics.GetPoliticianData(base.Model.visit.npc.Id) != null)
			{
				return ConversationConstants.TOPLEVEL_POLITICIAN;
			}
			return Label.NULL;
		}
		return label.Value;
	}

	internal void EndConversation()
	{
		if (base.Model.source == ConversationModel.Source.ControlledBusiness)
		{
			Game.ctx.hud.ownedBiz.Show(base.Model.visit);
		}
		else if (base.Model.source == ConversationModel.Source.ControlledGambling)
		{
			Game.ctx.hud.ownedGambling.Show(base.Model.visit);
		}
		else if (base.Model.source == ConversationModel.Source.Politics)
		{
			Game.ctx.hud.politicsDialog.Show(base.Model.visit);
		}
		else if (base.Model.source == ConversationModel.Source.Peep)
		{
			Game.ctx.selection.ClearActive();
			base.View.Hide();
		}
		else if (base.Model.source == ConversationModel.Source.CrewInspectPeep)
		{
			if (!Game.serv.ui.ContainsPopup<SchemePopup>())
			{
				if (Game.serv.ui.ContainsPopup<CrewPeepInspectPopup>() && !(Game.serv.ui.TopPopupUnsafe is CrewPeepInspectPopup))
				{
					Game.serv.ui.RemovePopup<CrewPeepInspectPopup>();
				}
				Game.serv.ui.AddPopup(new CrewPeepInspectPopup(base.Model.visit.crew.peepId));
			}
			base.View.Hide();
		}
		else if (!base.Model.IsCrewConvo)
		{
			Game.ctx.selection.ClearActive();
		}
		else
		{
			base.View.Hide();
		}
	}

	internal void OnBackButtonPress()
	{
		ConvoState state = base.Model.state;
		if (state == null)
		{
			StartTopLevelConversationTree(firstVisit: false);
		}
		else
		{
			SetState(state.parent);
		}
	}
}
