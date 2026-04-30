using Game.Session.Player;

namespace Game.Session.Data;

public abstract class AbstractVisitRequirement : IVisitRequirement, IRequirement
{
	public bool forcehuman;

	public PlayerInfo GetPlayer(VisitState visit)
	{
		if (!forcehuman)
		{
			return visit.GetPlayer();
		}
		return Game.ctx.players.Human;
	}

	public abstract bool DoesPass(VisitState visit);

	public abstract ReqExplanation Explain(VisitState visit);
}
