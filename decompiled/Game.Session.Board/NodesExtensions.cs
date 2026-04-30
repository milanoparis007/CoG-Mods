using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Board;

public static class NodesExtensions
{
	[DebuggerDisplay("{DebugString}")]
	internal struct EthnicityHeatmapEntry
	{
		public Label eth;

		public float value;

		private string DebugString => $"{eth}: {value}";

		public static EthnicityHeatmapEntry MakeEntryAt(Label eth, Node node, float factor)
		{
			float num = factor * (Game.ctx.heatmaps.FindEthnicityMap(eth)?.GetValueFast(node.pos) ?? 0f);
			return new EthnicityHeatmapEntry
			{
				eth = eth,
				value = num
			};
		}
	}

	public static Node FindNode(this NodeID nodeId)
	{
		if (!nodeId.IsValid)
		{
			return null;
		}
		return Game.ctx.board.nodes.GetNode(nodeId);
	}

	public static NodeEdge FindEdge(this NodeEdgeID edgeId)
	{
		if (!edgeId.IsValid)
		{
			return null;
		}
		return Game.ctx.board.nodes.GetEdge(edgeId);
	}

	public static Node FindOtherNode(this NodeEdge edge, NodeID nodeId)
	{
		return edge.GetOtherNodeID(nodeId).FindNode();
	}

	public static Node FindOtherNode(this NodeEdge edge, Node node)
	{
		return edge.GetOtherNodeID(node.id).FindNode();
	}

	public static Node FindNeighbor(this Node node, Direction dir)
	{
		return node.GetEdgeID(dir).FindEdge()?.GetOtherNodeID(node.id).FindNode();
	}

	public static EthnicityDef FindMainEthnicity(this Node node)
	{
		return Game.serv.globals.settings.ethnicities.FindEthnicityDef(node.maineth);
	}

	public static bool CanBeScopedOut(this Node node, PlayerID pid)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsToScopeOut(pid, pooledBlockList);
		return pooledBlockList.Count > 0;
	}

	public static List<EntityID> MakeListOfScopeOutCandidates(this Node node, PlayerID pid)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsToScopeOut(pid, pooledBlockList);
		return pooledBlockList.Select((Entity e) => e.Id).ToList();
	}

	public static EntityID MakeOneScopeOutCandidate(this Node node, PlayerID pid)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsToScopeOut(pid, pooledBlockList);
		return pooledBlockList.LastOrDefaultFast()?.Id ?? EntityID.INVALID;
	}

	public static bool HasBuildingsToScopeOut(this Node node, PlayerID pid)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsToScopeOut(pid, pooledBlockList);
		return pooledBlockList.Count > 0;
	}

	public static bool HasBuildingsForInteraction(this Node node, PlayerID pid)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		node.FindBuildingsForInteraction(pid, pooledBlockList);
		return pooledBlockList.Count > 0;
	}

	public static void FindBuildingsToShowGuitarPicks(this Node node, PlayerID pid, List<Entity> results)
	{
		node.FindBuildingsWithPred(pid, (BuildingComponent c, PlayerID p) => c.CanBeScopedOutByPlayer(p) || c.IsInteractableByPlayer(p) || c.IsSafehouseOrControlledByAny(), results);
	}

	public static void FindBuildingsToScopeOut(this Node node, PlayerID pid, List<Entity> results)
	{
		node.FindBuildingsWithPred(pid, (BuildingComponent c, PlayerID p) => c.CanBeScopedOutByPlayer(p), results);
	}

	public static void FindBuildingsForInteraction(this Node node, PlayerID pid, List<Entity> results)
	{
		node.FindBuildingsWithPred(pid, (BuildingComponent c, PlayerID p) => c.IsInteractableByPlayer(p), results);
	}

	public static void FindBuildingsWithPred(this Node node, PlayerID pid, Func<BuildingComponent, PlayerID, bool> pred, List<Entity> results)
	{
		results.Clear();
		foreach (EntityID item in node.contained)
		{
			Entity entity = item.FindEntity();
			BuildingComponent buildingComponent = entity?.components.building;
			if (buildingComponent != null && pred(buildingComponent, pid))
			{
				results.Add(entity);
			}
		}
	}

	public static void FindAllBuildings(this Node node, List<Entity> results)
	{
		results.Clear();
		foreach (EntityID item in node.contained)
		{
			results.Add(item.FindEntity());
		}
	}

	public static List<Entity> FindAllInterestingBuildings(this Node node)
	{
		List<Entity> list = new List<Entity>();
		node.FindAllInterestingBuildings(list);
		return list;
	}

	public static void FindAllInterestingBuildings(this Node node, List<Entity> results, bool clearFirst = true)
	{
		if (clearFirst)
		{
			results.Clear();
		}
		foreach (EntityID item in node.interesting)
		{
			results.Add(item.FindEntity());
		}
	}

	public static List<Node> FindAllNeighbors(this Node node)
	{
		List<Node> list = new List<Node>();
		node.FindAllNeighbors(list);
		return list;
	}

	public static void FindAllNeighbors(this Node node, List<Node> neighbors, bool clearFirst = true)
	{
		if (clearFirst)
		{
			neighbors.Clear();
		}
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID edgeId = edges[i];
			if (edgeId.IsValid)
			{
				Node item = edgeId.FindEdge().FindOtherNode(node);
				neighbors.Add(item);
			}
		}
	}

	public static List<Node> FindRoadNeighbors(this Node node)
	{
		List<Node> list = new List<Node>();
		node.FindRoadNeighbors(list);
		return list;
	}

	public static void FindRoadNeighbors(this Node node, List<Node> neighbors, bool clearFirst = true)
	{
		if (clearFirst)
		{
			neighbors.Clear();
		}
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID edgeId = edges[i];
			if (edgeId.IsValid)
			{
				NodeEdge nodeEdge = edgeId.FindEdge();
				if (nodeEdge.IsRoad)
				{
					Node item = nodeEdge.FindOtherNode(node);
					neighbors.Add(item);
				}
			}
		}
	}

	public static NodeEdge FindEdgeOrNull(this Node node, Node neighbor)
	{
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID edgeId = edges[i];
			if (edgeId.IsValid)
			{
				NodeEdge nodeEdge = edgeId.FindEdge();
				NodeID otherNodeID = nodeEdge.GetOtherNodeID(node.id);
				if (otherNodeID.IsValid && otherNodeID == neighbor.id)
				{
					return nodeEdge;
				}
			}
		}
		return null;
	}

	public static string GetCornerNameForDialog(this Node node)
	{
		Label roadName = node.GetRoadName(isVertical: true);
		Label roadName2 = node.GetRoadName(isVertical: false);
		if (!roadName.IsSet || !roadName2.IsSet)
		{
			if (!roadName.IsSet)
			{
				if (!roadName2.IsSet)
				{
					return null;
				}
				return Loc.Get("ui.cornerinfo.street.bold", "name", roadName2.String);
			}
			return Loc.Get("ui.cornerinfo.street.bold", "name", roadName.String);
		}
		return Loc.Get("ui.cornerinfo.corner.bold", "hname", roadName2.String, "vname", roadName.String);
	}

	public static string GetCornerNameShort(this Node node, bool addPrefix = true)
	{
		Label roadName = node.GetRoadName(isVertical: true);
		Label roadName2 = node.GetRoadName(isVertical: false);
		if (!(roadName.IsSet && roadName2.IsSet && addPrefix))
		{
			if (!roadName.IsSet || !roadName2.IsSet)
			{
				if (!roadName.IsSet)
				{
					if (!roadName2.IsSet)
					{
						return null;
					}
					return Loc.Get("ui.cornerinfo.street.short", "name", roadName2.String);
				}
				return Loc.Get("ui.cornerinfo.street.short", "name", roadName.String);
			}
			return Loc.Get("ui.cornerinfo.corner.shorter", "hname", roadName2.String, "vname", roadName.String);
		}
		return Loc.Get("ui.cornerinfo.corner.short", "hname", roadName2.String, "vname", roadName.String);
	}

	private static Label GetRoadName(this Node node, bool isVertical)
	{
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdge nodeEdge = edges[i].FindEdge();
			if (nodeEdge != null && nodeEdge.IsValid && nodeEdge.IsVertical == isVertical && nodeEdge.roadName.IsSet)
			{
				return nodeEdge.roadName;
			}
		}
		return Label.NULL;
	}

	internal static (List<Label> eths, List<float> vals) FindEthnicities(this Node node, int maxcount = -1)
	{
		List<Label> ethnicitiesUniqueSorted = Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted();
		SplitMix64 rng = new SplitMix64((uint)node.id.index);
		bool flag = node.FindIfInsideSafeMargins();
		float factor = (flag ? 1f : Game.ctx.session.mapconfig.playerStart.mapEdgePlacementPenalty);
		using ListPool<EthnicityHeatmapEntry>.PooledBlockList pooledBlockList = ListPool<EthnicityHeatmapEntry>.Allocate();
		pooledBlockList.AddRange(ethnicitiesUniqueSorted.Select((Label eth) => EthnicityHeatmapEntry.MakeEntryAt(eth, node, factor)));
		rng.Shuffle(pooledBlockList);
		pooledBlockList.StableSort((EthnicityHeatmapEntry a, EthnicityHeatmapEntry b) => Math.Sign(b.value - a.value));
		while (maxcount >= 0 && pooledBlockList.Count > maxcount)
		{
			pooledBlockList.RemoveLastOrDefault();
		}
		while (pooledBlockList.Count > 0 && pooledBlockList.LastOrDefaultFast().value <= 0f)
		{
			pooledBlockList.RemoveLastOrDefault();
		}
		return (eths: pooledBlockList.Select((EthnicityHeatmapEntry e) => e.eth).ToList(), vals: pooledBlockList.Select((EthnicityHeatmapEntry e) => e.value).ToList());
	}

	internal static bool FindIfInsideSafeMargins(this Node node)
	{
		MapConfig mapconfig = Game.ctx.session.mapconfig;
		IntSize mapSize = mapconfig.map.mapSize;
		float mapEdgePlacementMargin = mapconfig.playerStart.mapEdgePlacementMargin;
		Rect rect = new Rect(mapEdgePlacementMargin, mapEdgePlacementMargin, (float)mapSize.width - 2f * mapEdgePlacementMargin, (float)mapSize.height - 2f * mapEdgePlacementMargin);
		WorldPos pos = node.pos;
		return rect.Contains(new Vector2(pos.x, pos.y));
	}
}
