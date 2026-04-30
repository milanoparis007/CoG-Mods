using System;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Actions;

public abstract class GameAction : AbstractSmartQueueElement
{
	public enum UpdateStatus
	{
		NotUpdated,
		Updated,
		JustLoaded
	}

	public UpdateStatus update;

	public bool WasUpdated => update == UpdateStatus.Updated;

	public GameScript Script => base.Queue as GameScript;

	public GameScriptQueue ScriptQueue => Script?.queue;

	public GameScriptQueueContext Context => ScriptQueue?.context;

	public Entity Agent => ScriptQueue?.context?.agent;

	internal virtual void OnStart(bool loaded)
	{
	}

	internal virtual void OnUpdate()
	{
	}

	internal virtual void OnStop()
	{
	}

	public void Stop(bool success)
	{
		if (base.IsEnqueued && base.IsActive)
		{
			Script.StopCurrentAction(success);
		}
		else if (GameScriptQueue.DEBUG)
		{
			throw new Exception("Can't stop action that isn't running: " + this);
		}
	}

	public override void OnDeactivated(bool pushedback)
	{
		if (!pushedback)
		{
			OnStop();
		}
		base.OnDeactivated(pushedback);
	}
}
