using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Overlays;
using Game.Session.Player;
using Game.Session.Player.Commands;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class PickManager : IAnimatedSubManager<HUDManager>, ISubManager<HUDManager>
{
	public class PickMouseover : BaseMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.PickMouseover;

		public override void RefreshContents()
		{
			string text = context.GetComponentInParent<PickContext>().pick.MakeMouseoverMessage();
			go.SetText("Text", text);
		}
	}

	public static readonly PickType[] ALL_PICK_TYPES = Enum.GetValues(typeof(PickType)) as PickType[];

	private GameObject _parent;

	private GameObject _templates;

	private Rect _containerSize;

	private List<PickContainer> _containers;

	private Dictionary<PickTarget, PickType> _moving;

	private bool _hideDueToCamera;

	private bool _hideDueToCheats;

	private List<PickHideRequest> _hideRequests;

	private float _lastCameraPitch;

	private float _lastCameraZoomPercent;

	private bool _isAltHeld;

	private bool _picksGotReordered;

	private bool _isPreorder;

	private static Rect MARGINS = new Rect(-0.1f, -0.1f, 1.2f, 1.2f);

	public float CameraZoomPercent => _lastCameraZoomPercent;

	public bool OverrideDontHidePicksOnZoom => _isAltHeld;

	public bool IsPreorder => _isPreorder;

	public void Initialize(HUDManager manager)
	{
		_parent = Game.serv.ui.GetUI(UIElements.Picks);
		_parent.SetActive(value: true);
		_templates = _parent.GetChild("Templates");
		_templates.SetActive(value: false);
		GameObject container = RefreshContainerSize();
		_containers = ListGenerators.ListOfNewInstances<PickContainer>(ALL_PICK_TYPES.Length);
		_containers[0].Initialize(PickType.None, this, container, () => (BasePick)null, null);
		_containers[1].Initialize(PickType.SummaryPick, this, container, () => new SummaryPick(), _templates.GetChild("Summary Pick"));
		_containers[2].Initialize(PickType.CornerPick, this, container, () => new CornerPick(), _templates.GetChild("Corner Pick"));
		_containers[3].Initialize(PickType.BuildingPick, this, container, () => new BuildingPick(), _templates.GetChild("Building Pick"));
		_containers[4].Initialize(PickType.CrewPick, this, container, () => new CrewPick(), _templates.GetChild("Crew Pick"));
		_containers[5].Initialize(PickType.OrderArrowPick, this, container, () => new OrderArrowPick(), _templates.GetChild("Resource Arrow Pick"));
		_containers[6].Initialize(PickType.RelArrowPick, this, container, () => new RelationshipPick(), _templates.GetChild("Relationship Pick"));
		_containers[7].Initialize(PickType.RelSourcePick, this, container, () => new RelationshipSourcePick(), _templates.GetChild("Relationship Source Pick"));
		_containers[8].Initialize(PickType.ResourcePick, this, container, () => new ResourcePick(), _templates.GetChild("Resource Pick"));
		_containers[9].Initialize(PickType.SchemePick, this, container, () => new SchemePick(), _templates.GetChild("Scheme Pick"));
		_moving = new Dictionary<PickTarget, PickType>(new PickTargetEqualityComparer());
		_hideRequests = new List<PickHideRequest>();
		_lastCameraPitch = (_lastCameraZoomPercent = 0f);
		_isAltHeld = false;
		_isPreorder = Game.serv.store.IsPreorderInstalled();
		Game.serv.mouseovers.Register(MouseoverType.BuildingPickMouseover, new PickMouseover());
		Game.serv.camera.OnCameraMoved.Add(OnCameraMoved);
		Game.serv.camera.OnScreenChanged.Add(OnResChanged);
		Game.ctx.events.AddListener(SessionEventType.CrewHealthChanged, OnCrewHealthChanged);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberKilled, OnCrewMemberKilled);
		Game.ctx.events.AddListener(SessionEventType.CrewVanquished, OnCrewVanquished);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleCreated, OnCrewVehicleChanged);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleHealthChanged, OnCrewHealthChanged);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleReassigned, OnCrewVehicleChanged);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleRemoved, OnCrewVehicleRemoved);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHumanTurnStarted);
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		Game.ctx.events.AddListener(SessionEventType.PlayerAggroChanged, OnVizOrAggroChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnBuildingChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerBuildingClearControlImmediate, OnBuildingNoLongerInteractive);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandExecutedOneTurn, OnPlayerCommandExec);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandFinished, OnPlayerCommandExec);
		Game.ctx.events.AddListener(SessionEventType.PlayerExploredNode, OnNodeExplored);
		Game.ctx.events.AddListener(SessionEventType.PlayerKBChanged, OnPlayerKBChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerKBChangedOnBuilding, OnPlayerKBChangedOnBuilding);
		Game.ctx.events.AddListener(SessionEventType.PlayerResourcesChanged, OnResourcesChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerScopedOutBuilding, OnPlayerScopeOut);
		Game.ctx.events.AddListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerUsedSocialTicket, OnTicketUsed);
		Game.ctx.events.AddListener(SessionEventType.PlayerVizChanged, OnVizOrAggroChanged);
		Game.ctx.events.AddListener(SessionEventType.SafehouseRaided, OnSafehouseRaided);
		Game.ctx.events.AddListener(SessionEventType.SafehouseRemoved, OnBuildingNoLongerInteractive);
		Game.ctx.events.AddListener(SessionEventType.CrewAutomationChanged, OnAutoPauseOrResume);
		Game.ctx.events.AddListener(SessionEventType.BuildingConstructionStateChanged, OnBuildingChanged);
		Game.ctx.events.AddListener(SessionEventType.BuildingHealthChanged, OnBuildingChangedIfVisible);
		Game.ctx.events.AddListener(SessionEventType.BuildingHealthZero, OnBuildingNoLongerInteractive);
		Game.ctx.events.AddListener(SessionEventType.BuildingHighlight, OnBuildingHighlighted);
		Game.ctx.events.AddListener(SessionEventType.BuildingModuleResultChanged, OnBuildingChanged);
		Game.ctx.events.AddListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.AddListener(SessionEventType.UIOrderArrowsChanged, OnOrderArrowsChanged);
		Game.ctx.events.AddListener(SessionEventType.UIOverlayResourcesChanged, OnUIResourcesChanged);
		Game.ctx.events.AddListener(SessionEventType.UIRelationshipArrowsChanged, OnRelationshipArrowsChanged);
		Game.ctx.events.AddListener(SessionEventType.UIRelationshipHighlighted, OnRelationshipArrowHighlighted);
		Game.ctx.events.AddListener(SessionEventType.UIResAssignmentChanged, OnResidentialAssignmentChanged);
		Game.ctx.events.AddListener(SessionEventType.UIDebtorChange, OnDebtorChanged);
		Game.ctx.events.AddListener(SessionEventType.SchemeUpdated, OnSchemeChanged);
		Game.ctx.scriptevents.AddListener(GameScriptEventType.ScriptAfterStarted, OnScriptStarted);
		Game.ctx.scriptevents.AddListener(GameScriptEventType.ScriptAfterStopped, OnScriptStopped);
		Game.serv.events.AddListener(ServiceEventType.SessionContextStateChange, OnPotentiallyLoadedGame);
	}

	public void Release()
	{
		Game.serv.events.RemoveListener(ServiceEventType.SessionContextStateChange, OnPotentiallyLoadedGame);
		Game.ctx.scriptevents.RemoveListener(GameScriptEventType.ScriptAfterStarted, OnScriptStarted);
		Game.ctx.scriptevents.RemoveListener(GameScriptEventType.ScriptAfterStopped, OnScriptStopped);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingConstructionStateChanged, OnBuildingChanged);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingHealthChanged, OnBuildingChangedIfVisible);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingHealthZero, OnBuildingNoLongerInteractive);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingHighlight, OnBuildingHighlighted);
		Game.ctx.events.RemoveListener(SessionEventType.BuildingModuleResultChanged, OnBuildingChanged);
		Game.ctx.events.RemoveListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.RemoveListener(SessionEventType.UIOrderArrowsChanged, OnOrderArrowsChanged);
		Game.ctx.events.RemoveListener(SessionEventType.UIOverlayResourcesChanged, OnUIResourcesChanged);
		Game.ctx.events.RemoveListener(SessionEventType.UIRelationshipArrowsChanged, OnRelationshipArrowsChanged);
		Game.ctx.events.RemoveListener(SessionEventType.UIRelationshipHighlighted, OnRelationshipArrowHighlighted);
		Game.ctx.events.RemoveListener(SessionEventType.UIResAssignmentChanged, OnResidentialAssignmentChanged);
		Game.ctx.events.RemoveListener(SessionEventType.UIDebtorChange, OnDebtorChanged);
		Game.ctx.events.RemoveListener(SessionEventType.CrewHealthChanged, OnCrewHealthChanged);
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberKilled, OnCrewMemberKilled);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVanquished, OnCrewVanquished);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleCreated, OnCrewVehicleChanged);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleHealthChanged, OnCrewHealthChanged);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleReassigned, OnCrewVehicleChanged);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleRemoved, OnCrewVehicleRemoved);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnHumanTurnStarted);
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerAggroChanged, OnVizOrAggroChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingTakeoverImmediate, OnBuildingChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerBuildingClearControlImmediate, OnBuildingNoLongerInteractive);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandExecutedOneTurn, OnPlayerCommandExec);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandFinished, OnPlayerCommandExec);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerExploredNode, OnNodeExplored);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerKBChanged, OnPlayerKBChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerKBChangedOnBuilding, OnPlayerKBChangedOnBuilding);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerResourcesChanged, OnResourcesChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerScopedOutBuilding, OnPlayerScopeOut);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerUsedSocialTicket, OnTicketUsed);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerVizChanged, OnVizOrAggroChanged);
		Game.ctx.events.RemoveListener(SessionEventType.SafehouseRaided, OnSafehouseRaided);
		Game.ctx.events.RemoveListener(SessionEventType.CrewAutomationChanged, OnAutoPauseOrResume);
		Game.ctx.events.RemoveListener(SessionEventType.SafehouseRemoved, OnBuildingNoLongerInteractive);
		Game.ctx.events.RemoveListener(SessionEventType.SchemeUpdated, OnSchemeChanged);
		Game.serv.camera.OnCameraMoved.Remove(OnCameraMoved);
		Game.serv.camera.OnScreenChanged.Remove(OnResChanged);
		Game.serv.mouseovers.Unregister(MouseoverType.BuildingPickMouseover);
		_moving.Clear();
		foreach (PickContainer container in _containers)
		{
			container.Release();
		}
		_containers.Clear();
		_hideRequests.Clear();
		_templates = null;
		_parent = null;
	}

	public PickContainer GetContainer(PickType type)
	{
		return _containers[(int)type];
	}

	private GameObject RefreshContainerSize()
	{
		GameObject child = _parent.GetChild("Container");
		child.SetActive(value: false);
		_containerSize = child.GetComponent<RectTransform>().rect;
		return child;
	}

	private void OnScriptStarted(GameScriptEvent ev)
	{
		if (!ev.eid.IsNotValid && !_moving.ContainsKey(ev.eid) && ev.eid.FindEntity()?.config.mobile?.ShowPick == true)
		{
			_moving.Add(ev.eid, PickType.CrewPick);
		}
	}

	private void OnScriptStopped(GameScriptEvent ev)
	{
		_moving.Remove(ev.eid);
	}

	private void OnNewGame(SessionEvent sev)
	{
		RefreshPicksOnKnownBuildings(Game.ctx.players.Human);
		OnCameraMoved();
	}

	private void OnHumanTurnStarted(SessionEvent ev)
	{
		RefreshVisibleBuildingPicks();
	}

	private void OnPotentiallyLoadedGame(ServiceEvent sev)
	{
		if (!Game.ctx.IsPreInteractiveAIGenDone || !Game.ctx.HasSaveFile)
		{
			return;
		}
		RefreshPicksOnKnownBuildings(Game.ctx.players.Human);
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			RefreshAllPlayerCrewPicks(item.PID);
		}
		OnCameraMoved();
	}

	private void OnPlayerKBChanged(SessionEvent _)
	{
		RefreshPicksOnKnownBuildings(Game.ctx.players.Human);
	}

	private void OnPlayerKBChangedOnBuilding(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RefreshPickIfExists(PickType.BuildingPick, sev.eid);
		}
	}

	private void OnCrewVanquished(SessionEvent sev)
	{
		Entity entity = Game.ctx.players.WithID(sev.pid).territory.Safehouse.FindEntity();
		if (entity != null)
		{
			Node node = entity.components.board.GetNode();
			RefreshBuildingPicksOnNode(node);
		}
	}

	private void OnCrewHealthChanged(SessionEvent sev)
	{
		EntityID entityID = Game.ctx.players.WithID(sev.pid).crew.FindVehicleAssignedToPeep(sev.eid);
		GetOrNull(entityID, PickType.CrewPick)?.RefreshContents();
	}

	private bool CanShowPicksForPlayer(PlayerID pid)
	{
		return Game.ctx.players.Human.meetings.IsPlayerMet(pid);
	}

	private void OnCrewVehicleChanged(SessionEvent sev)
	{
		if (CanShowPicksForPlayer(sev.pid))
		{
			Entity entity = sev.eid.FindEntity();
			AddOrRefreshPick(PickType.CrewPick, entity, resetExisting: true);
		}
	}

	private void OnCrewVehicleRemoved(SessionEvent sev)
	{
		Remove(sev.eid, PickType.CrewPick);
	}

	private void OnCrewMemberKilled(SessionEvent sev)
	{
		EntityID entityID = (EntityID)sev.ctx;
		GetOrNull(entityID, PickType.CrewPick)?.RefreshContents();
	}

	private void OnSafehouseRaided(SessionEvent sev)
	{
		GetOrNull(sev.eid, PickType.BuildingPick)?.RefreshContents();
		RefreshVisibleSummaryPicks();
	}

	private void OnResidentialAssignmentChanged(SessionEvent sev)
	{
		GetOrNull(sev.eid, PickType.BuildingPick)?.RefreshContents();
		RefreshVisibleSummaryPicks();
	}

	private void OnAutoPauseOrResume(SessionEvent sev)
	{
		RefreshAllPlayerCrewPicks(sev.pid);
	}

	private void OnVizOrAggroChanged(SessionEvent sev)
	{
		RefreshAllPlayerCrewPicks(sev.pid);
	}

	private void OnNodeExplored(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			RefreshBuildingPicksOnNode(sev.ctx as Node);
		}
	}

	private void OnBuildingNoLongerInteractive(SessionEvent sev)
	{
		Remove(sev.eid, PickType.BuildingPick);
		RefreshVisibleSummaryPicks();
	}

	private void OnBuildingChanged(SessionEvent sev)
	{
		AddOrRefreshPick(PickType.BuildingPick, sev.eid.FindEntity());
		RefreshVisibleSummaryPicks();
	}

	private void OnBuildingChangedIfVisible(SessionEvent sev)
	{
		RefreshPickIfExists(PickType.BuildingPick, sev.eid.FindEntity());
		RefreshVisibleSummaryPicks();
	}

	private void OnBuildingHighlighted(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			BasePick orNull = GetOrNull(sev.eid, PickType.BuildingPick);
			if (orNull != null && orNull is BuildingPick buildingPick)
			{
				buildingPick.OnHighlight();
			}
		}
	}

	private void OnSchemeChanged(SessionEvent sev)
	{
		RefreshSchemePicks();
	}

	private void OnResourcesChanged(SessionEvent sev)
	{
		if (!sev.pid.IsHumanPlayer)
		{
			return;
		}
		PlayerSkills.Unlocked unlockedThisTurn = sev.pid.FindPlayer().skills.UnlockedThisTurn;
		if (unlockedThisTurn.res == null)
		{
			return;
		}
		PickContainer container = GetContainer(PickType.BuildingPick);
		foreach (EntityResDir building in unlockedThisTurn.buildings)
		{
			container.RefreshPickIfExists(building.eid);
		}
	}

	private void OnTicketUsed(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer)
		{
			Entity entity = BuildingUtil.FindBuildingForBizOwner(sev.eid);
			if (entity != null)
			{
				RefreshContentsIfExists(entity.Id, PickType.BuildingPick);
			}
		}
	}

	private void OnOrderArrowsChanged(SessionEvent _)
	{
		RemoveAndReaddOrderArrows();
	}

	private void OnUIResourcesChanged(SessionEvent _)
	{
		RemoveAndReaddResourcePicks();
	}

	private void OnRelationshipArrowHighlighted(SessionEvent evt)
	{
		if (GetOrNull(evt.eid, PickType.RelArrowPick) is RelationshipPick relationshipPick)
		{
			relationshipPick.SetHighlight((bool)evt.ctx);
		}
	}

	private void OnCurrentActiveChanged(SessionEvent sev)
	{
		OnActivationChange(sev.ctx as Entity, active: false);
		OnActivationChange(sev.eid.FindEntity(), active: true);
	}

	private void OnActivationChange(Entity entity, bool active)
	{
		if (entity != null && GetOrNull(entity.Id, PickType.CrewPick) is CrewPick crewPick)
		{
			crewPick.OnActivationChange(active);
		}
	}

	private void OnResChanged()
	{
		RefreshContainerSize();
		RefreshPickContainers(cameraRotated: false);
	}

	private void OnDebtorChanged(SessionEvent _)
	{
		TimerUtil.RunNextFrame(delegate
		{
			RefreshPickContainers(cameraRotated: false);
		});
	}

	private void OnCameraMoved()
	{
		float minZZoom = Game.serv.camera.Settings.minZZoom;
		float maxZZoom = Game.serv.camera.Settings.maxZZoom;
		float num = MathUtil.Interpolate(Game.serv.globals.settings.general.territory.territoryDisplayZoomRange.from, minZZoom, maxZZoom);
		float zoom = Game.serv.camera.GetZoom();
		bool valueOrDefault = Game.ctx?.overlays?.resources?.IsShowingAny == true;
		_hideDueToCamera = !valueOrDefault && zoom > num;
		_lastCameraZoomPercent = MathUtil.Uninterpolate(zoom, minZZoom, maxZZoom);
		bool cameraRotated = ShouldResortPicks();
		RefreshPickContainers(cameraRotated);
	}

	internal void OnPickSortingChanged()
	{
		_picksGotReordered = true;
	}

	private bool ShouldResortPicks()
	{
		if (_hideDueToCamera)
		{
			return false;
		}
		float pitch = Game.serv.camera.GetPitch();
		if (_lastCameraPitch == pitch)
		{
			return false;
		}
		_lastCameraPitch = pitch;
		return true;
	}

	private void RefreshPicksOnKnownBuildings(PlayerInfo player)
	{
		foreach (Node item in player.territory.GetAllKnownNodesExpensive())
		{
			RefreshBuildingPicksOnNode(item);
		}
		RefreshSchemePicks();
	}

	private void RefreshVisibleBuildingPicks()
	{
		GetContainer(PickType.BuildingPick).RefreshShowingPicks();
	}

	private void RefreshVisibleCornerPicks()
	{
		GetContainer(PickType.CornerPick).RefreshShowingPicks();
	}

	private void RefreshVisibleSummaryPicks()
	{
		GetContainer(PickType.SummaryPick).RefreshShowingPicks();
	}

	private void RefreshVisibleSchemePicks()
	{
		GetContainer(PickType.SchemePick).RefreshShowingPicks();
	}

	private void RefreshAllPlayerCrewPicks(PlayerID pid)
	{
		if (!CanShowPicksForPlayer(pid))
		{
			return;
		}
		PlayerCrew crew = pid.FindPlayer().crew;
		foreach (CrewAssignment item in crew.AllCrew)
		{
			if (item.IsInVehicle)
			{
				AddOrRefreshPick(PickType.CrewPick, item.GetVehicle());
			}
		}
		foreach (EntityID allScavengeableCar in crew.AllScavengeableCars)
		{
			AddOrRefreshPick(PickType.CrewPick, allScavengeableCar.FindEntity());
		}
		foreach (EntityID allUnassignedVehicle in crew.AllUnassignedVehicles)
		{
			AddOrRefreshPick(PickType.CrewPick, allUnassignedVehicle.FindEntity());
		}
	}

	private void OnPlayerCommandExec(SessionEvent sev)
	{
		if (!(sev.pid != PlayerID.HumanPlayer) && sev.ctx != null)
		{
			if (sev.ctx is CommandGoto cmd)
			{
				UpdatePicksAfterGoto(cmd);
			}
			CrewAssignment crewForPeep = sev.pid.FindPlayer().crew.GetCrewForPeep(sev.eid);
			if (crewForPeep.IsValid)
			{
				Entity vehicle = crewForPeep.GetVehicle();
				GetOrNull(vehicle, PickType.CrewPick)?.RefreshContents();
			}
		}
	}

	private void OnPlayerScopeOut(SessionEvent sev)
	{
		if (!(sev.pid != PlayerID.HumanPlayer))
		{
			AddOrRefreshPick(PickType.BuildingPick, sev.eid.FindEntity());
			RefreshVisibleSummaryPicks();
		}
	}

	private void OnTerritoryChanged(SessionEvent sev)
	{
		RefreshVisibleSummaryPicks();
		RefreshVisibleCornerPicks();
	}

	private void UpdatePicksAfterGoto(CommandGoto cmd)
	{
		if (cmd?.Path?.nodes == null)
		{
			return;
		}
		foreach (PathNode node in cmd.Path.nodes)
		{
			RefreshBuildingPicksOnNode(node.node);
		}
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		bool isAltHeld = _isAltHeld;
		_isAltHeld = KeyUtil.IsAltDown;
		if (isAltHeld != _isAltHeld)
		{
			RefreshPickContainers(cameraRotated: false);
			return;
		}
		foreach (KeyValuePair<PickTarget, PickType> item in _moving)
		{
			GetContainer(item.Value).RefreshPosition(item.Key);
		}
	}

	internal void CheatTogglePicks()
	{
		_hideDueToCheats = !_hideDueToCheats;
		RefreshPickContainers(cameraRotated: false, forceFullSort: true);
	}

	public void AddSuppressRequest(PickHideRequest req)
	{
		if (!_hideRequests.Contains(req))
		{
			_hideRequests.Add(req);
		}
		RefreshPickContainers(cameraRotated: false, forceFullSort: true);
	}

	public void RemoveSuppressRequest(PickHideRequest req)
	{
		_hideRequests.Remove(req);
		RefreshPickContainers(cameraRotated: false, forceFullSort: true);
	}

	public bool HasSuppressRequestOfType(PickType type)
	{
		for (int i = 0; i < _hideRequests.Count; i++)
		{
			if (_hideRequests[i].type == type)
			{
				return true;
			}
		}
		return false;
	}

	private bool ArePicksShowing(PickType type)
	{
		if (!_hideDueToCamera && !_hideDueToCheats)
		{
			return !HasSuppressRequestOfType(type);
		}
		return false;
	}

	private void RefreshBuildingPicksOnNode(Node node)
	{
		if (!Game.ctx.players.Human.meetings.IsNodeKnown(node))
		{
			return;
		}
		using (ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate())
		{
			node.FindBuildingsToShowGuitarPicks(PlayerID.HumanPlayer, pooledBlockList);
			foreach (Entity item in pooledBlockList)
			{
				AddOrRefreshPick(PickType.BuildingPick, item);
			}
		}
		AddOrRefreshCornerPick(node);
		AddOrRefreshSummaryPick(node);
	}

	private void RefreshSchemePicks()
	{
		foreach (SchemeData item in Game.ctx.players.Human.schemes.GetAllSchemesUnsafe())
		{
			if (item.overallTarget.IsValid)
			{
				AddOrRefreshPick(PickType.SchemePick, item.overallTarget.FindEntity());
			}
		}
		TimerUtil.RunNextFrame(delegate
		{
			RefreshPickContainers(cameraRotated: false);
		});
	}

	private void RemoveAndReaddOrderArrows()
	{
		RemoveAll(PickType.OrderArrowPick);
		foreach (VFXManager.WorldArrowHandle item in Game.ctx.overlays.arrows.GetOrderArrows().GetResourcesToShowUnsafe())
		{
			PickTarget target = new PickTarget(item.GetPickTarget().Id, item);
			AddOrRefreshPick(PickType.OrderArrowPick, target);
		}
	}

	private void OnRelationshipArrowsChanged(SessionEvent sev)
	{
		RemoveAll(PickType.RelArrowPick);
		RemoveAll(PickType.RelSourcePick);
		if (sev.eid.IsNotValid)
		{
			return;
		}
		foreach (VFXManager.WorldArrowHandle item in Game.ctx.overlays.arrows.GetRelArrows().GetResourcesToShowUnsafe())
		{
			AddOrRefreshPick(PickType.RelArrowPick, item.GetPickTarget());
		}
		AddOrRefreshPick(PickType.RelSourcePick, sev.eid.FindEntity());
	}

	private void RemoveAndReaddResourcePicks()
	{
		Dictionary<EntityID, OverlayResDir>.ValueCollection values = Game.ctx.overlays.resources.buildings.Values;
		RemoveAll(PickType.ResourcePick);
		foreach (OverlayResDir item in values)
		{
			AddOrRefreshPick(PickType.ResourcePick, item.eid.FindEntity());
		}
	}

	private void AddOrRefreshCornerPick(Node node)
	{
		var (flag, entity) = Game.ctx.board.CornerCache.FindCorner(node.id);
		if (flag && entity != null)
		{
			AddOrRefreshPick(PickType.CornerPick, entity);
		}
	}

	private void AddOrRefreshSummaryPick(Node node)
	{
		var (flag, entity) = Game.ctx.board.CornerCache.FindCorner(node.id);
		if (flag && entity != null)
		{
			AddOrRefreshPick(PickType.SummaryPick, entity);
		}
	}

	private void AddOrRefreshPick(PickType type, PickTarget target, bool resetExisting = false)
	{
		GetContainer(type).AddOrRefreshPick(target, resetExisting);
	}

	private void RefreshPickIfExists(PickType type, PickTarget target)
	{
		GetContainer(type).RefreshPickIfExists(target);
	}

	private BasePick GetOrNull(PickTarget target, PickType type)
	{
		return GetContainer(type).GetOrNull(target);
	}

	private bool Remove(PickTarget target, PickType type)
	{
		return GetContainer(type).Remove(target);
	}

	private void RemoveAll(PickType type)
	{
		GetContainer(type).RemoveAll();
	}

	private void RefreshContentsIfExists(EntityID entityId, PickType type)
	{
		GetOrNull(entityId, type)?.RefreshContents();
	}

	internal bool IsAnchorWithinMargins(Vector2 anchor)
	{
		Vector2 point = new Vector2(anchor.x / _containerSize.width, anchor.y / _containerSize.height);
		return MARGINS.Contains(point);
	}

	private void RefreshPickContainers(bool cameraRotated, bool forceFullSort = false)
	{
		PickContainer.SortType sortType = ((_picksGotReordered || forceFullSort) ? PickContainer.SortType.Full : (cameraRotated ? PickContainer.SortType.Linear : PickContainer.SortType.None));
		PickType[] aLL_PICK_TYPES = ALL_PICK_TYPES;
		foreach (PickType type in aLL_PICK_TYPES)
		{
			bool show = ArePicksShowing(type);
			GetContainer(type).RefreshAll(show, sortType);
		}
		_picksGotReordered = false;
	}
}
