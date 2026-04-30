using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class CheckConvoActions : AbstractVisitRequirement
{
	public enum CheckType
	{
		Enough,
		NotEnough
	}

	private enum ResultType
	{
		OK,
		FailInjured,
		FailActions
	}

	public CheckType has;

	public override bool DoesPass(VisitState visit)
	{
		return has switch
		{
			CheckType.Enough => CheckActions(visit) == ResultType.OK, 
			CheckType.NotEnough => CheckActions(visit) != ResultType.OK, 
			_ => false, 
		};
	}

	private ResultType CheckActions(VisitState visit)
	{
		AgentComponent agent = visit.peep.components.agent;
		if (agent.IsInjured())
		{
			return ResultType.FailInjured;
		}
		CrewCost convoCost = Game.serv.globals.settings.people.social.costs.convoCost;
		if (!agent.CanPay(convoCost))
		{
			return ResultType.FailActions;
		}
		return ResultType.OK;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		CrewCost convoCost = Game.serv.globals.settings.people.social.costs.convoCost;
		string message = CheckActions(visit) switch
		{
			ResultType.FailActions => Loc.Get("ui.requirements.convoactions.failactions", "actions", convoCost.actions), 
			ResultType.FailInjured => Loc.Get("ui.requirements.convoactions.failinjured"), 
			_ => Loc.Get("ui.requirements.convoactions.default"), 
		};
		return new ReqExplanation(DoesPass(visit), message);
	}
}
