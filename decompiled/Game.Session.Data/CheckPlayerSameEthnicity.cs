using Game.Services;

namespace Game.Session.Data;

public class CheckPlayerSameEthnicity : AbstractVisitRequirement
{
	public bool @is = true;

	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).social.PlayerEthnicity == visit.npc.data.person.eth == @is;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ethnicity.same"));
	}
}
