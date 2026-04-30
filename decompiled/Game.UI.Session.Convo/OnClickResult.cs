namespace Game.UI.Session.Convo;

public struct OnClickResult
{
	public static readonly OnClickResult CONTINUE = default(OnClickResult);

	public static readonly OnClickResult END_CONVERSATION = new OnClickResult
	{
		endConversation = true
	};

	public static readonly OnClickResult PAUSE_CONVERSATION = new OnClickResult
	{
		pauseConversation = true
	};

	public bool pauseConversation;

	public bool endConversation;
}
