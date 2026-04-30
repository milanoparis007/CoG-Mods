using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Player.AI;

namespace Game.Session.Player;

public sealed class PlayerData
{
	public PlayerID pid;

	public PlayerType type;

	public PlayerFinanceData finances = new PlayerFinanceData();

	public PlayerSkillsData skills = new PlayerSkillsData();

	public PlayerCrewData crew;

	public PlayerSocialData social;

	public PlayerVizData viz = new PlayerVizData();

	public PlayerAIData ai;

	public List<CommandExecutor.CommandTuple> queues = new List<CommandExecutor.CommandTuple>();

	public PlayerTerritoryData territory;

	public PlayerOutpostsData outposts;

	public PlayerGamblingData gambling;

	public PlayerSchemeData scheme;

	public PlayerThroneData throne;

	public AutomationData automation = new AutomationData();

	public PlayerData()
	{
	}

	public PlayerData(short pid, PlayerType type)
	{
		this.pid = new PlayerID(pid);
		this.type = type;
		crew = new PlayerCrewData(this.pid);
		ai = new PlayerAIData(this.pid);
		territory = new PlayerTerritoryData(this.pid);
		outposts = new PlayerOutpostsData(this.pid);
		social = new PlayerSocialData(this.pid);
		gambling = new PlayerGamblingData(this.pid);
		scheme = new PlayerSchemeData(this.pid);
		throne = new PlayerThroneData(this.pid);
	}
}
