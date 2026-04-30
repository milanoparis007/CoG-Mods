using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class UnlockResource : VisitGrant
{
	public Label id;

	public override GrantReq RequiredContext => GrantReq.PlayerID;

	public override void Apply(GrantContext ctx)
	{
		PlayerSkills skills = ctx.GetPlayer().skills;
		if (!skills.HasResourceUnlocked(id))
		{
			if (GetResource() == null)
			{
				Label label = id;
				Logger.Warning("Unlocking resource: invalid resource id " + label.ToString());
			}
			else
			{
				skills.UnlockResource(id, startup: false);
			}
		}
	}

	private Resource GetResource()
	{
		return Game.ctx.simman.FindResource(id);
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.unlockresource.describe", "iconAndName", GetResource().GetIconAndName());
	}
}
