using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class PlayerTerritoryData
{
	public SimTime lastTerritoryExpansion;

	public Xorshift rng;

	public PlayerColor color;

	public List<EntityID> controlledBuildings = new List<EntityID>();

	public List<NodeID> ownedNodes = new List<NodeID>();

	public SafehouseData safehouseData;

	public List<EntityID> scopeReservations = new List<EntityID>();

	public ResourceAndQtyList productionHistory;

	public PlayerTerritoryData()
	{
	}

	public PlayerTerritoryData(PlayerID pid)
	{
		rng = Game.ctx.scenario.MakeSeededRng(pid);
		safehouseData = new SafehouseData(pid);
	}

	internal void SetColor(PlayerTerritory manager)
	{
		color = FindPlayerColor(manager.PlayerInfo);
	}

	private static PlayerColor FindPlayerColor(PlayerInfo player)
	{
		PickColors pickColors = Game.serv.globals.settings.npc.pickColors;
		List<PlayerColor> list = player.PlayerType switch
		{
			PlayerType.AgentPlayer => pickColors.agents, 
			PlayerType.CopPlayer => pickColors.cops, 
			PlayerType.GangPlayer => pickColors.gangs, 
			PlayerType.HumanPlayer => pickColors.humans, 
			_ => pickColors.goons, 
		};
		return Game.ctx.session.scenario.MakeSeededRng(player.PID).PickElement(list);
	}
}
