using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class AiPersonalityMod : TagListMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	protected override void Validate(ModQuery query)
	{
		NPCSettings npc = Game.serv.globals.settings.npc;
		foreach (Label item in @if)
		{
			if (npc.FindCategoryForAspectLabel(item).IsNotSet)
			{
				Label label = item;
				Logger.Warning("Invalid personality aspect id in mod value: " + label.ToString());
			}
		}
	}

	protected override Fixnum DoEvaluate(ModQuery query, Fixnum source)
	{
		if (!query.pid.IsAnyPlayer)
		{
			return source;
		}
		if (!FindMatchingAspect(query).IsSet)
		{
			return source;
		}
		return (source + delta) * multiplier;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		string text = FindDetails(query);
		return Loc.Get("mod.check-personality", "delta", AbstractModifier.FormatDelta(delta), "details", text);
	}

	private string FindDetails(ModQuery query)
	{
		if (@if == null || @if.Count == 0)
		{
			return Loc.Get("mod.unspecified");
		}
		Label aspectId = FindMatchingAspect(query);
		NPCPersonalityAspect nPCPersonalityAspect = Game.serv.globals.settings.npc.FindAspectByID(aspectId);
		if (nPCPersonalityAspect == null)
		{
			return Loc.Get("mod.unspecified");
		}
		return Loc.Get(nPCPersonalityAspect.lockey);
	}

	private Label FindMatchingAspect(ModQuery query)
	{
		return query.FindPlayer()?.ai?.Data.FindPersonalityAspect(@if) ?? Label.NULL;
	}

	public override string ToString()
	{
		return "[Personality mod if " + string.Join(",", @if) + "]";
	}
}
