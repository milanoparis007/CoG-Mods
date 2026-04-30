using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.UI.Session.Convo;

public static class VisitStateExtensions
{
	public static void ConsumeConvoActionsHelper(this VisitState visit)
	{
		CrewCost convoCost = Game.serv.globals.settings.people.social.costs.convoCost;
		visit.crew.peepId.FindEntity().components.agent.DoPay(convoCost);
	}
}
