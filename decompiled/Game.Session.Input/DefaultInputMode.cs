using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Input;

public class DefaultInputMode : BaseInputMode
{
	public static bool IsDefaultActive => Game.serv.input.Peek()?.GetType() == typeof(DefaultInputMode);

	public static void Reset(bool force = false)
	{
		if (!IsDefaultActive || force)
		{
			Game.serv.input.Replace(new DefaultInputMode());
		}
	}

	protected override void HandleMouseMove(Vector2 pos, Vector2 lastpos)
	{
		base.HandleMouseMove(pos, lastpos);
		if (!_isDragging & (GUIUtility.hotControl == 0))
		{
			Entity e = Game.ctx.board.PickEntityUnderCursor(pos);
			ProcessHoverOverEntity(pos, e);
		}
	}

	protected override void HandleDragMove(Vector2 pos, Vector2 lastpos)
	{
		Game.ctx.selection.ClearFocus();
		WorldPos worldPos = Game.serv.camera.ScreenToWorldPos(pos);
		WorldPos worldPos2 = Game.serv.camera.ScreenToWorldPos(lastpos);
		WorldPos delta = worldPos - worldPos2;
		OnPan(delta);
	}

	protected override void HandleLeftUp(Vector2 pos, bool wasDragging)
	{
		base.HandleLeftUp(pos, wasDragging);
		if (!wasDragging)
		{
			ProcessClickOnEntity(pos, !UIUtil.IsInputOverUI, leftButton: true);
		}
	}

	protected override void HandleRightUp(Vector2 pos)
	{
		ProcessClickOnEntity(pos, !UIUtil.IsInputOverUI, leftButton: false);
	}

	protected virtual void ProcessClickOnEntity(Vector2 screenpos, bool overEntity, bool leftButton)
	{
		if (leftButton && overEntity)
		{
			Entity e = Game.ctx.board.PickEntityUnderCursor(screenpos);
			Game.ctx.selection.HandleSelect(e);
		}
		else if (!leftButton)
		{
			Game.ctx.selection.HandleClosePopupOrDeselect(fromKeyboard: false);
		}
	}

	protected virtual void ProcessHoverOverEntity(Vector2 screenpos, Entity e)
	{
		Game.ctx.selection.SetFocus(e);
	}
}
