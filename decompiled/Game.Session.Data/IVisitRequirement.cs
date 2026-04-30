using Game.Session.Player;

namespace Game.Session.Data;

public interface IVisitRequirement : IRequirement
{
	PlayerInfo GetPlayer(VisitState visit);

	bool DoesPass(VisitState visit);

	ReqExplanation Explain(VisitState visit);
}
