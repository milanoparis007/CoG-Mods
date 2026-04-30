using Game.Core;

namespace Game.Session.Data;

public class GrantRevokeLaw : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.Nothing;

	public override void Apply(GrantContext ctx)
	{
		Game.ctx.simman.politics.RevokeLaw(Game.serv.globals.settings.politics.FindLawDef(id));
	}
}
