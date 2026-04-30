using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.KB;

internal class PlayerKBCache
{
	public abstract class EntryList
	{
		public List<KBResult> data = new List<KBResult>();

		public int IndexOf(Label queryId)
		{
			int i = 0;
			for (int count = data.Count; i < count; i++)
			{
				if (data[i].queryId == queryId)
				{
					return i;
				}
			}
			return -1;
		}

		public KBResult Get(Label queryId)
		{
			int num = IndexOf(queryId);
			if (num < 0)
			{
				return default(KBResult);
			}
			return data[num];
		}

		public bool Contains(Label queryId)
		{
			return IndexOf(queryId) >= 0;
		}

		public bool Remove(Label queryId)
		{
			int num = IndexOf(queryId);
			if (num >= 0)
			{
				data.RemoveAt(num);
			}
			return num >= 0;
		}

		public void Upsert(KBResult status)
		{
			Remove(status.queryId);
			data.Add(status);
		}

		public void Clear()
		{
			data.Clear();
		}
	}

	public sealed class BuildingEntries : EntryList
	{
		public EntityID buildingId;

		public BuildingEntries(EntityID buildingId)
		{
			this.buildingId = buildingId;
		}

		public NodeID GetNodeID()
		{
			return buildingId.FindEntity().components.board.GetNodeID();
		}
	}

	public sealed class CornerEntries : EntryList
	{
		public NodeID nodeId;

		public CornerEntries(NodeID nodeId)
		{
			this.nodeId = nodeId;
		}
	}

	public Dictionary<EntityID, BuildingEntries> buildingData = new Dictionary<EntityID, BuildingEntries>(new EntityIDEqualityComparer());

	public Dictionary<NodeID, CornerEntries> cornerData = new Dictionary<NodeID, CornerEntries>(new NodeIDEqualityComparer());

	public void ClearAll()
	{
		buildingData.Clear();
		cornerData.Clear();
	}

	public BuildingEntries GetEntriesForBuilding(EntityID buildingId)
	{
		BuildingEntries buildingEntries = buildingData.FindOrNull(buildingId);
		if (buildingEntries == null)
		{
			BuildingEntries buildingEntries2 = (buildingData[buildingId] = new BuildingEntries(buildingId));
			buildingEntries = buildingEntries2;
		}
		return buildingEntries;
	}

	public CornerEntries GetEntriesForCorner(NodeID nodeId)
	{
		CornerEntries cornerEntries = cornerData.FindOrNull(nodeId);
		if (cornerEntries == null)
		{
			CornerEntries cornerEntries2 = (cornerData[nodeId] = new CornerEntries(nodeId));
			cornerEntries = cornerEntries2;
		}
		return cornerEntries;
	}

	public void ClearEntriesForBuildingIfExist(EntityID buildingId)
	{
		buildingData.FindOrNull(buildingId)?.Clear();
	}
}
