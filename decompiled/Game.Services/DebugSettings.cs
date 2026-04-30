using System.Collections.Generic;
using Game.Services.Store;

namespace Game.Services;

public sealed class DebugSettings
{
	public uint forceRandomSeed = 123u;

	public List<PackID> forcePacks;

	public bool skipSwoop;

	public bool skipDecos;

	public bool skipStarterQuests;

	public bool showDebugZoneColors;

	public bool showGoonsAtStartup;

	public bool ignoreSeasons;

	public bool powerfulGuns;

	public int startingTrucks;

	public bool disableIndirectInstancing;

	public bool dumpModCount;

	public bool dumpModSkills;

	public bool dumpBizCount;

	public bool dumpTraits;

	public bool dumpAIStatsOnExit;

	public int runOnlyAIUntil;
}
