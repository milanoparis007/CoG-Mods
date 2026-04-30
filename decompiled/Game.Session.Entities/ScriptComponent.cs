using System.Collections.Generic;
using Game.Core;
using Game.Session.Actions;

namespace Game.Session.Entities;

public sealed class ScriptComponent : BaseComponent, IAnimatedComponent, ILoadObserverComponent, ISaveObserverComponent
{
	public GameScriptQueue queue;

	public ScriptConfig Config => _baseConfig as ScriptConfig;

	public override void OnAfterComponentCreated(Entity entity, BaseConfig baseconfig)
	{
		base.OnAfterComponentCreated(entity, baseconfig);
		queue = new GameScriptQueue();
		queue.Initialize(_entity);
	}

	public override void OnBeforeEntityDestroyed(bool shutdown)
	{
		queue.StopAllScripts(success: true);
		queue.Release();
		queue = null;
		base.OnBeforeEntityDestroyed(shutdown);
	}

	internal void OnScriptEvent(GameScriptEventType eventType, GameScript script)
	{
		if (_entity.components.mobile != null)
		{
			GameScriptEvent ev = new GameScriptEvent(eventType, script.data, _entity.Id);
			Game.ctx.scriptevents.EnqueueUnchecked(ev);
		}
	}

	public void UpdateOnFrame(GameAnimUpdate anim)
	{
		if (anim.IsAdvancing && !queue.IsEmpty)
		{
			queue.OnUpdate();
		}
	}

	public void OnBeforeSaving()
	{
		_entity.data.script.savedQueue = new List<GameScriptQueue.SavedScript>();
		queue.SaveTo(_entity.data.script.savedQueue);
	}

	public void OnAfterSaving()
	{
		_entity.data.script.savedQueue.Clear();
		_entity.data.script.savedQueue = null;
	}

	public void OnAfterLoading()
	{
		ScriptData script = _entity.data.script;
		foreach (GameScriptQueue.SavedScript item in script.savedQueue)
		{
			foreach (GameAction action in item.actions)
			{
				if (action.update == GameAction.UpdateStatus.Updated)
				{
					action.update = GameAction.UpdateStatus.JustLoaded;
				}
			}
		}
		queue.LoadFrom(script.savedQueue, clearFirst: true);
		script.savedQueue.Clear();
		script.savedQueue = null;
	}
}
