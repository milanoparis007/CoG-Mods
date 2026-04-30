using System;
using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;

namespace Game.Core;

public static class PathfindingUtil
{
	public struct PathSegment
	{
		public WorldPos start;

		public WorldPos end;

		public PathSegment(WorldPos start, WorldPos end)
		{
			this.start = start;
			this.end = end;
		}
	}

	private static readonly bool DEBUG = false;

	private static readonly List<WorldPos> _cardinals = new List<WorldPos>
	{
		new WorldPos(0f, 1f),
		new WorldPos(0f, -1f),
		new WorldPos(1f, 0f),
		new WorldPos(-1f, 0f)
	};

	public static bool PopulatePath(PathData result, List<PathElement> path, WorldPos start, Fixnum maxCost, List<PathNode> exclude = null)
	{
		if (DEBUG)
		{
			Game.serv.debugvis.RemoveAllDebugObjects();
		}
		result.Reset();
		if (path.Count == 0)
		{
			result.world.Add(start);
			return true;
		}
		PathNode source = path[0].source;
		if (!InExcludeList(exclude, source))
		{
			result.nodes.Add(source);
			GenerateWorldPositionsBetween(start, source.node.pos, result.world);
		}
		bool result2 = true;
		using (ListPool<PathElement>.PooledBlockList pooledBlockList = ListPool<PathElement>.Allocate())
		{
			foreach (PathElement item in path)
			{
				if (item.target.costSoFar > maxCost)
				{
					result2 = false;
					break;
				}
				if (!InExcludeList(exclude, item.target))
				{
					result.cost = item.target.costSoFar;
					result.nodes.Add(item.target);
					pooledBlockList.Add(item);
				}
			}
			using ListPool<PathSegment>.PooledBlockList pooledBlockList2 = ListPool<PathSegment>.Allocate();
			ConvertEdgesToSegments(pooledBlockList, pooledBlockList2);
			foreach (PathSegment item2 in pooledBlockList2)
			{
				GenerateWorldPositionsBetween(item2.start, item2.end, result.world);
			}
		}
		FixUpJaggies(result.world);
		if (DEBUG)
		{
			foreach (WorldPos item3 in result.world)
			{
				Game.serv.debugvis.AddCube(item3, Color.blue, 0.2f);
			}
		}
		return result2;
	}

	private static bool InExcludeList(List<PathNode> exclude, PathNode candidate)
	{
		return exclude?.Contains(candidate) ?? false;
	}

	private static void ConvertEdgesToSegments(List<PathElement> edges, List<PathSegment> segments)
	{
		for (int i = 0; i < edges.Count; i++)
		{
			PathElement pathElement = edges[i];
			(WorldPos start, WorldPos end) startEndPos = pathElement.GetStartEndPos();
			WorldPos item = startEndPos.start;
			WorldPos item2 = startEndPos.end;
			WorldPos worldPos = FindEdgePointClosestTo(pathElement.source.node, pathElement.target.node);
			WorldPos worldPos2 = FindEdgePointClosestTo(pathElement.target.node, pathElement.source.node);
			if (DEBUG)
			{
				Game.serv.debugvis.AddCube(worldPos, Color.red);
				Game.serv.debugvis.AddCube(worldPos2, Color.yellow);
			}
			segments.Add(new PathSegment(item, worldPos));
			segments.Add(new PathSegment(worldPos, worldPos2));
			segments.Add(new PathSegment(worldPos2, item2));
		}
	}

	private static WorldPos FindEdgePointClosestTo(Node s, Node t)
	{
		float num = float.MaxValue;
		WorldPos result = s.pos;
		foreach (WorldPos cardinal in _cardinals)
		{
			WorldPos worldPos = s.Transform.RotateAndAdd(cardinal.x, cardinal.y);
			float magnitudeSquared = (worldPos - t.pos).MagnitudeSquared;
			if (magnitudeSquared < num)
			{
				num = magnitudeSquared;
				result = worldPos;
			}
		}
		return result;
	}

	private static void GenerateWorldPositionsBetween(WorldPos start, WorldPos end, List<WorldPos> results)
	{
		WorldPos worldPos = end - start;
		int num = (int)Math.Round(worldPos.Magnitude / 1f);
		WorldPos normalized = worldPos.Normalized;
		results.Add(start);
		for (int i = 1; i < num; i++)
		{
			WorldPos item = start + normalized * i;
			results.Add(item);
		}
	}

	private static void FixUpJaggies(List<WorldPos> list)
	{
		for (int i = 0; i < list.Count - 2 && HasJaggie(list, i); i++)
		{
			list[i] = list[i + 1];
		}
		if (list.Count > 4 && HasJaggie(list, list.Count - 3))
		{
			list[list.Count - 1] = list[list.Count - 2];
		}
	}

	private static bool HasJaggie(List<WorldPos> list, int i)
	{
		WorldPos worldPos = list[i];
		WorldPos worldPos2 = list[i + 1];
		WorldPos worldPos3 = list[i + 2];
		float magnitudeSquared = (worldPos - worldPos2).MagnitudeSquared;
		return (worldPos - worldPos3).MagnitudeSquared < magnitudeSquared;
	}
}
