using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Services.Store;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;

namespace Game.Session.Entities;

public sealed class CivicComponent : BaseComponent
{
	public bool IsNotAssigned => _entity.config.civic.IsNotAssigned;

	public bool IsAssigned => _entity.config.civic.IsAssigned;

	public bool IsBuildingPickInteractable
	{
		get
		{
			if (IsAssigned)
			{
				return HasShadowGovernment;
			}
			return false;
		}
	}

	public bool HasShadowGovernment => Game.serv.store.IsPackInstalled(PackID.ShadowGovernment);

	internal void OnActivationChange(bool activated)
	{
		if (activated)
		{
			OnCivicActivated();
		}
		else
		{
			OnCivicDeactivated();
		}
	}

	public void OnCivicActivated()
	{
		bool num = _entity.components.building.IsInteractableByPlayer(PlayerID.HumanPlayer);
		Game.ctx.simman.politics.GetPoliticianData(_entity.data.civic.npc);
		if (num)
		{
			Ward wardForPolitician = Game.ctx.simman.politics.GetWardForPolitician(_entity.data.civic.npc);
			if (wardForPolitician == null || !wardForPolitician.ElectionOngoing)
			{
				StartConversation(fromPolDialog: false);
			}
			else
			{
				ShowPoliticsDialog();
			}
			if (wardForPolitician == null)
			{
				return;
			}
			{
				foreach (EntityID localPolitician in wardForPolitician.localPoliticians)
				{
					_ = localPolitician;
				}
				return;
			}
		}
		Game.ctx.hud.bldginfo.Toggle(_entity, show: true);
	}

	public void OnCivicDeactivated()
	{
		Game.ctx.hud.HideGroup(BaseHUDDialog.GroupType.ConvoGroup);
	}

	public void SetNpc(EntityID peepId, bool expectedEmpty)
	{
		_ = _entity.data.civic.npc;
		_entity.data.civic.npc = peepId;
		RefreshPickSuppression();
	}

	internal (bool success, string name, string desc) GetCivicMouseoverNameAndDesc()
	{
		if (_entity.config.civic.IsWard && HasShadowGovernment)
		{
			return (success: true, name: Loc.Get(_entity.config.civic.locname), desc: Loc.GetWithPolUnit("ui.pick.civic.political", "politician", _entity.data.civic.npc.FindEntity().data.person.FullName));
		}
		return (success: true, name: Loc.Get(_entity.config.civic.locname), desc: Loc.Get(_entity.config.civic.locdesc));
	}

	public void ClearNpc()
	{
		SetNpc(EntityID.INVALID, expectedEmpty: false);
	}

	public void RefreshPickSuppression()
	{
		bool isNotAssigned = IsNotAssigned;
		_entity.components.building.TogglePickSuppression(isNotAssigned);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.UIResAssignmentChanged, _entity.Id, PlayerID.HumanPlayer));
	}

	public void ShowPoliticsDialog()
	{
		bool isShiftDown = KeyUtil.IsShiftDown;
		BuildingAndBusinessData buildingData = BuildingUtil.MakeDataForBuildingAndOwner(_entity, _entity.data.civic.npc.FindEntity());
		if (isShiftDown)
		{
			CrewAssignment quickTarget = BuildingUtil.GetQuickTarget(_entity);
			Game.ctx.hud.politicsDialog.Show(new VisitState(quickTarget, buildingData, Game.ctx.clock.Now, PlayerID.HumanPlayer));
			return;
		}
		EntitySelectionPopup.ShowCrewSelector(_entity.components.board.GetNodeID(), Loc.Get("ui.crewinfo.pickone.civic"), delegate(EntityID eid)
		{
			Game.ctx.hud.politicsDialog.Show(new VisitState(Game.ctx.players.Human.crew.GetCrewForPeep(eid), buildingData, Game.ctx.clock.Now, PlayerID.HumanPlayer));
		}, delegate
		{
			BuildingUtil.DeselectOnNextFrame();
		});
	}

	public void StartConversation(bool fromPolDialog)
	{
		if (KeyUtil.IsShiftDown)
		{
			StartConversation(_entity, BuildingUtil.GetQuickTarget(_entity), fromPolDialog);
			return;
		}
		EntitySelectionPopup.ShowCrewSelector(_entity.components.board.GetNodeID(), Loc.Get("ui.crewinfo.pickone.civic"), delegate(EntityID eid)
		{
			StartConversation(_entity, Game.ctx.players.Human.crew.GetCrewForPeep(eid), fromPolDialog);
		}, delegate
		{
			BuildingUtil.DeselectOnNextFrame();
		});
	}

	public Ward GetWard()
	{
		return Game.ctx.simman.politics.GetWardForID(_entity.components.board.GetNode().precinctId);
	}

	public static void StartConversation(Entity building, CrewAssignment crew, bool fromPolDialog)
	{
		Game.ctx.hud.convoDialog.Controller.StartCivicVisit(building, crew, fromPolDialog ? ConversationModel.Source.Politics : ConversationModel.Source.None);
	}
}
