using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckOwnerTrait : AbstractVisitRequirement
{
	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		return visit.npc.data.person.traitIds.Contains(id);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		Trait trait = Game.serv.globals.settings.people.traits.Find(id);
		string text = ((trait != null) ? trait.GetLocName() : id.String);
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ownertrait", "name", text));
	}
}
