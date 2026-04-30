using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class AddRelBuffWithFamily : VisitGrant
{
	public enum Type
	{
		AnyFamily,
		CloseFamily
	}

	public Label id;

	public Type type;

	public override GrantReq RequiredContext => GrantReq.PlayerID | GrantReq.VisitPeep | GrantReq.VisitNpc;

	public override void Apply(GrantContext ctx)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(ctx.visit.npc.Id);
		PlayerInfo player = ctx.GetPlayer();
		foreach (Relationship datum in listOrNull.data)
		{
			if ((type == Type.CloseFamily && datum.IsCloseFamily) || (type == Type.AnyFamily && datum.IsAnyFamily))
			{
				player.social.MeetBuildingOwner(datum.to, oldfriends: false, player.social.PlayerPeepId);
				player.social.AddBuffFrom(datum.to, id);
			}
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get((type == Type.AnyFamily) ? "ui.grants.addrelbuff-allfam.describe" : "ui.grants.addrelbuff-closefam.describe", "name", NameUtils.GetPeepFullName(ctx.visit.npc));
	}
}
