using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Sim;

public static class BusinessUpdate
{
	private static List<(NodeID nid, Fixnum val)> _tmpAverageRelsPerNode = new List<(NodeID, Fixnum)>();

	private static List<(Node node, Fixnum val)> _tmpFromNeighbors = new List<(Node, Fixnum)>();

	public static void Tick(bool initial)
	{
		try
		{
			ClearAOEEffectsOnNodes();
			UpdateBusinessModules(initial);
			UpdateGamblingModules(initial);
		}
		catch (Exception ex)
		{
			Game.serv.stats.LogException(ex);
		}
		try
		{
			UpdateRespectFromRelationships();
			UpdateRespectFromTerritory();
			UpdateRespectFromEthnicity();
			UpdateRespectFromSafehouses();
		}
		catch (Exception ex2)
		{
			Game.serv.stats.LogException(ex2);
		}
		try
		{
			UpdateHeatFromRelationships();
			UpdateHeatFromNeighbors();
			UpdateHeatFromBusinesses();
		}
		catch (Exception ex3)
		{
			Game.serv.stats.LogException(ex3);
		}
		RecalculateHeatAndRespectForNodes(initial);
	}

	private static void ClearAOEEffectsOnNodes()
	{
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			item.respect.ResetPerTurnRespect();
			item.heat.ResetPerTurnHeat();
		}
	}

	private static void UpdateBusinessModules(bool initial)
	{
		SimTime now = Game.ctx.clock.Now;
		foreach (Entity item in Game.ctx.simman.businesses.GetAllBizWithOwnersUnsafe())
		{
			BuildingUtil.FindBuildingForBiz(item).components.modules?.DoUpdate(now, initial);
		}
	}

	private static void UpdateGamblingModules(bool initial)
	{
		SimTime now = Game.ctx.clock.Now;
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			foreach (EntityID allMyGamblingHouse in item.gambling.GetAllMyGamblingHouses())
			{
				allMyGamblingHouse.FindEntity().components.modules?.DoUpdate(now, initial);
			}
		}
	}

	private static void UpdateRespectFromRelationships()
	{
		RespectSettings respect = Game.serv.globals.settings.people.social.respect;
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			item.social.ProduceAverageRelPerNode(_tmpAverageRelsPerNode);
			if (_tmpAverageRelsPerNode.Count == 0)
			{
				continue;
			}
			Fixnum fixnum = respect.bizRelationshipMultiplier.Evaluate(item.PID);
			foreach (var (nodeId, fixnum2) in _tmpAverageRelsPerNode)
			{
				if (fixnum2.IsNotZero)
				{
					Fixnum delta = fixnum2 * fixnum;
					nodeId.FindNode().respect.IncrementBizRespect(item.PID, delta);
				}
			}
			_tmpAverageRelsPerNode.Clear();
		}
	}

	private static void UpdateRespectFromTerritory()
	{
		RespectSettings respect = Game.serv.globals.settings.people.social.respect;
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			Fixnum vOut = respect.fromNeighborOutside.Evaluate(item.PID);
			Fixnum vIn = respect.fromNeighborInside.Evaluate(item.PID);
			item.territory.ProduceRespectFromNeighbors(_tmpFromNeighbors, vIn, vOut);
			if (_tmpFromNeighbors.Count == 0)
			{
				continue;
			}
			foreach (var (node, delta) in _tmpFromNeighbors)
			{
				node.respect.IncrementNeighborRespect(item.PID, delta);
			}
			_tmpFromNeighbors.Clear();
		}
	}

	private static void UpdateRespectFromEthnicity()
	{
		RespectSettings respect = Game.serv.globals.settings.people.social.respect;
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			foreach (Respect datum in item.respect.data)
			{
				if (!(datum.GetPerTurnValue() == 0))
				{
					MaybeIncrementEthnicityRespect(Game.ctx.players.WithID(datum.pid), item, respect);
				}
			}
		}
	}

	private static void MaybeIncrementEthnicityRespect(PlayerInfo player, Node node, RespectSettings settings)
	{
		Label playerEthnicity = player.social.PlayerEthnicity;
		if (Game.ctx.board.GetMainEthnicityAtNode(node) == playerEthnicity)
		{
			Fixnum delta = settings.fromSameEthnicity.Evaluate(player.PID);
			node.respect.IncrementEthnicity(player.PID, delta);
		}
	}

	private static void UpdateRespectFromSafehouses()
	{
		RespectSettings respect = Game.serv.globals.settings.people.social.respect;
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			BuildingComponent building = item.components.building;
			if (building.IsSafehouse)
			{
				Node node = item.components.board.GetNode();
				if (node != null)
				{
					PlayerID safehouseOwner = building.SafehouseOwner;
					Fixnum delta = respect.fromSafehouse.Evaluate(safehouseOwner);
					node.respect.IncrementSafehouse(safehouseOwner, delta);
				}
			}
		}
	}

	private static void UpdateHeatFromRelationships()
	{
		HeatSettings heat = Game.serv.globals.settings.people.social.heat;
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			item.social.ProduceAverageRelPerNode(_tmpAverageRelsPerNode);
			if (_tmpAverageRelsPerNode.Count == 0)
			{
				continue;
			}
			Fixnum fixnum = heat.bizRelationshipMultiplier.Evaluate(item.PID);
			foreach (var (nodeId, fixnum2) in _tmpAverageRelsPerNode)
			{
				if (fixnum2.scaled < 0)
				{
					Fixnum delta = fixnum2 * fixnum;
					nodeId.FindNode().heat.IncrementUnpopularity(item.PID, delta);
				}
			}
			_tmpAverageRelsPerNode.Clear();
		}
	}

	private static void UpdateHeatFromNeighbors()
	{
		ModValue fromNeighborsProportion = Game.serv.globals.settings.people.social.heat.fromNeighborsProportion;
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			foreach (Heat datum in item.heat.data)
			{
				Fixnum val = datum.CalculatePropagationValue(new ModQuery(datum.pid, item));
				if (val.IsNotZero)
				{
					SpreadHeatToNeighbors(item, datum.pid, val, fromNeighborsProportion);
				}
			}
		}
	}

	private static void UpdateHeatFromBusinesses()
	{
		Fixnum fixnum = Game.serv.globals.settings.people.social.heat.fromBusinesses.Evaluate(PlayerID.HumanPlayer);
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			int num = IllegalBizCount(item);
			if (num > 0)
			{
				item.heat.IncrementFromIllegalBusiness(PlayerID.HumanPlayer, num * fixnum);
			}
		}
	}

	private static int IllegalBizCount(Node node)
	{
		int num = 0;
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsWithPred(PlayerID.HumanPlayer, (BuildingComponent bc, PlayerID pid) => Game.ctx.players.Human.territory.IsControlled(bc.entity), pooledBlockList);
		foreach (Entity item in pooledBlockList)
		{
			if (IsBizIllegal(item))
			{
				num++;
			}
		}
		return num;
	}

	private static bool IsBizIllegal(Entity building)
	{
		foreach (IBizModule bizModule in ModulesUtil.GetBizModules(building))
		{
			foreach (MfgItem item in bizModule.ProduceAllItemsInCurrentRecipe())
			{
				if (item.illegal)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static void SpreadHeatToNeighbors(Node node, PlayerID pid, Fixnum val, ModValue multiplier)
	{
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID edgeId = edges[i];
			if (edgeId.IsNotValid)
			{
				continue;
			}
			NodeEdge nodeEdge = edgeId.FindEdge();
			if (nodeEdge.IsRoad)
			{
				Node node2 = nodeEdge.FindOtherNode(node);
				if (!node2.owner.Is(pid))
				{
					Fixnum delta = val * multiplier.Evaluate(new ModQuery(pid, node));
					node2.heat.IncrementFromNeighbors(pid, delta);
				}
			}
		}
	}

	private static void RecalculateHeatAndRespectForNodes(bool initial)
	{
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			HeatAndRespect.RecomputeRespectForAllPlayers(item, initial);
			HeatAndRespect.RecomputeHeatForAllPlayers(item);
		}
	}
}
