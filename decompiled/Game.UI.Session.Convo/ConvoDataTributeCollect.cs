using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataTributeCollect : ConvoData
{
	public OutpostID outpost;

	public Price collected;

	public Price expenses;

	public Price Delta => new Price(collected.cash + expenses.cash);

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[6]
		{
			"collected",
			Loc.Price(collected),
			"expenses",
			Loc.Price(expenses),
			"delta",
			Loc.Price(Delta)
		};
	}

	public ConvoDataTributeCollect()
	{
	}

	public ConvoDataTributeCollect(OutpostID outpost, Price collected, Price expenses)
	{
		this.outpost = outpost;
		this.collected = collected;
		this.expenses = expenses;
	}
}
