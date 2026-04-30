using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.UI.Mouseovers;
using Game.UI.Session.Picks;
using UnityEngine;

namespace Game.Session.Board;

public sealed class SelectionManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	private List<Entity> _allHighlighted;

	private List<EntityID> _highlighted = new List<EntityID>(32);

	public Entity CurrentActive { get; private set; }

	public Entity CurrentFocus { get; private set; }

	public Entity PreviousActive { get; private set; }

	public bool HasActive => CurrentActive != null;

	public bool HasFocus => CurrentFocus != null;

	public bool IsNodePickerShowing => Game.ctx.vfx.IsCornerShowing();

	public override void OnInitializeDone()
	{
		_allHighlighted = new List<Entity>(32);
		Game.ctx.events.AddListener(SessionEventType.OnSavingStatusChanged, OnSaving);
	}

	public override void OnReleased()
	{
		Game.ctx.events.RemoveListener(SessionEventType.OnSavingStatusChanged, OnSaving);
		RemoveHighlights();
	}

	private void OnSaving(SessionEvent obj)
	{
		if (Game.ctx.IsSaving)
		{
			ClearActive();
			ClearFocus();
		}
	}

	private static void SendEvent(SessionEventType type, Entity current, Entity previous)
	{
		EntityID eid = current?.Id ?? EntityID.INVALID;
		Game.ctx.events.SendImmediate(new SessionEvent(type, eid, PlayerID.HumanPlayer, previous));
	}

	public void HandleSelect(Entity e)
	{
		if (!Game.ctx.IsSaving)
		{
			SetActive(e);
		}
	}

	public void HandleDeselect()
	{
		SetActive(null);
	}

	public void HandleClosePopupOrDeselect(bool fromKeyboard)
	{
		if (Game.ctx.hud.tickers.IsClickOverTicker())
		{
			Game.ctx.hud.tickers.HandleRightClick();
		}
		else if (!Game.ctx.hud.CanClosePopup() || !Game.ctx.hud.CloseNextPopup())
		{
			if (HasActive)
			{
				HandleDeselect();
			}
			else if ((!Game.ctx.hud.CanCloseDialog() || !Game.ctx.hud.CloseNextDialog()) && fromKeyboard)
			{
				Game.ctx.RequestQuit();
			}
		}
	}

	public void ShowNodeHighlightAt(Node node)
	{
		if (!IsNodePickerShowing)
		{
			Game.ctx.vfx.ShowCorner(show: true);
		}
		Shader.SetGlobalInt("_Highlighted_Node_ID", node.id.index);
		Game.ctx.vfx.UpdateCornerPosition(node);
		_highlighted.Clear();
		_highlighted.AddRange(node.contained);
		foreach (EntityID item in _highlighted)
		{
			ToggleHighlight(item.FindEntity(), show: true);
		}
	}

	public void HideNodeHighlight()
	{
		Game.ctx.vfx.ShowCorner(show: false);
		Shader.SetGlobalInt("_Highlighted_Node_ID", 0);
		foreach (EntityID item in _highlighted)
		{
			ToggleHighlight(item.FindEntity(), show: false);
		}
		_highlighted.Clear();
	}

	public void ToggleNodeBorder(Node node, bool show)
	{
		bool isNodePickerShowing = IsNodePickerShowing;
		if (show && !isNodePickerShowing)
		{
			ShowNodeHighlightAt(node);
		}
		if (!show && isNodePickerShowing)
		{
			HideNodeHighlight();
		}
	}

	public bool IsHighlighted(Entity e)
	{
		return _allHighlighted.Contains(e);
	}

	public void ToggleHighlight(Entity e, bool show)
	{
		if (IsHighlighted(e) != show)
		{
			if (show)
			{
				_allHighlighted.Add(e);
			}
			else
			{
				_allHighlighted.Remove(e);
			}
			e.components.selection.OnHighlightChange(show);
			if (show)
			{
				Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingHighlight, e.Id, PlayerID.HumanPlayer));
			}
		}
	}

	public void AddHighlight(Entity e)
	{
		ToggleHighlight(e, show: true);
	}

	public void RemoveHighlights()
	{
		while (_allHighlighted.Count > 0)
		{
			ToggleHighlight(_allHighlighted[0], show: false);
		}
	}

	public void SetFocus(Entity e)
	{
		Entity currentFocus = CurrentFocus;
		if (e == currentFocus)
		{
			UpdateFocusedMouseover(focusChanged: false);
			return;
		}
		if (CurrentFocus != null)
		{
			CurrentFocus = null;
			currentFocus?.components.selection?.OnFocusChange(focused: false);
		}
		int value = -1000;
		if (e != null)
		{
			CurrentFocus = e;
			e.components.selection?.OnFocusChange(focused: true);
			value = e.Id.index;
		}
		Shader.SetGlobalInt("_Focus_ID", value);
		SendEvent(SessionEventType.SelectionFocusChange, CurrentFocus, currentFocus);
		UpdateFocusedMouseover(focusChanged: true);
	}

	public void ClearFocus()
	{
		SetFocus(null);
	}

	private void UpdateFocusedMouseover(bool focusChanged)
	{
		if (CurrentFocus != null)
		{
			Game.serv.mouseovers.OnMouseOverOrMove(MouseoverType.GameBoardMouseover, null, focusChanged);
		}
		else
		{
			Game.serv.mouseovers.OnMouseOut(MouseoverType.GameBoardMouseover);
		}
	}

	public void SetActive(Entity e)
	{
		Entity currentActive = CurrentActive;
		if (e != currentActive)
		{
			if (CurrentActive != null)
			{
				CurrentActive = null;
				currentActive?.components.selection?.OnActivationChange(activated: false);
			}
			int value = -1000;
			if (e != null)
			{
				CurrentActive = e;
				e.components.selection?.OnActivationChange(activated: true);
				value = e.Id.index;
			}
			Shader.SetGlobalInt("_Active_ID", value);
			SendEvent(SessionEventType.SelectionActivationChange, CurrentActive, currentActive);
		}
	}

	public void ClearActive()
	{
		SetActive(null);
	}

	public void UpdateAnimations(GameAnimUpdate _)
	{
		PreviousActive = CurrentActive;
	}

	public void ProcessBuildingActivation(Entity building, bool activated)
	{
		EntityComponents components = building.components;
		components.modules?.OnActivation(activated);
		PoliceStationComponent police = components.police;
		if (police != null && police.HasStation && components.building.IsInteractableByPlayer(PlayerID.HumanPlayer))
		{
			components.police.OnStationActivationChange(activated);
		}
		else if (components.building.IsRaidTarget(PlayerID.HumanPlayer))
		{
			if (activated && components.building.CanBeRaidedByPlayerCrew(PlayerID.HumanPlayer))
			{
				ScavengeUtil.ShowSafehouseRaidPopup(building);
			}
		}
		else
		{
			BuildingUtil.FindBizForBuilding(building)?.components.biz?.OnBizActivationChange(building, activated);
			components.residence?.OnResidenceActivationChange(activated);
			components.civic?.OnActivationChange(activated);
		}
	}
}
