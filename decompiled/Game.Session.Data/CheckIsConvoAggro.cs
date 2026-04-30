using Game.Core;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckIsConvoAggro : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		PlayerID pid = visit.pid;
		PlayerID other = visit.npc?.data.agent?.pid ?? PlayerID.INVALID;
		return FindAggro(pid, other) == expected;
	}

	private bool FindAggro(PlayerID source, PlayerID other)
	{
		if (source.IsNotValid || other.IsNotValid)
		{
			return false;
		}
		return other.FindPlayer().ai?.combat?.IsAttackAllowed(source) == true;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
