using System;
using System.Collections.Generic;

namespace Game.Session.Board;

[Serializable]
public class SeasonConfig
{
	public int seasonBlendASRWidth;

	public FColor32 resGroundColor;

	public FColor32 comGroundColor;

	public FColor32 indGroundColor;

	public List<SeasonData> data;

	public int GetIndex(Season season)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].season == season)
			{
				return i;
			}
		}
		return -1;
	}

	public SeasonData Get(Season season)
	{
		int index = GetIndex(season);
		if (index >= 0)
		{
			return data[index];
		}
		return data[0];
	}

	public SeasonData GetSpring()
	{
		return Get(Season.Spring);
	}

	public SeasonData GetSummer()
	{
		return Get(Season.Summer);
	}

	public SeasonData GetFall()
	{
		return Get(Season.Fall);
	}

	public SeasonData GetWinter()
	{
		return Get(Season.Winter);
	}
}
