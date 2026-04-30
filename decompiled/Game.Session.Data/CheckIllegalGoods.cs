using Game.Services;

namespace Game.Session.Data;

public class CheckIllegalGoods : AbstractVisitRequirement
{
	public enum Status
	{
		Locked,
		Unlocked
	}

	public Status are;

	public override bool DoesPass(VisitState visit)
	{
		bool num = GetPlayer(visit).social.AreIllegalItemsLocked(visit.npc, visit.building);
		bool flag = are == Status.Locked;
		return num == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.illegalgoods"));
	}
}
