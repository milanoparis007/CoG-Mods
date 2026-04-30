using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class PersonRelativeOfHumanMod : BaseDeltaMultiplierModifier
{
	public bool expected;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		Entity entity = query.FindTarget();
		if (entity?.config.person == null)
		{
			return false;
		}
		return (Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(entity.Id)?.IsAnyFamily ?? false) == expected;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get(expected ? "mod.person-rel-yes" : "mod.person-rel-no", "delta", AbstractModifier.FormatDelta(delta));
	}
}
