using System.Linq;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckAggroGangExists : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		visit.GetPlayer();
		foreach (PlayerInfo item in Game.ctx.players.all.Where((PlayerInfo x) => x.IsJustGang))
		{
			if (item.ai.combat.IsAggroAnyType(visit.pid))
			{
				return true;
			}
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.aggro-gang"));
	}
}
