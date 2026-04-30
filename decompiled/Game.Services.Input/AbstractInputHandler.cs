using Game.Core;
using UnityEngine;

namespace Game.Services.Input;

public abstract class AbstractInputHandler : IInputHandler
{
	public virtual void OnHandlerActivated()
	{
	}

	public virtual void OnHandlerDeactivated()
	{
	}

	public virtual bool OnPrimary(Vector2 before, Vector2 after, InputPhase phase)
	{
		return false;
	}

	public virtual bool OnSecondary(Vector2 before, Vector2 after, InputPhase phase)
	{
		return false;
	}

	public virtual bool OnTertiary(Vector2 before, Vector2 after, InputPhase phase)
	{
		return false;
	}

	public virtual bool OnPan(WorldPos delta)
	{
		return false;
	}

	public virtual bool OnZoom(float zoom, bool tween)
	{
		return false;
	}

	public virtual bool OnRotate(float direction, bool tween)
	{
		return false;
	}
}
