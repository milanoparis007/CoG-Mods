using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class RelationshipMod : BaseDeltaMultiplierModifier
{
	public Test @is;

	public Fixnum value = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		return ValueUtil.TestCurrentValue(FindRel(query), @is, value);
	}

	private static Fixnum FindRel(ModQuery query)
	{
		return query.FindPlayer().social.EvaluateRelationshipFromSourceToPlayer(query.targetId);
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.check-relationship", "delta", AbstractModifier.FormatDelta(delta), "current", FindRel(query));
	}
}
