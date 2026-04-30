using Game.Core;

namespace Game.Session.Data;

public class SpecialIsTiedHouse : AbstractVisitRequirement
{
	public enum Reason
	{
		None,
		Human,
		Ai,
		Any
	}

	public Reason type;

	public override bool DoesPass(VisitState visit)
	{
		(bool, PlayerID)? tuple = visit.biz?.components.biz?.GetTiedHouseStatus();
		if (!tuple.HasValue)
		{
			return false;
		}
		var (flag, playerID) = tuple.Value;
		switch (type)
		{
		case Reason.None:
			return !flag;
		case Reason.Human:
			if (flag)
			{
				return playerID.IsHumanPlayer;
			}
			return false;
		case Reason.Ai:
			if (flag)
			{
				return playerID.IsAIPlayer;
			}
			return false;
		case Reason.Any:
			return flag;
		default:
			return false;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
