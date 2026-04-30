using Game.Services;

namespace Game.UI.Session.Convo;

public struct ConvoButtonState
{
	public ConvoButtonDef def;

	public ConvoData data;

	public ConvoButtonState(ConvoButtonDef def, ConvoData data)
	{
		this.def = def;
		this.data = data;
	}
}
