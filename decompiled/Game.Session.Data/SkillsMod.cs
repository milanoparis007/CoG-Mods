using Game.Core;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class SkillsMod : TagListMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	protected override void Validate(ModQuery query)
	{
		SkillSettings skills = Game.serv.globals.settings.skills;
		foreach (Label item in @if)
		{
			if (skills.GetSkill(item) == null)
			{
				Label label = item;
				Logger.Warning("Skills mod: unrecognized skill id " + label.ToString());
			}
		}
	}

	protected override Fixnum DoEvaluate(ModQuery query, Fixnum source)
	{
		if (!query.pid.IsAnyPlayer)
		{
			return source;
		}
		if (!query.FindPlayer().skills.HasSkillAny(@if))
		{
			return source;
		}
		return (source + delta) * multiplier;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		string text = FindSkillName(query);
		return Loc.Get("mod.check-skills", "delta", AbstractModifier.FormatDelta(delta), "name", text);
	}

	private string FindSkillName(ModQuery query)
	{
		if (@if == null || @if.Count == 0)
		{
			return "--";
		}
		PlayerSkills skills = query.FindPlayer().skills;
		foreach (Label item in @if)
		{
			if (skills.HasSkill(item))
			{
				return skills.FindSkillDef(item).GetName();
			}
		}
		return "?";
	}

	public override string ToString()
	{
		return "[SkillsMod check if " + string.Join(",", @if) + "]";
	}
}
