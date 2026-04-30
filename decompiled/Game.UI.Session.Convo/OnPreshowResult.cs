namespace Game.UI.Session.Convo;

public struct OnPreshowResult
{
	public ConvoData newData;

	public static readonly OnPreshowResult CONTINUE;

	public static OnPreshowResult SetButtonData(ConvoData data)
	{
		return new OnPreshowResult
		{
			newData = data
		};
	}
}
