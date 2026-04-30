using System;
using System.Collections.Generic;
using System.Linq;
using Game.Session.Data;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Player;

public static class TicketBoosts
{
	public static ConvoDataNPCSelection MakeReputationBoostData(VisitState visit, Func<Entity, List<Entity>> targFunc)
	{
		return new ConvoDataNPCSelection((from target in targFunc(visit.npc)
			select MakeRepBoost(visit.npc, target)).ToList());
	}

	private static ConvoDataNPCSelection.Entry MakeRepBoost(Entity owner, Entity other)
	{
		string rel = IntroductionsUtils.MakeRelFlavor(owner, other, "convo.ticket-npcboost-flavor.relationship");
		return new ConvoDataNPCSelection.Entry(other.Id, "", rel, "");
	}
}
