using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckCrewRank : AbstractVisitRequirement
{
	public enum Rank
	{
		Boss,
		Captain,
		BossOrCaptain,
		None
	}

	public Rank @is;

	public override bool DoesPass(VisitState visit)
	{
		return CheckRank(visit.peep, @is);
	}

	public static bool CheckRank(Entity peep, Rank rank)
	{
		bool item = peep.components.agent.IsBoss().pass;
		bool flag = peep.components.agent.IsCaptain();
		switch (rank)
		{
		case Rank.Boss:
			return item;
		case Rank.Captain:
			return flag;
		case Rank.BossOrCaptain:
			return item || flag;
		case Rank.None:
			if (!item)
			{
				return !flag;
			}
			return false;
		default:
			return false;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
