using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public class ResEventResultIntro : ResEventResultBase
{
	public override ResEventResultData GenerateResultData(VisitState visit)
	{
		ResEventResultData resEventResultData = new ResEventResultData
		{
			resultId = id
		};
		List<Entity> first = FindHostConnectionsUnknown(visit).ToList();
		List<Entity> second = FindUnknownBizOwnersByDistance(visit).Take(2).ToList();
		List<Entity> list = first.Concat(second).ToList();
		Xorshift rng = visit.npc.data.ident.rng;
		resEventResultData.targetNpc = rng.PickElementOrDefault(list)?.Id ?? EntityID.INVALID;
		PopulateEventMessage(resEventResultData);
		return resEventResultData;
	}

	public override void AcceptResult(VisitState visit, ResEventData data)
	{
		TicketIntroductions.PerformIntro(data.chosenResult.targetNpc, visit.crew.peepId);
	}
}
