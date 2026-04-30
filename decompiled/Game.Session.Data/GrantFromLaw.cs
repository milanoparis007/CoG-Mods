using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class GrantFromLaw : VisitGrant
{
	public List<Label> ids;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitState;

	public override void Apply(GrantContext ctx)
	{
		foreach (Label id in ids)
		{
			PoliticsSettings.Law law = Game.serv.globals.settings.politics.FindLawDef(id);
			if (Game.ctx.simman.politics.IsLawEnacted(id))
			{
				law.grants.ApplyAll(ctx);
			}
		}
	}
}
