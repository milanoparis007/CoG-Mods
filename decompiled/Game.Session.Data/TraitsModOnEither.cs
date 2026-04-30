using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class TraitsModOnEither : TraitsMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	protected override Label FindMatchingTrait(ModQuery query)
	{
		if (@if == null || @if.Count == 0)
		{
			return Label.NULL;
		}
		if (query.targetId.IsValid)
		{
			Label result = FindTraitOn(query.targetId);
			if (result.IsSet)
			{
				return result;
			}
		}
		if (query.crewPeepId.IsValid)
		{
			Label result2 = FindTraitOn(query.crewPeepId);
			if (result2.IsSet)
			{
				return result2;
			}
		}
		return Label.NULL;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		bool isSet = FindTraitOn(query.targetId).IsSet;
		string text = TraitsMod.FindTraitName(FindMatchingTrait(query));
		return Loc.Get(isSet ? "mod.check-traits-npc" : "mod.check-traits-crew", "delta", AbstractModifier.FormatDelta(delta), "name", text);
	}

	public override string ToString()
	{
		return "[TraitsModOnEither check if " + string.Join(",", @if) + "]";
	}
}
