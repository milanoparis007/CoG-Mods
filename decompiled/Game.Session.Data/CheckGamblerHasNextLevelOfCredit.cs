using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckGamblerHasNextLevelOfCredit : AbstractVisitRequirement
{
	public override bool DoesPass(VisitState visit)
	{
		PlayerGambling gambling = visit.pid.FindPlayer().gambling;
		GamblerState state = gambling.FindGamblerState(visit.npc);
		DebtLevelDef debtLevelDef = gambling.FindNextDebtLevel(state);
		DebtLevelDef debtLevelDef2 = gambling.FindCurrentDebtLevel(state);
		return debtLevelDef != debtLevelDef2;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
