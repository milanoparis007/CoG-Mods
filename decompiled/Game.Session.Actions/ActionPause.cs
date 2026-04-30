namespace Game.Session.Actions;

public sealed class ActionPause : GameAction
{
	public float deltaSeconds;

	public float endtime;

	public ActionPause()
	{
	}

	public ActionPause(float secondsAnimTime)
	{
		deltaSeconds = secondsAnimTime;
		endtime = 0f;
	}

	internal override void OnStart(bool loaded)
	{
		base.OnStart(loaded);
		endtime = Game.ctx.clock.AnimState.cumulativeSeconds + deltaSeconds;
	}

	internal override void OnUpdate()
	{
		base.OnUpdate();
		if (Game.ctx.clock.AnimState.cumulativeSeconds >= endtime)
		{
			Stop(success: true);
		}
	}
}
