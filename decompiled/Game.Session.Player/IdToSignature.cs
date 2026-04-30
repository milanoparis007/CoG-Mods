using Game.Core;

namespace Game.Session.Player;

public struct IdToSignature
{
	public Label id;

	public string signature;

	public IdToSignature(Label id, string signature)
	{
		this.id = id;
		this.signature = signature;
	}
}
