using System;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Input;

public abstract class BaseInputMode : IInputHandler
{
	private readonly float MinMovementMouse = 3f;

	protected bool _isDragging;

	protected float _deadZone;

	private static readonly object MOUSE_DRAG_IGNORE_SENTINEL = "MOUSE_DRAG_IGNORE_SENTINEL";

	private bool _isRotating;

	public virtual void OnHandlerActivated()
	{
		_deadZone = MinMovementMouse;
	}

	public virtual void OnHandlerDeactivated()
	{
	}

	protected virtual void HandleMouseMove(Vector2 pos, Vector2 lastpos)
	{
	}

	protected virtual void HandleLeftDown(Vector2 pos)
	{
	}

	protected virtual void HandleLeftUp(Vector2 pos, bool wasDragging)
	{
	}

	protected virtual void HandleDragStart(Vector2 pos)
	{
	}

	protected virtual void HandleDragMove(Vector2 pos, Vector2 lastpos)
	{
	}

	protected virtual void HandleDragEnd(Vector2 pos)
	{
	}

	protected virtual void HandleRightDown(Vector2 pos)
	{
	}

	protected virtual void HandleRightUp(Vector2 pos)
	{
	}

	public virtual bool OnPan(WorldPos delta)
	{
		Vector3 asVector3XZ = delta.AsVector3XZ;
		Vector3 v = Game.serv.camera.GetPosition().AsVector3XZ - asVector3XZ;
		Game.serv.camera.SetPosition(CameraPos.FromVectorXZ(v));
		return true;
	}

	public virtual bool OnZoom(float zoom, bool tween)
	{
		if (UIUtil.IsInputOverUI && !IgnoreMouse.CheckIfAllUIElementsIgnoreScroll())
		{
			return false;
		}
		CameraService camera = Game.serv.camera;
		float num = 1f + Math.Abs(zoom) * camera.Settings.mouseZoomMultiplier;
		float zoom2 = camera.GetZoom();
		float zoom3 = ((zoom < 0f) ? (zoom2 * num) : (zoom2 / num)) * camera.Settings.mouseZoomHeightMultiplier;
		camera.SetZoom(zoom3, tween ? new CameraTween?(CameraTween.DEFAULT_ZOOM_TWEEN) : ((CameraTween?)null));
		return true;
	}

	public virtual bool OnRotate(float direction, bool tween)
	{
		if (UIUtil.IsInputOverUI && !IgnoreMouse.CheckIfAllUIElementsIgnoreScroll())
		{
			return false;
		}
		CameraService camera = Game.serv.camera;
		float angle = camera.GetRotation() + direction;
		camera.SetRotation(angle, tween ? new CameraTween?(CameraTween.DEFAULT_ZOOM_TWEEN) : ((CameraTween?)null));
		return true;
	}

	public virtual bool OnPrimary(Vector2 before, Vector2 pos, InputPhase phase)
	{
		if (UIUtil.IsInputOverUI)
		{
			return false;
		}
		switch (phase)
		{
		case InputPhase.HoverMoved:
			HandleMouseMove(pos, before);
			break;
		case InputPhase.ButtonBegan:
			HandleLeftDown(pos);
			HandleMouseMove(pos, before);
			ToggleDragging(value: false);
			break;
		case InputPhase.ButtonMoved:
			if (!_isDragging && Vector2.Distance(pos, before) > _deadZone)
			{
				HandleDragStart(pos);
				ToggleDragging(value: true);
			}
			if (_isDragging)
			{
				HandleDragMove(pos, before);
			}
			break;
		case InputPhase.ButtonEnded:
			HandleMouseMove(pos, before);
			if (_isDragging)
			{
				HandleDragEnd(pos);
			}
			HandleLeftUp(pos, _isDragging);
			ToggleDragging(value: false);
			break;
		}
		return true;
	}

	private void ToggleDragging(bool value)
	{
		_isDragging = value;
		Game.serv.ui.ToggleIgnoreMouseRequest(value, MOUSE_DRAG_IGNORE_SENTINEL);
	}

	public virtual bool OnSecondary(Vector2 before, Vector2 after, InputPhase phase)
	{
		switch (phase)
		{
		case InputPhase.ButtonBegan:
			HandleRightDown(after);
			return true;
		case InputPhase.ButtonEnded:
			HandleRightUp(after);
			return true;
		default:
			return false;
		}
	}

	public virtual bool OnTertiary(Vector2 before, Vector2 after, InputPhase phase)
	{
		switch (phase)
		{
		case InputPhase.ButtonBegan:
			_isRotating = true;
			break;
		case InputPhase.ButtonEnded:
			_isRotating = false;
			break;
		case InputPhase.ButtonMoved:
			if (_isRotating)
			{
				CameraService camera = Game.serv.camera;
				Vector2 vector = after - before;
				camera.IncrementPitch(vector.y * -1f * camera.Settings.mousePitchMultiplier);
				camera.IncrementRotation(vector.x * camera.Settings.mouseRotationMultiplier);
			}
			break;
		default:
			return false;
		}
		return true;
	}
}
