using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataOutpostExpansion : ConvoData
{
	public OutpostID outpostId;

	public Label expansionId;

	public NodeID targetNodeId;

	public Price monthlyCost;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		OutpostSettings.ExpansionDef expansionDef = FindExpansion();
		string text = Loc.Price(monthlyCost, abs: true);
		return new string[6]
		{
			"expansion-npc",
			Loc.Get(expansionDef.convonpc, "amt", text),
			"expansion-say",
			Loc.Get(expansionDef.convosay, "amt", text),
			"amt",
			text
		};
	}

	public ConvoDataOutpostExpansion()
	{
	}

	public ConvoDataOutpostExpansion(OutpostID outpostId, Label expansionId, NodeID targetNodeId, VisitState visit)
	{
		OutpostSettings.ExpansionDef expansionDef = Game.serv.globals.settings.people.social.outposts.FindExpansion(expansionId);
		this.outpostId = outpostId;
		this.expansionId = expansionId;
		this.targetNodeId = targetNodeId;
		monthlyCost = expansionDef.FindMonthlyCost(visit.MakeOwnerModQuery());
	}

	public OutpostSettings.ExpansionDef FindExpansion()
	{
		return Game.serv.globals.settings.people.social.outposts.FindExpansion(expansionId);
	}
}
