using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Overlays;

public sealed class OverlayArrows
{
	private List<ArrowHandles> _arrows;

	private EntityID _selectedBuilding;

	private CoroutineTask _arrowGenTask;

	public ArrowHandles GetOrderArrows()
	{
		return _arrows[0];
	}

	public ArrowHandles GetRelArrows()
	{
		return _arrows[1];
	}

	public void Initialize()
	{
		_arrows = ArrowHandles.ARROW_TYPES.Select((ArrowType type) => new ArrowHandles(type)).ToList();
	}

	public void Release()
	{
		foreach (ArrowHandles arrow in _arrows)
		{
			_ = arrow;
		}
		_arrows.Clear();
	}

	public void HideAllArrows()
	{
		StopArrowsTaskIfNeeded();
		foreach (ArrowHandles arrow in _arrows)
		{
			arrow.HideAllArrows();
		}
	}

	public void HideOrderArrows()
	{
		HideAllArrows();
		Game.ctx.overlays.HideAnyOverlay();
	}

	public void ShowOrderArrows()
	{
		ArrowHandles orderArrows = GetOrderArrows();
		foreach (ArrowChainLink item in Game.ctx.players.Human.automation.MakeArrowsForAllBuildings())
		{
			orderArrows.ShowArrow(item);
		}
		orderArrows.RefreshColorsForOrders(delegate(VFXManager.WorldArrowHandle handle)
		{
			if (handle.from == _selectedBuilding || handle.to == _selectedBuilding)
			{
				return handle.color;
			}
			return (!_selectedBuilding.IsValid) ? ((Color?)null) : new Color?(Color.gray);
		});
		Game.ctx.overlays.ShowOverlayForOrderArrows();
	}

	public void HideFrontArrows()
	{
		HideAllArrows();
		Game.ctx.overlays.HideAnyOverlay();
	}

	public void DrawFrontArrows()
	{
		Game.ctx.overlays.RecolorAllBuildingsToDefault();
		ArrowHandles orderArrows = GetOrderArrows();
		foreach (OutpostEntry item in Game.ctx.players.Human.outposts.GetOutpostEntriesUnsafe())
		{
			if (item.outpostId.IsNotValid)
			{
				return;
			}
			List<NodeEntry> targetNodes = item.targetNodes;
			Entity entity = item.outpostId.FindBuilding();
			foreach (NodeEntry item2 in targetNodes)
			{
				foreach (Entity item3 in Game.ctx.board.nodes.GetNode(item2.nodeId).FindAllInterestingBuildings())
				{
					Entity biz = BuildingUtil.FindBizForBuilding(item3);
					if (!(item3.Id == entity.Id))
					{
						if (!Game.ctx.players.Human.outposts.IsBizPayingTribute(biz))
						{
							item3.components.model.SetOverlayColor(ColorConstants.ARROW_BUY);
							continue;
						}
						item3.components.model.SetOverlayColor(ColorConstants.ARROW_SAFE);
						ArrowChainLink link = new ArrowChainLink(item3.Id, entity.Id);
						orderArrows.ShowArrow(link);
					}
				}
			}
		}
		orderArrows.RefreshColorsForOrders((VFXManager.WorldArrowHandle handle) => handle.color);
		Game.ctx.overlays.ShowAllFrontsOverlay();
	}

	public void HideRelationshipArrows()
	{
		HideAllArrows();
		Game.ctx.overlays.HideAnyOverlay();
	}

	public void ShowRelationshipArrows(Entity me, List<EntityID> targets, bool suppress = false, bool showpicks = true)
	{
		if (BoardUtil.FindBoardInfoFor(me).boardEntity != null)
		{
			List<EntityID> targets2 = new List<EntityID>(targets);
			StopArrowsTaskIfNeeded();
			_arrowGenTask = Game.serv.sequencer.StartCoroutineTask(MakeRelArrowsTask(me, targets2, showpicks), delegate
			{
				_arrowGenTask = null;
			});
			Game.ctx.overlays.ShowOverlayForRelationshipArrows(targets, suppress);
		}
	}

	private void StopArrowsTaskIfNeeded()
	{
		if (_arrowGenTask != null && !_arrowGenTask.IsFinished)
		{
			_arrowGenTask.Stop();
		}
	}

	private IEnumerator MakeRelArrowsTask(Entity me, List<EntityID> targets, bool showpicks)
	{
		ArrowHandles arrows = GetRelArrows();
		for (int i = 0; i < targets.Count; i++)
		{
			EntityID entityID = targets[i];
			if (entityID == me.Id)
			{
				continue;
			}
			Entity item = BoardUtil.FindBoardInfoFor(entityID).boardEntity;
			if (item != null)
			{
				arrows.ShowArrow(me.Id, item.Id, ColorConstants.ARROW_REL, entityID);
				if (i > 0 && i % 10 == 0)
				{
					MaybeUpdatePicks(showpicks);
					yield return null;
				}
				MaybeUpdatePicks(showpicks);
			}
		}
		void MaybeUpdatePicks(bool show)
		{
			if (show)
			{
				Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.UIRelationshipArrowsChanged, me.Id, PlayerID.HumanPlayer));
			}
		}
	}

	public void RefreshArrowsForAutomation(CrewAssignment crew)
	{
		HideArrowsForAutomation();
		ShowArrowsForAutomation(crew);
	}

	public void ShowArrowsForAutomation(CrewAssignment crew)
	{
		if (crew.IsNotValid)
		{
			return;
		}
		ArrowHandles orderArrows = GetOrderArrows();
		foreach (ArrowChainLink item in Game.ctx.players.Human.automation.MakeArrowsForCrew(crew))
		{
			orderArrows.ShowArrow(item);
		}
		orderArrows.RefreshColorsForOrders((VFXManager.WorldArrowHandle handle) => handle.color);
	}

	public void HideArrowsForAutomation()
	{
		_selectedBuilding = EntityID.INVALID;
		HideAllArrows();
	}

	public void ShowArrowsForBuilding(Entity building)
	{
		ArrowHandles orderArrows = GetOrderArrows();
		_selectedBuilding = building.Id;
		bool isTargetControlled = building.data.building.controlled.Is(PlayerID.HumanPlayer);
		foreach (ArrowChainLink item in Game.ctx.players.Human.automation.MakeArrowsForBuilding(building, isTargetControlled))
		{
			orderArrows.ShowArrow(item);
		}
		orderArrows.RefreshColorsForOrders((VFXManager.WorldArrowHandle handle) => (!isTargetControlled) ? ((!(handle.from == _selectedBuilding)) ? ((!(handle.to == _selectedBuilding)) ? ((!_selectedBuilding.IsValid) ? ((Color?)null) : new Color?(Color.gray)) : new Color?(handle.color)) : new Color?(handle.color)) : new Color?(handle.color));
	}

	public void HideArrowsForBuilding(EntityID buildingId)
	{
		if (!(_selectedBuilding != buildingId))
		{
			_selectedBuilding = EntityID.INVALID;
			HideAllArrows();
		}
	}

	public VFXManager.WorldArrowHandle? FindHandleForContext(EntityID ctx)
	{
		foreach (ArrowHandles arrow in _arrows)
		{
			VFXManager.WorldArrowHandle? result = arrow.FindHandleForContext(ctx);
			if (result.HasValue)
			{
				return result;
			}
		}
		return null;
	}

	public void HighlightArrowForContext(EntityID ctx, Color hl, Color nohl, bool shouldhl)
	{
		VFXManager.WorldArrowHandle? worldArrowHandle = FindHandleForContext(ctx);
		if (worldArrowHandle.HasValue)
		{
			Game.ctx.vfx.RefreshArrowColor(worldArrowHandle.Value, shouldhl ? hl : nohl);
		}
	}

	public static void HighlightConnection(EntityID peepID, bool hl)
	{
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.UIRelationshipHighlighted, peepID, PlayerID.HumanPlayer, hl));
		Game.ctx.overlays.arrows.HighlightArrowForContext(peepID, ColorConstants.ARROW_REL_HL, ColorConstants.ARROW_REL, hl);
	}
}
