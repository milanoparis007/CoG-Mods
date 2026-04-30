using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class AddRelBuff : AbstractRelGrant
{
	public override void Apply(GrantContext ctx)
	{
		Entity entity = FindPeep(ctx);
		if (entity != null)
		{
			ctx.visit.GetPlayer().social.FindOrMakeRelationshipsWith(entity.Id).from.AddBuff(crewpeep: ctx.visit?.crew.peepId ?? EntityID.INVALID, buffId: id);
		}
	}

	public override string Describe(GrantContext ctx)
	{
		string peepFullName = NameUtils.GetPeepFullName(FindPeep(ctx).Id);
		return Loc.Get("ui.grants.addrelbuff.describe", "name", peepFullName);
	}
}
