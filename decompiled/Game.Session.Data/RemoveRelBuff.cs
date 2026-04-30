using Game.Core;
using Game.Services;
using Game.Session.Entities;

namespace Game.Session.Data;

public class RemoveRelBuff : AbstractRelGrant
{
	public bool matchall;

	public override void Apply(GrantContext ctx)
	{
		Entity entity = FindPeep(ctx);
		if (entity != null)
		{
			Relationship item = ctx.visit.GetPlayer().social.FindOrMakeRelationshipsWith(entity.Id).from;
			if (matchall)
			{
				RemoveAllMatching(item, id);
			}
			else
			{
				item.RemoveBuff(id);
			}
		}
	}

	private void RemoveAllMatching(Relationship from, Label id)
	{
		foreach (BuffConfig definition in Game.serv.globals.settings.people.allBuffs.definitions)
		{
			if (definition.id.String.StartsWith(id.String))
			{
				from.RemoveBuff(definition.id);
			}
		}
	}

	public override string Describe(GrantContext ctx)
	{
		return Loc.Get("ui.grants.removerelbuff.describe", "name", NameUtils.GetPeepFullName(FindPeep(ctx).Id));
	}
}
