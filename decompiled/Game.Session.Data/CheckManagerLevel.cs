using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckManagerLevel : VisitWithValueRequirement
{
	public Label id;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return (ModulesUtil.GetManagerOrNull(visit.building).manager?.components.agent?.GetLevel(id)).GetValueOrDefault();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = Game.serv.globals.settings.skills.experience.GetLevelup(id)?.Describe((int)value) ?? Loc.Get("ui.requirements.none");
		string message = Loc.Get("ui.requirements.checkcrew.managerxp", "exp", text);
		return new ReqExplanation(DoesPass(visit), message);
	}
}
