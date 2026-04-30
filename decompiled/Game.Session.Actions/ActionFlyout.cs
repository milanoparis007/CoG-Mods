namespace Game.Session.Actions;

public sealed class ActionFlyout : GameAction
{
	public string message;

	public float seconds = 2f;

	internal override void OnStart(bool loaded)
	{
		base.OnStart(loaded);
		Stop(success: true);
	}
}
