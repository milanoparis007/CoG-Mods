using Game.Services;
using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class AlwaysFalse : IVisitRequirement, IRequirement, IConvoButtonRequirement
{
	public bool DoesPass(VisitState visit)
	{
		return false;
	}

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		return false;
	}

	public ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(passed: false, Loc.Get("ui.requirements.debug.disabled"));
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return Explain(visit);
	}

	public PlayerInfo GetPlayer(VisitState visit)
	{
		return visit.GetPlayer();
	}
}
