using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Actions;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player.Commands;
using Game.UI.Session;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Input;

public class CarInputMode : DefaultInputMode
{
	private Entity _peep;

	private Entity _car;

	private Node _targetNode;

	private Node _hoverNode;

	private bool _navigateWithLeftButton;

	private PathData _pathValid = new PathData();

	private PathData _pathTooFar = new PathData();

	private VFXManager.NavLineHandle _lineValid;

	private VFXManager.NavLineHandle _lineTooFar;

	private FlyoutManager.FlyoutHandle _costFlyout;

	private static readonly object CAR_MODE_MOUSE_IGNORE_SENTINEL = "CAR_MODE_MOUSE_IGNORE_SENTINEL";

	public CarInputMode(Entity car, bool fromButton)
	{
		_car = car;
		_peep = Game.ctx.players.Human.crew.FindPeepAssignedToVehicle(_car.Id).FindEntity();
		_navigateWithLeftButton = fromButton;
		if (fromButton)
		{
			UpdateTargetPosition(car.data.mobile.worldpos);
			ToggleUIIgnore(ignore: true);
		}
	}

	protected override void HandleRightDown(Vector2 screenPos)
	{
		base.HandleRightDown(screenPos);
		WorldPos worldPos = Game.serv.camera.ScreenToWorldPos(screenPos);
		UpdateTargetPosition(worldPos);
		ToggleUIIgnore(ignore: true);
	}

	public override void OnHandlerDeactivated()
	{
		MoveNodeHighlight(null);
		HideNavLinesAndCost();
		ToggleUIIgnore(ignore: false);
		base.OnHandlerDeactivated();
	}

	private void ToggleUIIgnore(bool ignore)
	{
		Game.serv.ui.ToggleIgnoreMouseRequest(ignore, CAR_MODE_MOUSE_IGNORE_SENTINEL);
	}

	protected override void ProcessHoverOverEntity(Vector2 screenPos, Entity e)
	{
		base.ProcessHoverOverEntity(screenPos, e);
		WorldPos worldPos = Game.serv.camera.ScreenToWorldPos(screenPos);
		Node node = Game.ctx.board.nodes.FindNearestNodeAroundUserInput(worldPos);
		MoveNodeHighlight(node);
		if (_targetNode != null)
		{
			UpdateTargetPosition(worldPos);
		}
	}

	private void MoveNodeHighlight(Node node)
	{
		if (node != _hoverNode)
		{
			Game.ctx.selection.HideNodeHighlight();
			_hoverNode = node;
			if (node != null)
			{
				Game.ctx.selection.ShowNodeHighlightAt(node);
			}
		}
	}

	private void UpdateTargetPosition(WorldPos worldPos)
	{
		Node node = Game.ctx.board.nodes.FindNearestNodeAroundUserInput(worldPos);
		if (node != _targetNode && node != null)
		{
			_targetNode = node;
			UpdateLine();
		}
	}

	private void HideNavLinesAndCost()
	{
		_lineValid = Game.ctx.vfx.HideNavLine(_lineValid);
		_lineTooFar = Game.ctx.vfx.HideNavLine(_lineTooFar);
		_costFlyout = Game.ctx.hud.flyouts.ExpireFlyout(_costFlyout);
	}

	private void UpdateLine()
	{
		if (_targetNode == null)
		{
			return;
		}
		WorldPos startPos = _car.data.mobile.worldpos;
		WorldPos pos = _targetNode.pos;
		int movementPoints = _peep.components.agent.MovesRemaining;
		PlayerID humanPlayer = PlayerID.HumanPlayer;
		Game.ctx.transit.FindDrivingPath(humanPlayer, _peep, pos, delegate(Pathfinding.Result result)
		{
			_pathValid.Reset();
			_pathTooFar.Reset();
			HideNavLinesAndCost();
			if (result.status == Pathfinding.Status.Success)
			{
				if (movementPoints > 0)
				{
					result.PopulatePath(_pathValid, startPos, movementPoints);
				}
				result.PopulatePath(_pathTooFar, startPos, Fixnum.MAX_VALUE, _pathValid.nodes);
				if (_pathValid.world.Count > 0)
				{
					ActionNavigate.PreprocessAddOffsets(_pathValid.world, _car, _pathValid.fuzznode.FindNode());
					_lineValid = Game.ctx.vfx.ShowNavLineFixed(_car, _pathValid.world);
				}
				if (_pathTooFar.world.Count > 0)
				{
					ActionNavigate.PreprocessAddOffsets(_pathTooFar.world, _car, _pathTooFar.fuzznode.FindNode());
					MaybeMergePaths(_pathValid.world, _pathTooFar.world);
					_lineTooFar = Game.ctx.vfx.ShowNavLineDetached(Color.gray, _pathTooFar.world);
				}
				ShowCost(result);
			}
		});
		static void MaybeMergePaths(List<WorldPos> valid, List<WorldPos> toofar)
		{
			if (valid.Count > 0 && toofar.Count > 0)
			{
				toofar[toofar.Count - 1] = valid[0];
			}
		}
		void ShowCost(Pathfinding.Result result)
		{
			PathElement pathElement = result.path.LastOrDefaultFast();
			if (pathElement != null)
			{
				WorldPos pos2 = pathElement.target.node.pos;
				Fixnum costSoFar = pathElement.target.costSoFar;
				string text = Loc.Get("ui.movementcost", "num", Loc.FormatNumber(costSoFar));
				_costFlyout = Game.ctx.hud.flyouts.MakeSimpleTextFlyout(pos2, text, 1f, 0f);
			}
		}
	}

	protected override void ProcessClickOnEntity(Vector2 screenPos, bool overEntity, bool leftButton)
	{
		Game.ctx.selection.HideNodeHighlight();
		bool flag = !leftButton || _navigateWithLeftButton;
		if (overEntity && flag)
		{
			StartDriving(screenPos);
		}
		else
		{
			base.ProcessClickOnEntity(screenPos, overEntity, leftButton);
		}
	}

	private void StartDriving(Vector2 screenPos)
	{
		if (_pathValid.nodes.Count != 0)
		{
			if (_pathValid.nodes.Count < 2 || _pathValid.nodes[0] == _pathValid.nodes[1])
			{
				WorldPos wpos = Game.serv.camera.ScreenToWorldPos(screenPos);
				string text = TextUtil.ColorWrap(Loc.Get("ui.movementcost.fail"), ColorConstants.TEXT_HEX_RED);
				Game.ctx.hud.flyouts.MakeSimpleTextFlyout(wpos, text, 3f);
				Game.ctx.sfx.PlayOutOfPoints(moves: true);
				Game.ctx.events.EnqueueOnce(SessionEventType.UIInsufficientMovementPoints, PlayerID.HumanPlayer);
			}
			else
			{
				CommandGoto cmd = new CommandGoto(PlayerID.HumanPlayer, _peep.Id, _targetNode);
				Game.ctx.players.Human.commands.AddCommandImmediate(cmd);
				Game.ctx.sfx.PlayDriving(_car);
				_targetNode = null;
				_pathValid.Reset();
				_pathTooFar.Reset();
				HideNavLinesAndCost();
				ToggleUIIgnore(ignore: false);
			}
		}
	}
}
