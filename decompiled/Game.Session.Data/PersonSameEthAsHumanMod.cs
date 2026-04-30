using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class PersonSameEthAsHumanMod : BaseDeltaMultiplierModifier
{
	public bool expected;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Target;

	public override bool DoesPass(ModQuery query)
	{
		PersonData personData = query.FindTarget()?.data.person;
		if (personData == null)
		{
			return false;
		}
		PersonData person = Game.ctx.players.Human.social.GetPlayerPeep().data.person;
		return personData.eth == person.eth == expected;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get(expected ? "mod.person-same-eth" : "mod.person-diff-eth", "delta", AbstractModifier.FormatDelta(delta));
	}
}
