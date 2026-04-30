using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataTributeStart : ConvoData
{
	public OutpostID outpost;

	public Price cashamt;

	public string name;

	public Demand.State demandState;

	public override bool IsConvoStepEnabled(VisitState _, ConvoButtonState __)
	{
		return cashamt.cash > 0;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4]
		{
			"amt",
			Loc.Price(cashamt),
			"collector",
			name
		};
	}

	public ConvoDataTributeStart()
	{
	}

	public ConvoDataTributeStart(OutpostID outpost, Price cashamt, string name)
	{
		this.outpost = outpost;
		this.name = name;
		this.cashamt = cashamt;
	}
}
