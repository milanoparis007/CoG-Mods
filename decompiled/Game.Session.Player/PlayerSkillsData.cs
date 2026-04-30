using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerSkillsData
{
	public struct SkillGained
	{
		public Label id;

		public EntityID peepId;

		public EntityID fromId;

		public SimTime time;

		public SkillGained(Label id, EntityID peepId, EntityID fromId, SimTime time)
		{
			this = default(SkillGained);
			this.id = id;
			this.peepId = peepId;
			this.fromId = fromId;
			this.time = time;
		}
	}

	public Xorshift rng;

	public List<Label> currentSkills = new List<Label>();

	public List<SkillGained> currentSkillInfos = new List<SkillGained>();

	public List<Label> resUnlocked = new List<Label>();

	public List<Label> playerFlags;

	public Dictionary<Label, int> playerCounters;

	public PlayerSkillsData()
	{
		rng = Game.ctx.scenario.MakeSeededRng<PlayerSkillsData>();
	}

	public int IndexOfSkill(Label skillId)
	{
		return currentSkills.IndexOf(skillId);
	}

	public bool HasSkill(Label skillId)
	{
		return IndexOfSkill(skillId) >= 0;
	}

	public int IndexOfUnlockedRes(Label resId)
	{
		return resUnlocked.IndexOf(resId);
	}

	public bool HasUnlockedRes(Label resId)
	{
		return IndexOfUnlockedRes(resId) >= 0;
	}
}
