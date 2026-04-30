using System;
using Game.Core;
using Game.Services;
using Game.Session.Actions;

namespace Game.Session;

public struct GameScriptEvent : IEventOfType<GameScriptEventType>, IEquatable<GameScriptEvent>
{
	public GameScript.Data data;

	public EntityID eid;

	public GameScriptEventType type { get; set; }

	public GameScriptEvent(GameScriptEventType type, GameScript.Data data, EntityID id)
	{
		this.type = type;
		this.data = data;
		eid = id;
	}

	public bool Equals(GameScriptEvent other)
	{
		if (type == other.type && eid == other.eid)
		{
			return data.Equals(other.data);
		}
		return false;
	}
}
