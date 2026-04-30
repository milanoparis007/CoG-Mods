using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class SkillDef
{
	public struct Cost
	{
		public ModValue cash;
	}

	public Label id;

	public bool starter;

	public string locname;

	public string locdesc;

	public string locicon;

	public string locconv;

	public string banner;

	public TagList tags = new TagList();

	public VisitRequirementList reqs = new VisitRequirementList();

	public VisitRequirementList visreqs = new VisitRequirementList();

	public Cost cost;

	public List<Label> unlocksResources;

	public QuestDefinition quest;

	public VisitGrantList grants = new VisitGrantList();

	public string GetIcon()
	{
		return Loc.Get(locicon);
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}

	public string GetConvo()
	{
		return Loc.Get(locconv);
	}

	public string GetIconAndName()
	{
		return GetIcon() + " " + GetName();
	}

	public Price GetCashPrice(VisitState visit)
	{
		if (cost.cash != null)
		{
			return new Price(-cost.cash.Evaluate(visit.MakeOwnerModQuery()));
		}
		return Price.ZERO;
	}

	public static bool Matcher(Label id, SkillDef item)
	{
		return id == item.id;
	}

	public static bool QuestIDMatcher(Label questId, SkillDef item)
	{
		return questId.String == item.quest?.id;
	}
}
