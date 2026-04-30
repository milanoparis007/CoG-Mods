using System;
using Game.Core;
using Game.Services;

namespace Game.Session;

public sealed class SessionEventBus : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	private sealed class BusImpl : EventBus<SessionEventType, SessionEvent>
	{
		protected override bool Equals(SessionEventType x, SessionEventType y)
		{
			return x == y;
		}

		protected override int ToInt(SessionEventType x)
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

	public bool AddListener(SessionEventType type, Action<SessionEvent> listener)
	{
		return _bus.AddListener(type, listener);
	}

	public bool RemoveListener(SessionEventType type, Action<SessionEvent> listener)
	{
		return _bus.RemoveListener(type, listener);
	}

	public void EnqueueOnce(SessionEventType type)
	{
		_bus.SendNextFrameIfNew(SessionEvent.Make(type));
	}

	public void EnqueueOnce(SessionEventType type, PlayerID pid)
	{
		_bus.SendNextFrameIfNew(SessionEvent.Make(type, pid));
	}

	public void EnqueueOnce(SessionEventType type, PlayerID pid, EntityID eid)
	{
		_bus.SendNextFrameIfNew(new SessionEvent(type, eid, pid));
	}

	public void EnqueueOnce(SessionEvent ev)
	{
		_bus.SendNextFrameIfNew(ev);
	}

	public void SendImmediate(SessionEventType type)
	{
		_bus.SendImmediate(SessionEvent.Make(type));
	}

	public void SendImmediate(SessionEventType type, PlayerID pid)
	{
		_bus.SendImmediate(SessionEvent.Make(type, pid));
	}

	public void SendImmediate(SessionEvent ev)
	{
		_bus.SendImmediate(ev);
	}
}
