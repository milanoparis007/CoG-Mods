using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CheckBackroomInConvo : AbstractVisitRequirement
{
	public bool empty;

	public override bool DoesPass(VisitState visit)
	{
		ModulesComponent modulesComponent = visit.building?.components.modules;
		if (modulesComponent == null)
		{
			return false;
		}
		return modulesComponent.FindBackroomModule() == null == empty;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
