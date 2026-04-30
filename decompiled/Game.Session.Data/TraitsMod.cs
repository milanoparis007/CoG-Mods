using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public class TraitsMod : TagListMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Target;

	protected override void Validate(ModQuery query)
	{
		TraitList traits = Game.serv.globals.settings.people.traits;
		foreach (Label item in @if)
		{
			if (traits.Find(item) == null)
			{
				Label label = item;
				Logger.Warning("Traits mod: unrecognized trait id " + label.ToString());
			}
		}
	}

	protected override Fixnum DoEvaluate(ModQuery query, Fixnum source)
	{
		if (!FindMatchingTrait(query).IsSet)
		{
			return source;
		}
		return (source + delta) * multiplier;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		string text = FindTraitName(FindMatchingTrait(query));
		return Loc.Get("mod.check-traits-npc", "delta", AbstractModifier.FormatDelta(delta), "name", text);
	}

	protected virtual Label FindMatchingTrait(ModQuery query)
	{
		if (@if == null || @if.Count == 0)
		{
			return Label.NULL;
		}
		return FindTraitOn(query.targetId);
	}

	protected virtual Label FindTraitOn(EntityID eid)
	{
		Entity entity = eid.FindEntity();
		if (entity == null)
		{
			Logger.Warning($"Cannot find the right peep in mod {this}");
			return Label.NULL;
		}
		return entity.data.person.traitIds.FindFirstMatch(@if);
	}

	protected static string FindTraitName(Label trait)
	{
		if (trait.IsSet)
		{
			Trait trait2 = Game.serv.globals.settings.people.traits.Find(trait);
			if (trait2 != null)
			{
				return trait2.GetLocName();
			}
		}
		return "?";
	}

	public override string ToString()
	{
		return "[TraitsMod check if " + string.Join(",", @if) + "]";
	}
}
