using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class ResidenceComponent : BaseComponent, IUpgradableComponent
{
	public ResidenceConfig Config => _baseConfig as ResidenceConfig;

	public bool IsAssigned => _entity.data.residence.IsAssigned;

	public bool IsNotAssigned => _entity.data.residence.IsNotAssigned;

	public bool IsGamblingHouse => _entity.data.residence.assignment == ResidenceAssignment.GamblingHouse;

	public bool IsPoliticianResidence => _entity.data.residence.assignment == ResidenceAssignment.PoliticianResidence;

	public bool CanBecomeDebtorResidence => IsNotAssigned;

	public bool IsDebtorResidence => _entity.data.residence.assignment == ResidenceAssignment.DebtorResidence;

	public bool IsEventHostingSpace => _entity.data.residence.IsEventHostingSpace;

	public bool IsEventHostingReserved => _entity.data.residence.IsEventHostingReserved;

	public bool IsEventHostingActive => _entity.data.residence.IsEventHostingActive;

	public bool CanMarkAsReservedEventSpace
	{
		get
		{
			if (IsNotAssigned && !IsEventHostingSpace)
			{
				return !IsEventHostingActive;
			}
			return false;
		}
	}

	public bool CanUnmarkAsReservedEventSpace
	{
		get
		{
			if (IsAssigned && IsEventHostingSpace)
			{
				return !IsEventHostingActive;
			}
			return false;
		}
	}

	public bool IsBuildingPickInteractable
	{
		get
		{
			if (!IsEventHostingActive && !IsDebtorResidence)
			{
				return IsPoliticianResidence;
			}
			return true;
		}
	}

	public (int residents, int allApts) CountEthnicApartments(Label eth)
	{
		List<ApartmentData> apartments = _entity.data.residence.apartments;
		int count = apartments.Count;
		int num = 0;
		for (int i = 0; i < count; i++)
		{
			if (apartments[i].eth == eth)
			{
				num++;
			}
		}
		return (residents: num, allApts: count);
	}

	public bool HasEmptyApartments()
	{
		return Config.apartments > _entity.data.residence.apartments.Count;
	}

	public bool HasNoMoreApartments()
	{
		return Config.apartments <= _entity.data.residence.apartments.Count;
	}

	public int CountEmptyApartments()
	{
		return Config.apartments - _entity.data.residence.apartments.Count;
	}

	public void AddResidents(ApartmentData apt)
	{
		_entity.data.residence.apartments.Add(apt);
	}

	public object GenerateUpgradeData()
	{
		ResidenceData residence = _entity.data.residence;
		_entity.data.residence = null;
		return residence;
	}

	public void ConsumeUpgradeData(EntityID previousId, object data)
	{
		if (data is ResidenceData residenceData)
		{
			_entity.data.residence.apartments.AddRange(residenceData.apartments);
		}
	}

	public void OnAfterUpgrade(EntityID previousId)
	{
	}

	public void OnResidenceActivationChange(bool activated)
	{
		bool show = false;
		if (IsEventHostingActive)
		{
			ToggleConversation(activated, StartResEventConvo);
		}
		else if (IsGamblingHouse)
		{
			if (_entity.data.building.controlled.pid.IsHumanPlayer)
			{
				ToggleGamblingUI(activated);
			}
			else
			{
				ToggleCasinoConversation(_entity, activated);
			}
		}
		else if (IsDebtorResidence)
		{
			ToggleConversation(activated, StartGamblerConvo);
		}
		else if (IsPoliticianResidence)
		{
			ToggleConversation(activated, StartPoliticianConvo);
		}
		else
		{
			show = activated;
		}
		Game.ctx.hud?.bldginfo?.Toggle(_entity, show);
	}

	private void ToggleConversation(bool activated, Action<EntityID> convoFn)
	{
		if (activated)
		{
			EntitySelectionPopup.ShowCrewSelector(_entity.components.board.GetNodeID(), Loc.Get("ui.crewinfo.pickone"), convoFn);
		}
		else
		{
			Game.ctx.hud.HideGroup(BaseHUDDialog.GroupType.ConvoGroup);
		}
	}

	private void StartResEventConvo(EntityID eid)
	{
		Game.ctx.hud.convoDialog.Controller.StartResEventVisit(_entity, Game.ctx.players.Human.crew.GetCrewForPeep(eid));
	}

	private void StartGamblerConvo(EntityID eid)
	{
		Game.ctx.hud.convoDialog.Controller.StartDebtorVisit(_entity, Game.ctx.players.Human.crew.GetCrewForPeep(eid));
	}

	private void StartPoliticianConvo(EntityID eid)
	{
		Game.ctx.hud.convoDialog.Controller.StartCivicVisit(_entity, Game.ctx.players.Human.crew.GetCrewForPeep(eid));
	}

	public Entity GetNpcResident()
	{
		return _entity.data.residence.npcResident.FindEntity();
	}

	public void SetAssigned(ResidenceAssignment expected, ResidenceAssignment next)
	{
		_ = _entity.data.residence.assignment;
		_entity.data.residence.assignment = next;
		RefreshPickSuppression();
	}

	public void ClearAssigned(ResidenceAssignment expected)
	{
		SetAssigned(expected, ResidenceAssignment.None);
	}

	public void SetNpcResident(EntityID peepId, bool expectedEmpty)
	{
		_ = _entity.data.residence.npcResident;
		_entity.data.residence.npcResident = peepId;
		RefreshPickSuppression();
	}

	public void ClearNpcResident()
	{
		SetNpcResident(EntityID.INVALID, expectedEmpty: false);
	}

	public void MarkAsGamblingHouse()
	{
		SetAssigned(ResidenceAssignment.None, ResidenceAssignment.GamblingHouse);
	}

	public void UnmarkAsGamblingHouse()
	{
		SetAssigned(ResidenceAssignment.GamblingHouse, ResidenceAssignment.None);
	}

	public void MarkAsPoliticianResidence()
	{
		SetAssigned(ResidenceAssignment.None, ResidenceAssignment.PoliticianResidence);
	}

	public void UnmarkAsPoliticianResidence()
	{
		SetAssigned(ResidenceAssignment.PoliticianResidence, ResidenceAssignment.None);
	}

	public void MarkDebtorResidence(Entity peep)
	{
		SetAssigned(ResidenceAssignment.None, ResidenceAssignment.DebtorResidence);
		SetNpcResident(peep.Id, expectedEmpty: true);
	}

	public void UnmarkDebtorResidence(Entity peep)
	{
		SetAssigned(ResidenceAssignment.DebtorResidence, ResidenceAssignment.None);
		SetNpcResident(EntityID.INVALID, expectedEmpty: false);
	}

	public static void ToggleCasinoConversation(Entity building, bool activated)
	{
		if (activated)
		{
			StartCasinoConversation(building);
		}
		else
		{
			Game.ctx.hud.HideGroup(BaseHUDDialog.GroupType.ConvoGroup);
		}
	}

	public static void StartCasinoConversation(VisitState visit, bool fromGamblingDialog)
	{
		StartCasinoConversation(visit.crew, visit.building, fromGamblingDialog);
	}

	public static void StartCasinoConversation(Entity building)
	{
		bool isShiftDown = KeyUtil.IsShiftDown;
		CrewAssignment quickTarget = BuildingUtil.GetQuickTarget(building);
		building.components.residence.GetNpcResident();
		if (isShiftDown)
		{
			StartCasinoConversation(quickTarget, building, fromGamblingDialog: false);
			return;
		}
		EntitySelectionPopup.ShowCrewSelector(building.components.board.GetNodeID(), Loc.Get("ui.crewinfo.pickone.biz"), delegate(EntityID eid)
		{
			StartCasinoConversation(eid.FindEntity().components.agent.FindCrewAssignment(), building, fromGamblingDialog: false);
		}, delegate
		{
			BuildingUtil.DeselectOnNextFrame();
		});
	}

	public static void StartCasinoConversation(CrewAssignment crew, Entity building, bool fromGamblingDialog)
	{
		ConversationModel.Source source = (fromGamblingDialog ? ConversationModel.Source.ControlledGambling : ConversationModel.Source.None);
		Game.ctx.hud.convoDialog.Controller.StartCasinoVisit(building, crew, source);
	}

	public ResEventData GetResEventOrNull()
	{
		return _entity.data.residence.hostedResEvent;
	}

	public GamblingModule GetGamblingModule()
	{
		return _entity.components.modules.gambling;
	}

	public void MarkAsEventHostingSpace()
	{
		SetAssigned(ResidenceAssignment.None, ResidenceAssignment.EventSpaceReserved);
	}

	public void UnmarkAsEventHostingSpace()
	{
		SetAssigned(ResidenceAssignment.EventSpaceReserved, ResidenceAssignment.None);
	}

	public void SetResEventHost(Label eventId, Entity peep)
	{
		SetAssigned(ResidenceAssignment.EventSpaceReserved, ResidenceAssignment.EventSpaceActive);
		SetNpcResident(peep.Id, expectedEmpty: true);
		_entity.data.residence.hostedResEvent = new ResEventData(eventId);
	}

	public void ClearResEventHost()
	{
		SetAssigned(ResidenceAssignment.EventSpaceActive, ResidenceAssignment.EventSpaceReserved);
		SetNpcResident(EntityID.INVALID, expectedEmpty: false);
		_entity.data.residence.hostedResEvent = null;
	}

	public void SetAICasinoManager(Entity peep)
	{
		SetNpcResident(peep.Id, expectedEmpty: true);
	}

	public void ClearAICasinoManager()
	{
		SetNpcResident(EntityID.INVALID, expectedEmpty: false);
	}

	public void RefreshPickSuppression()
	{
		bool value = IsNotAssigned || IsEventHostingReserved;
		_entity.components.building.TogglePickSuppression(value);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.UIResAssignmentChanged, _entity.Id, PlayerID.HumanPlayer));
	}

	public string GetBuildingIcon()
	{
		string text = null;
		if (text == null && IsEventHostingActive)
		{
			text = GetResEventOrNull().GetConfig().GetIcon();
		}
		if (text == null && IsGamblingHouse)
		{
			text = GetGamblingModule().config.common.display.GetTextIcon();
		}
		if (text == null && IsDebtorResidence)
		{
			text = Loc.Get("debtor-house.icon");
		}
		if (text == null && IsPoliticianResidence)
		{
			text = Loc.Get("politician-house.icon");
		}
		return text ?? "";
	}

	public string GetBuildingOrNpcName()
	{
		string text = null;
		if (text == null && IsGamblingHouse)
		{
			text = _entity.components.modules.gambling.ModuleConfig?.Common?.display.GetName();
		}
		if (text == null && IsEventHostingActive)
		{
			text = GetResEventOrNull().GetConfig().GetName();
		}
		if (text == null && IsDebtorResidence)
		{
			Entity npcResident = GetNpcResident();
			text = Loc.Get("debtor-house.name", "name", npcResident.data.person.FullName);
		}
		if (text == null && IsPoliticianResidence)
		{
			Entity npcResident2 = GetNpcResident();
			text = Loc.Get("politician-house.name", "name", npcResident2.data.person.FullName);
		}
		if (text == null)
		{
			text = _entity.config.residence.GetName();
		}
		return text;
	}

	public Entity GetResidentialNpc()
	{
		Entity entity = null;
		if (entity == null && IsGamblingHouse)
		{
			entity = ModulesUtil.GetManagerOrNull(_entity).manager ?? GetNpcResident();
		}
		if (entity == null && (IsEventHostingActive || IsDebtorResidence || IsPoliticianResidence))
		{
			entity = GetNpcResident();
		}
		return entity;
	}

	private void ToggleGamblingUI(bool activated)
	{
		if (activated)
		{
			BuildingUtil.StartOwnedBuildingInteraction(_entity, ExecuteOwnedGamblingHouseInteraction);
		}
		else
		{
			Game.ctx.hud.HideGroup(BaseHUDDialog.GroupType.ConvoGroup);
		}
	}

	private void ExecuteOwnedGamblingHouseInteraction(EntityID chosenPeep, Entity building)
	{
		CrewAssignment crewForPeep = Game.ctx.players.Human.crew.GetCrewForPeep(chosenPeep);
		BuildingAndBusinessData bbdata = BuildingUtil.FindDataForBuilding(building);
		VisitState visit = new VisitState(crewForPeep, bbdata, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		Game.ctx.hud.ownedGambling.Show(visit);
	}

	[Conditional("UNITY_EDITOR")]
	private void PrintDebugInfo()
	{
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		stringBuilder.Append($"APTS {_entity.data.residence.apartments.Count} total: ");
		foreach (ApartmentData apartment in _entity.data.residence.apartments)
		{
			stringBuilder.Append($"{apartment.eth}: {apartment.count}. ");
		}
	}

	internal IEnumerable<string> ProduceResidentNames(int max)
	{
		IRandom rng = _entity.components.ident.GetIdentityRNGUnchanging();
		List<ApartmentData> list = _entity.data.residence.apartments.Take(max).ToList();
		if (list.Count == 0)
		{
			yield break;
		}
		EthnicitySettings settings = Game.serv.globals.settings.ethnicities;
		rng.Shuffle(list);
		foreach (ApartmentData item in list)
		{
			yield return settings.FindEthnicityDef(item.eth).loc.GetRandomLastName(rng, Gender.M);
		}
	}

	internal (bool success, string name, string desc) GetResMouseoverNameAndDesc()
	{
		if (IsEventHostingActive)
		{
			ResEventData resEventOrNull = GetResEventOrNull();
			if (resEventOrNull != null)
			{
				ResidentialEventConfig config = resEventOrNull.GetConfig();
				Entity npcResident = GetNpcResident();
				return (success: true, name: config.GetName(), desc: config.GetDesc(npcResident));
			}
		}
		if (IsGamblingHouse)
		{
			GamblingModule gambling = _entity.components.modules.gambling;
			if (gambling != null)
			{
				ModuleCommon.Display display = gambling.config.common.display;
				return (success: true, name: display.GetName(), desc: display.GetDesc());
			}
		}
		if (IsDebtorResidence)
		{
			string buildingOrNpcName = GetBuildingOrNpcName();
			Entity npcResident2 = GetNpcResident();
			string debtLevelString = GetDebtLevelString(npcResident2);
			if (buildingOrNpcName != null)
			{
				return (success: false, name: buildingOrNpcName, desc: debtLevelString);
			}
		}
		if (IsPoliticianResidence)
		{
			string buildingOrNpcName2 = GetBuildingOrNpcName();
			GetNpcResident();
			if (buildingOrNpcName2 != null)
			{
				return (success: false, name: buildingOrNpcName2, desc: "");
			}
		}
		return (success: false, name: GetBuildingOrNpcName(), desc: "");
	}

	internal string GetDebtLevelString(Entity gambler)
	{
		PlayerGambling gambling = Game.ctx.players.Human.gambling;
		GamblerState gamblerState = gambling.FindGamblerState(gambler);
		Entity entity = gamblerState.FindGamblingHouse();
		ModQuery query = entity.components.modules.gambling.MakeManagerBasedModQuery(Game.ctx.players.Human, entity);
		gambling.FindCurrentDebtLevel(gamblerState).cash.Evaluate(query);
		return "";
	}
}
