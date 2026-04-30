namespace Game.Session.Actions;

public class ActionDestroySelf : GameAction
{
	internal override void OnStart(bool loaded)
	{
		base.OnStart(loaded);
		Game.ctx.transit.DestroyTransit(base.Agent);
	}
}
