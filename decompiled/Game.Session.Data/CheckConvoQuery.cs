using Game.Core;

namespace Game.Session.Data;

public sealed class CheckConvoQuery : AbstractVisitRequirement
{
	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		return GetPlayer(visit).kb.GetStatusForBuilding(visit.building.Id, id).passed;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
