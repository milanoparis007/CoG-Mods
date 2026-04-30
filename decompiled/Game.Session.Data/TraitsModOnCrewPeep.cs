using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class TraitsModOnCrewPeep : TraitsMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	protected override Label FindMatchingTrait(ModQuery query)
	{
		if (@if == null || @if.Count == 0)
		{
			return Label.NULL;
		}
		if (query.crewPeepId.IsValid)
		{
			return FindTraitOn(query.crewPeepId);
		}
		return Label.NULL;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		if (query.crewPeepId.IsNotValid)
		{
			return null;
		}
		string text = TraitsMod.FindTraitName(FindTraitOn(query.crewPeepId));
		return Loc.Get("mod.check-traits-crew", "delta", AbstractModifier.FormatDelta(delta), "name", text);
	}

	public override string ToString()
	{
		return "[TraitsModOnCrewPeep check if " + string.Join(",", @if) + "]";
	}
}
