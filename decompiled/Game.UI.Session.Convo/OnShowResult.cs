namespace Game.UI.Session.Convo;

public struct OnShowResult
{
	public ConvoData newData;

	public static readonly OnShowResult CONTINUE;

	public static OnShowResult SetButtonData(ConvoData data)
	{
		return new OnShowResult
		{
			newData = data
		};
	}
}
