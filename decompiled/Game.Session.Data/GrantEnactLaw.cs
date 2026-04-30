using Game.Core;

namespace Game.Session.Data;

public class GrantEnactLaw : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.Nothing;

	public override void Apply(GrantContext ctx)
	{
		Game.ctx.simman.politics.EnactLaw(Game.serv.globals.settings.politics.FindLawDef(id));
	}
}
