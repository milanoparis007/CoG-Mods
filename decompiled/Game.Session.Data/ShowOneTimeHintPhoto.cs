using Game.Core;

namespace Game.Session.Data;

public sealed class ShowOneTimeHintPhoto : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.Nothing;

	public override void Apply(GrantContext ctx)
	{
		Game.ctx.simman.hints.ShowOneTimeHintPhoto(id);
	}
}
