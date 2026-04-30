using System.Collections.Generic;
using ClipperLib;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using UnityEngine;

namespace Game.Session.Player;

public class PolygonClipper
{
	private const float _clipscale = 1000f;

	private List<WorldPos> tmp_corners = new List<WorldPos>(4);

	private static WorldPos IntPointToWorld(IntPoint pos)
	{
		return new WorldPos((float)pos.X / 1000f, (float)pos.Y / 1000f);
	}

	private static IntPoint WorldPosToIntPoint(WorldPos pos)
	{
		return new IntPoint(pos.x * 1000f, pos.y * 1000f);
	}

	public Lines GenerateSinglePolygon(Paths paths)
	{
		Clipper clipper = new Clipper();
		clipper.AddPaths(paths, PolyType.ptClip, closed: true);
		Lines lines = new Lines();
		Paths paths2 = new Paths();
		bool num = clipper.Execute(ClipType.ctUnion, paths2, PolyFillType.pftNonZero);
		clipper.Clear();
		if (num)
		{
			foreach (List<IntPoint> item in paths2)
			{
				List<WorldPos> list = PathToLine(item);
				if (list.Count > 0)
				{
					lines.Add(list);
				}
			}
		}
		lines.debug = paths.debug;
		return lines;
	}

	private List<WorldPos> PathToLine(List<IntPoint> path)
	{
		List<WorldPos> list = new List<WorldPos>(path.Count);
		int i = 0;
		for (int count = path.Count; i < count; i++)
		{
			list.Add(IntPointToWorld(path[i]));
		}
		RemoveTriangles(list);
		RemoveKinks(list);
		RemoveTriWedges(list);
		return list;
	}

	private void RemoveKinks(List<WorldPos> line)
	{
		for (int num = line.Count - 3; num >= 1; num--)
		{
			if (num <= line.Count - 3)
			{
				WorldPos worldPos = line[num];
				WorldPos worldPos2 = line[num + 1];
				WorldPos worldPos3 = line[num + 2];
				float magnitudeSquared = (worldPos - worldPos2).MagnitudeSquared;
				float magnitudeSquared2 = (worldPos2 - worldPos3).MagnitudeSquared;
				float magnitudeSquared3 = (worldPos - worldPos3).MagnitudeSquared;
				if (magnitudeSquared < 1f && magnitudeSquared2 < 1f && magnitudeSquared3 < 1f)
				{
					line.RemoveAt(num + 2);
					line.RemoveAt(num + 1);
					line[num] = WorldPos.Lerp(worldPos, worldPos3, 0.5f);
				}
			}
		}
	}

	private void RemoveTriWedges(List<WorldPos> line)
	{
		for (int num = line.Count - 3; num >= 1; num--)
		{
			WorldPos worldPos = line[num];
			WorldPos worldPos2 = line[num + 1];
			WorldPos worldPos3 = line[num + 2];
			float magnitudeSquared = (worldPos - worldPos2).MagnitudeSquared;
			float magnitudeSquared2 = (worldPos2 - worldPos3).MagnitudeSquared;
			float magnitudeSquared3 = (worldPos - worldPos3).MagnitudeSquared;
			if (magnitudeSquared3 < 3f && magnitudeSquared > magnitudeSquared3 && magnitudeSquared2 > magnitudeSquared3)
			{
				line.RemoveAt(num + 1);
			}
		}
	}

	private void RemoveQuadWedges(List<WorldPos> line)
	{
		for (int num = line.Count - 4; num >= 1; num--)
		{
			if (num <= line.Count - 4)
			{
				WorldPos worldPos = line[num];
				WorldPos worldPos2 = line[num + 1];
				WorldPos worldPos3 = line[num + 2];
				WorldPos worldPos4 = line[num + 3];
				float magnitudeSquared = (worldPos - worldPos2).MagnitudeSquared;
				float magnitudeSquared2 = (worldPos2 - worldPos3).MagnitudeSquared;
				float magnitudeSquared3 = (worldPos3 - worldPos4).MagnitudeSquared;
				float magnitudeSquared4 = (worldPos - worldPos4).MagnitudeSquared;
				if (magnitudeSquared4 < 2f && magnitudeSquared2 < 2f && magnitudeSquared > magnitudeSquared4 && magnitudeSquared3 > magnitudeSquared4)
				{
					line.RemoveAt(num + 3);
					line.RemoveAt(num + 2);
					line.RemoveAt(num + 1);
					line[num] = WorldPos.Lerp(worldPos, worldPos4, 0.5f);
				}
			}
		}
	}

	private void RemoveTriangles(List<WorldPos> line)
	{
		if (line.Count <= 3)
		{
			line.Clear();
		}
	}

	private List<IntPoint> MakePathFromRect(WorldSize size, GridTransform tr)
	{
		BoardConfig.GetFootprintTestPoints(size, tr, 1.2f, tmp_corners, includeInnerPoints: false);
		return MakePathFromCorners(tmp_corners);
	}

	private List<IntPoint> MakePathFromEntity(Entity e)
	{
		e.components.board.GetFootprintCorners(1.2f, tmp_corners);
		return MakePathFromCorners(tmp_corners);
	}

	private List<IntPoint> MakePathFromCorners(List<WorldPos> pts)
	{
		List<IntPoint> list = new List<IntPoint>(pts.Count);
		foreach (WorldPos pt in pts)
		{
			list.Add(WorldPosToIntPoint(pt));
		}
		return list;
	}

	public Paths GatherIndividualPolygons(List<Node> nodes)
	{
		Paths paths = new Paths();
		foreach (Node node in nodes)
		{
			PlayerID playerID = node.owner.Get();
			if (node.HasAnyTransit)
			{
				paths.Add(MakePathFromRect(new WorldSize(2f, 2f), node.Transform));
			}
			NodeEdgeID[] edges = node.edges;
			for (int i = 0; i < edges.Length; i++)
			{
				NodeEdge nodeEdge = edges[i].FindEdge();
				if (nodeEdge == null || nodeEdge.lotBeads == null || nodeEdge.IsTransitEmpty)
				{
					continue;
				}
				bool flag = nodeEdge.FindOtherNode(node).owner.Get() == playerID;
				List<LotBead> lotBeads = nodeEdge.lotBeads;
				if (lotBeads != null && lotBeads.Count >= 2)
				{
					LotBead lotBead = lotBeads[0];
					LotBead lotBead2 = lotBeads[lotBeads.Count - 1];
					WorldPos pos = lotBead.pos;
					WorldPos pos2 = lotBead2.pos;
					WorldPos b = WorldPos.Lerp(pos, pos2, 0.5f);
					float len = (pos - pos2).Magnitude / 2f - 1f;
					float rot = Vector2.SignedAngle((pos2 - pos).AsVector2, Vector2.up);
					if (flag)
					{
						WorldPos pos3 = WorldPos.Lerp(pos, pos2, 0.5f);
						float len2 = (pos - pos2).Magnitude - 2f;
						Stamp(paths, pos3, rot, len2);
					}
					if (!flag && lotBead.nodeOwner.Equals(node.id))
					{
						WorldPos worldPos = WorldPos.Lerp(pos, b, 0.5f);
						WorldPos pos4 = WorldPos.Lerp(pos, worldPos, 0.1f);
						Stamp(paths, worldPos, rot, len);
						Stamp(paths, pos4, rot, 2f);
					}
					if (!flag && lotBead2.nodeOwner.Equals(node.id))
					{
						WorldPos worldPos2 = WorldPos.Lerp(pos2, b, 0.5f);
						WorldPos pos5 = WorldPos.Lerp(pos2, worldPos2, 0.1f);
						Stamp(paths, worldPos2, rot, len);
						Stamp(paths, pos5, rot, 2f);
					}
				}
			}
			foreach (EntityID item in node.contained)
			{
				paths.Add(MakePathFromEntity(item.FindEntity()));
			}
		}
		return paths;
	}

	private void Stamp(Paths paths, WorldPos pos, float rot, float len)
	{
		GridTransform tr = new GridTransform(pos, rot);
		WorldSize size = new WorldSize(2f, len);
		paths.Add(MakePathFromRect(size, tr));
	}
}
