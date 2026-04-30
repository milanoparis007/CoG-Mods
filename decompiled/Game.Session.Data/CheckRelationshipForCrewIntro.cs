using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckRelationshipForCrewIntro : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		if (visit?.npc == null)
		{
			return false;
		}
		PlayerInfo player = GetPlayer(visit);
		return player.crew.CrewGrowth.CheckFriendIntroRequirements(player, visit.npc.Id).item1;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		PlayerInfo player = GetPlayer(visit);
		TupleStruct<bool, Fixnum> tupleStruct = player.crew.CrewGrowth.CheckFriendIntroRequirements(player, visit.npc.Id);
		int num = (int)tupleStruct.item2;
		return new ReqExplanation(tupleStruct.item1, Loc.Get("ui.requirements.relationship.crew", "relNeeded", num));
	}
}
