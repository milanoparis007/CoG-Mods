using System.Collections.Generic;
using System.Linq;
using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class SkillSettings : IValidatingSettings
{
	public List<SkillDef> items = new List<SkillDef>();

	public Dictionary<Label, SkillTrackDef> aiskilltracks = new Dictionary<Label, SkillTrackDef>();

	public ExperienceSettings experience = new ExperienceSettings();

	public KeyedListCache<Label, SkillDef> _skills;

	public KeyedListCache<Label, SkillDef> _skillByQuestId;

	public LabelDictionary<List<Label>> skillStarterPacks = new LabelDictionary<List<Label>>();

	public LabelDictionary<List<Label>> resourceStarterPacks = new LabelDictionary<List<Label>>();

	public SkillDef GetSkill(Label id)
	{
		if (items == null)
		{
			Logger.Error("Prematurely quering skills");
			return null;
		}
		_skills = _skills ?? new KeyedListCache<Label, SkillDef>(items, SkillDef.Matcher);
		return _skills.Get(id);
	}

	public SkillDef GetSkillByQuestID(Label questId)
	{
		if (items == null)
		{
			Logger.Error("Prematurely quering skills");
			return null;
		}
		_skillByQuestId = _skillByQuestId ?? new KeyedListCache<Label, SkillDef>(items, SkillDef.QuestIDMatcher);
		return _skillByQuestId.Get(questId);
	}

	public List<Label> GenerateSkillTrack(Label id, IRandom rng)
	{
		SkillTrackDef skillTrackDef = aiskilltracks.FindOrNull(id);
		if (skillTrackDef != null)
		{
			return skillTrackDef.GenerateFlatList(rng);
		}
		Logger.Warning("Unknown skill track", id);
		return new List<Label>();
	}

	public IEnumerable<SkillDef> FindAllStarterSkills()
	{
		return from def in items
			where def.starter
			orderby def.GetName()
			select def;
	}

	public void Validate()
	{
		foreach (SkillTrackDef value in aiskilltracks.Values)
		{
			foreach (List<Label> item in value)
			{
				foreach (Label item2 in item)
				{
					_ = item2;
				}
			}
		}
	}

	public void ValidateAllSkillsHaveQuests(List<SkillDef> skills)
	{
		foreach (SkillDef skill in skills)
		{
			_ = skill;
		}
	}
}
