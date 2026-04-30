using System;
using System.Collections.Generic;
using System.Linq;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Actions;

public class GameScriptQueue : SmartQueue<GameScript>
{
	public struct SavedScript
	{
		public GameScript.Data data;

		public List<GameAction> actions;
	}

	public static bool DEBUG;

	public GameScriptQueueContext context;

	public void Initialize(Entity agent)
	{
		context = new GameScriptQueueContext();
		context.agent = agent;
	}

	public void Release()
	{
		StopAllScripts(success: true);
		context.agent = null;
		context = null;
	}

	public virtual void Run(GameScript script)
	{
		GameScript head = base.Head;
		base.Enqueue(script);
		if (base.Head != head)
		{
			OnScriptEvent(GameScriptEventType.ScriptAfterStarted, script);
		}
	}

	public void StopCurrentScript(bool success)
	{
		if (!base.IsEmpty)
		{
			GameScript script = base.Dequeue();
			OnScriptEvent(GameScriptEventType.ScriptAfterStopped, script);
		}
		if (!base.IsEmpty)
		{
			OnScriptEvent(GameScriptEventType.ScriptAfterStarted, base.Head);
		}
	}

	public void StopAllScripts(bool success)
	{
		while (base.Count > 1)
		{
			PopTail();
		}
		if (!base.IsEmpty)
		{
			GameScript script = base.Dequeue();
			OnScriptEvent(GameScriptEventType.ScriptAfterStopped, script);
		}
	}

	private void OnScriptEvent(GameScriptEventType eventType, GameScript script)
	{
		context?.agent?.components.script.OnScriptEvent(eventType, script);
	}

	public new void Add(GameScript _)
	{
		throw new InvalidOperationException("Add not available, use Run instead");
	}

	public new void Enqueue(GameScript _)
	{
		throw new InvalidOperationException("Enqueue not available, use Run instead");
	}

	public new void Dequeue()
	{
		throw new InvalidOperationException("Dequeue not available, use one of the Stop methods instead");
	}

	public void OnUpdate()
	{
		if (!base.IsEmpty)
		{
			base.Head.OnUpdate();
		}
	}

	public override string ToString()
	{
		return "ScriptQueue: " + string.Join(", ", this.Select((GameScript seq) => $"{seq.data.type}/{seq.data.name}").ToArray());
	}

	public void SaveTo(List<SavedScript> results)
	{
		using IEnumerator<GameScript> enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			GameScript current = enumerator.Current;
			results.Add(new SavedScript
			{
				data = current.data,
				actions = new List<GameAction>(current)
			});
		}
	}

	public void LoadFrom(List<SavedScript> data, bool clearFirst)
	{
		if (clearFirst)
		{
			StopAllScripts(success: true);
		}
		foreach (SavedScript datum in data)
		{
			GameScript script = new GameScript(datum.data.type, datum.data.name, datum.actions);
			Run(script);
		}
	}
}
