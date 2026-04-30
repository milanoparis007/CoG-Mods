using System;
using Game.Core;
using Game.Services;

namespace Game.Session;

public sealed class ScriptEventBus : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	private sealed class BusImpl : EventBus<GameScriptEventType, GameScriptEvent>
	{
		protected override bool Equals(GameScriptEventType x, GameScriptEventType y)
		{
			return x == y;
		}

		protected override int ToInt(GameScriptEventType x)
		{
			return (int)x;
		}
	}

	private BusImpl _bus;

	public override void OnInitializeDone()
	{
		_bus = new BusImpl();
		_bus.Initialize();
	}

	public override void OnReleased()
	{
		_bus.Release();
		_bus = null;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		_bus.ProcessQueue();
	}

	public void Flush()
	{
		_bus.ProcessQueue();
	}

	public bool AddListener(GameScriptEventType type, Action<GameScriptEvent> listener)
	{
		return _bus.AddListener(type, listener);
	}

	public bool RemoveListener(GameScriptEventType type, Action<GameScriptEvent> listener)
	{
		return _bus.RemoveListener(type, listener);
	}

	public void EnqueueOnce(GameScriptEvent ev)
	{
		_bus.SendNextFrameIfNew(ev);
	}

	public void EnqueueUnchecked(GameScriptEvent ev)
	{
		_bus.SendNextFrame(ev);
	}

	public void SendImmediate(GameScriptEvent ev)
	{
		_bus.SendImmediate(ev);
	}
}
