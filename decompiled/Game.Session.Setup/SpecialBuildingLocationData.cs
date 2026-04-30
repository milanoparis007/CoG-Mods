using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Setup;

internal sealed class SpecialBuildingLocationData
{
	public sealed class Unplaced
	{
		public WorldPos center;

		public Node node;

		public List<NodeEdge> edges;

		public Unplaced(Node node)
		{
			this.node = node;
			center = node.pos;
			edges = (from eid in node.edges
				select eid.FindEdge() into e
				where e != null
				select e).ToList();
		}

		public bool IsNearTo(WorldPos pos)
		{
			return (center - pos).MagnitudeSquared <= 900f;
		}
	}

	public const float NEARBY_DIST_SQ = 900f;

	public readonly List<Unplaced> unplaced;

	public readonly List<Entity> placed;

	public readonly IRandom rng;

	public SpecialBuildingLocationData(List<Node> nodes)
	{
		unplaced = nodes.SelectIntoNewList((Node node) => new Unplaced(node));
		placed = new List<Entity>();
		rng = Game.ctx.scenario.MakeSeededRng(GetType());
	}

	public Unplaced FindClosestUnplaced(WorldPos pos)
	{
		float num = float.MaxValue;
		Unplaced result = null;
		foreach (Unplaced item in unplaced)
		{
			if (item.IsNearTo(pos))
			{
				float magnitudeSquared = (pos - item.center).MagnitudeSquared;
				if (magnitudeSquared < num)
				{
					result = item;
					num = magnitudeSquared;
				}
			}
		}
		return result;
	}

	internal List<NodeEdge> MakeEdges()
	{
		List<NodeEdge> list = new List<NodeEdge>();
		for (int i = 0; i < 4; i++)
		{
			foreach (Unplaced item in unplaced)
			{
				if (item.edges.Count > 0)
				{
					list.Add(item.edges.RemoveLast());
				}
			}
		}
		return list;
	}
}
