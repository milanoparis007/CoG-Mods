using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public class ResEventResultQuest : ResEventResultBase
{
	public override ResEventResultData GenerateResultData(VisitState visit)
	{
		ResEventResultData resEventResultData = new ResEventResultData
		{
			resultId = id
		};
		List<Entity> first = FindHostConnectionsForQuest(visit).Take(5).ToList();
		List<Entity> second = FindUnknownBizOwnersByDistance(visit).Take(5).ToList();
		List<Entity> list = first.Concat(second).ToList();
		Xorshift rng = visit.npc.data.ident.rng;
		resEventResultData.targetNpc = rng.PickElementOrDefault(list)?.Id ?? EntityID.INVALID;
		PopulateEventMessage(resEventResultData);
		return resEventResultData;
	}

	public override void AcceptResult(VisitState visit, ResEventData data)
	{
		if (data.chosenResult != null)
		{
			ResEventResultData chosenResult = data.chosenResult;
			ResidentialEventResultConfig config = chosenResult.GetConfig();
			if (visit.GetPlayer().social.GetRelationshipFromSourceToPlayer(chosenResult.targetNpc) == null)
			{
				TicketIntroductions.PerformIntro(chosenResult.targetNpc, visit.crew.peepId);
			}
			config.grants?.ApplyAll(new GrantContext(visit));
			string text = config.quest?.id;
			if (text != null)
			{
				chosenResult.quuid = Game.ctx.quests.StartQuest(text, chosenResult.targetNpc, fromRequest: false);
			}
		}
	}
}
