using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataTicketResourceReveal : ConvoData
{
	public List<EntityID> containers;

	public List<EntityID> constructions;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[0];
	}

	public ConvoDataTicketResourceReveal()
	{
	}

	public ConvoDataTicketResourceReveal(List<EntityID> constructionRevs, List<EntityID> containerRevs)
	{
		containers = containerRevs;
		constructions = constructionRevs;
	}
}
