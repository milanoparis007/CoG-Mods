using Game.Services;

namespace Game.Session.Data;

public sealed class CheckAtNpcSafehouse : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		return (visit.npc.components.agent?.GetPlayer())?.territory.GetHeadquartersNode().id.Equals(visit.GetBldgNodeID()) ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ai.notatsafehouse"));
	}
}
