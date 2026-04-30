using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckCrewPeepLevel : VisitWithValueRequirement
{
	public Label id;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		return (visit.peep?.components.agent?.GetLevel(id)).GetValueOrDefault();
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string text = Game.serv.globals.settings.skills.experience.GetLevelup(id)?.Describe((int)value) ?? Loc.Get("ui.requirements.none");
		string message = Loc.Get("ui.requirements.checkcrew.crewxp", "exp", text);
		return new ReqExplanation(DoesPass(visit), message);
	}
}
