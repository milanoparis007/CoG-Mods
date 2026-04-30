namespace Game.UI.Session.Picks;

public sealed class PickHideRequest
{
	public PickType type;

	public object source;

	public PickHideRequest(PickType type, object source)
	{
		this.type = type;
		this.source = source;
	}
}
