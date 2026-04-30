using System;
using System.Collections.Generic;
using System.Linq;
using Game.Services.Store;

namespace Game.Session.Achievements;

public sealed class AchievementDef : IAchievementDef
{
	public string steamId;

	public string gogId;

	public string epicId;

	public int internalId;

	public Func<SessionEvent, bool> testFn;

	public List<SessionEventType> triggers;

	public AchievementDef(string steamId, string gogId, string epicId, int id, Func<SessionEvent, bool> testFn, params SessionEventType[] triggers)
	{
		internalId = id;
		this.steamId = steamId;
		this.gogId = gogId;
		this.epicId = epicId;
		this.testFn = testFn;
		this.triggers = triggers.ToList();
	}

	public string GetSteamId()
	{
		return steamId;
	}

	public string GetGOGId()
	{
		return gogId;
	}

	public string GetEpicId()
	{
		return epicId;
	}
}
