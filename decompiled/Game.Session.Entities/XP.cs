using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class XP
{
	public struct Levelup
	{
		public Label id;

		public int level;

		public Levelup SetLevel(int newlevel)
		{
			return new Levelup
			{
				id = id,
				level = newlevel
			};
		}

		public static int Compare(Levelup a, Levelup b)
		{
			return a.level - b.level;
		}

		public LevelupChain FindLevelupChain()
		{
			return Game.serv.globals.settings.skills.experience.GetLevelup(id);
		}
	}

	public int current;

	public int lastThreshold;

	public List<Levelup> levelups = new List<Levelup>();

	public Label crewRole = Label.NULL;

	private int GetLevelupIndex(Label id)
	{
		int i = 0;
		for (int count = levelups.Count; i < count; i++)
		{
			if (levelups[i].id == id)
			{
				return i;
			}
		}
		return -1;
	}

	public int GetLevelupLevel(Label id)
	{
		int levelupIndex = GetLevelupIndex(id);
		if (levelupIndex < 0)
		{
			return 0;
		}
		return levelups[levelupIndex].level;
	}

	public bool RemoveLevelup(Label id)
	{
		int levelupIndex = GetLevelupIndex(id);
		if (levelupIndex >= 0)
		{
			levelups.RemoveAt(levelupIndex);
			return true;
		}
		return false;
	}

	public void SetLevelupLevel(Label id, int newlevel)
	{
		int levelupIndex = GetLevelupIndex(id);
		if (levelupIndex >= 0)
		{
			levelups[levelupIndex] = levelups[levelupIndex].SetLevel(newlevel);
		}
		else
		{
			levelups.Add(new Levelup
			{
				id = id,
				level = newlevel
			});
		}
		levelups.StableSort(Levelup.Compare);
	}

	public int GetSumOfLevels()
	{
		int num = 0;
		int i = 0;
		for (int count = levelups.Count; i < count; i++)
		{
			num += levelups[i].level;
		}
		return num;
	}

	public void SetCrewRole(Label role)
	{
		crewRole = role;
	}

	public RoleDef GetCrewRole()
	{
		return Game.serv.globals.settings.people.social.crew.roleSettings.GetRoleById(crewRole);
	}
}
