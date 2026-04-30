using System;
using Game.Core;
using Game.Services;

namespace Game.Session;

public struct SessionEvent : IEventOfType<SessionEventType>, IEquatable<SessionEvent>
{
	public PlayerID pid;

	public EntityID eid;

	public object ctx;

	public SessionEventType type { get; set; }

	public SessionEvent(SessionEventType type, EntityID eid, PlayerID pid, object ctx = null)
	{
		this.type = type;
		this.eid = eid;
		this.pid = pid;
		this.ctx = ctx;
	}

	public T GetCtx<T>()
	{
		return (T)ctx;
	}

	public bool Equals(SessionEvent other)
	{
		if (type == other.type && eid == other.eid && pid == other.pid)
		{
			return ctx == other.ctx;
		}
		return false;
	}

	public override bool Equals(object obj)
	{
		if (obj is SessionEvent sessionEvent)
		{
			return sessionEvent.Equals(this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ((int)type << 16) ^ (pid.id << 8) ^ eid.index ^ (ctx?.GetHashCode() ?? 0);
	}

	public static SessionEvent Make(SessionEventType type, PlayerID pid)
	{
		return new SessionEvent(type, EntityID.INVALID, pid);
	}

	public static SessionEvent Make(SessionEventType type)
	{
		return new SessionEvent(type, EntityID.INVALID, Game.ctx.clock.CurrentPlayer);
	}

	public static SessionEvent Make(SessionEventType type, EntityID eid)
	{
		return new SessionEvent(type, eid, Game.ctx.clock.CurrentPlayer);
	}
}
