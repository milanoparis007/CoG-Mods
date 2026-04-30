using System.Collections.Generic;
using Game.Services;

namespace Game.Session.Data;

public class CheckCrewHasASchemeToOffer : AbstractVisitRequirement
{
	public bool expected;

	public override bool DoesPass(VisitState visit)
	{
		List<SchemeDef> allSchemeDefs = Game.serv.globals.settings.schemes.GetAllSchemeDefs();
		bool flag = false;
		foreach (SchemeDef item in allSchemeDefs)
		{
			if (item.startup.visreqs.AllPass(visit))
			{
				flag = true;
			}
		}
		return expected == flag;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
