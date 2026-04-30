using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Actions;

public sealed class GameScript : SmartQueue<GameAction>, ISmartQueueElement
{
	public struct Data : IEquatable<Data>
	{
		public GameScriptType type;

		public Label name;

		public override int GetHashCode()
		{
			return (int)type ^ name.Index;
		}

		public override bool Equals(object obj)
		{
			if (obj is Data data)
			{
				return data.Equals(this);
			}
			return false;
		}

		public bool Equals(Data other)
		{
			if (type == other.type)
			{
				return name == other.name;
			}
			return false;
		}
	}

	public Data data;

	public GameScriptQueue queue;

	public GameScriptContext context = new GameScriptContext();

	public bool IsActive { get; private set; }

	public bool IsEnqueued => queue != null;

	public GameScript(GameScriptType type, Label name, params GameAction[] actions)
		: base((actions != null) ? actions.Length : 0)
	{
		data.type = type;
		data.name = name;
		if (actions != null)
		{
			Enqueue(actions);
		}
	}

	public GameScript(GameScriptType type, Label name, List<GameAction> actions)
		: base(actions?.Count ?? 0)
	{
		data.type = type;
		data.name = name;
		if (actions != null)
		{
			Enqueue(actions);
		}
	}

	public GameScript(GameScriptType type, params GameAction[] actions)
		: this(type, Label.NULL, actions)
	{
	}

	public GameScript(GameScriptType type, List<GameAction> actions)
		: this(type, Label.NULL, actions)
	{
	}

	public void OnEnqueued(object queue)
	{
		this.queue = queue as GameScriptQueue;
	}

	public void OnActivated(bool pushedback)
	{
		IsActive = true;
		if (base.IsEmpty)
		{
			StopScript(success: true);
		}
	}

	public void OnDeactivated(bool pushedback)
	{
		IsActive = false;
	}

	public void OnDequeued(bool poppedtail)
	{
		Clear();
		queue = null;
	}

	public new void Add(GameAction action)
	{
		base.Enqueue(action);
	}

	public void Add(List<GameAction> actions)
	{
		Enqueue(actions);
	}

	public void StopCurrentAction(bool success)
	{
		if (IsActive && !success)
		{
			StopScript(success: false);
			return;
		}
		base.Dequeue();
		if (base.IsEmpty && IsActive)
		{
			StopScript(success);
		}
	}

	public void StopScript(bool success)
	{
		if (IsEnqueued && IsActive)
		{
			queue.StopCurrentScript(success);
		}
		else if (GameScriptQueue.DEBUG)
		{
			throw new InvalidOperationException("Can't stop a script that isn't running: " + this);
		}
	}

	public void InterruptWithActions(List<GameAction> actions)
	{
		GameScript element = new GameScript(data.type, data.name, actions);
		queue.PushHead(element);
	}

	internal void OnUpdate()
	{
		if (base.Count == 0)
		{
			if (GameScriptQueue.DEBUG)
			{
				throw new InvalidOperationException("Can't update an empty script, it should have been dequened already");
			}
			StopScript(success: true);
			return;
		}
		try
		{
			GameAction head = base.Head;
			if (!head.WasUpdated && head.IsActive)
			{
				bool loaded = head.update == GameAction.UpdateStatus.JustLoaded;
				head.update = GameAction.UpdateStatus.Updated;
				head.OnStart(loaded);
			}
			if (head.IsActive)
			{
				head.OnUpdate();
			}
		}
		catch (Exception ex)
		{
			if (GameScriptQueue.DEBUG)
			{
				Logger.Error("Action error", ex.Message, ex.StackTrace);
			}
			StopScript(success: false);
		}
	}

	public new void Enqueue(GameAction action)
	{
		throw new InvalidOperationException("Enqueue not available, use Add instead");
	}

	public new void Dequeue()
	{
		throw new InvalidOperationException("Dequeue not available, use one of the Stop methods instead");
	}

	public override string ToString()
	{
		string text = string.Join(", ", this.Select((GameAction action) => action.ToString()).ToArray());
		if (queue == null || queue.IsEmpty)
		{
			text = "(not in queue)" + text;
		}
		return $"Script {data.type}/{data.name}: {text}";
	}
}
