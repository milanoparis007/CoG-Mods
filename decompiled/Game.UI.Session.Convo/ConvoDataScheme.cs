using Game.Core;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataScheme : ConvoData
{
	public Label selected;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[0];
	}

	public ConvoDataScheme()
	{
	}

	public ConvoDataScheme(Label selected)
	{
		this.selected = selected;
	}
}
