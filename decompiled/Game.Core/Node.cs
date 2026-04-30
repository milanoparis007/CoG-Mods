using System.Collections.Generic;
using System.Diagnostics;
using Game.Services.Maps;
using Game.Session.Data;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public sealed class Node
{
	public NodeID id;

	public WorldPos pos;

	public float deg;

	public Label cfg;

	public IntPos genOffset;

	public NodeProcGenData procData;

	public int roadIslandId = -1;

	public TransitFlags transit;

	public TerrainType terraintype;

	public List<EntityID> contained = new List<EntityID>(24);

	public List<EntityID> interesting = new List<EntityID>(4);

	public EntityID potential = EntityID.INVALID;

	public PlayerFlags known = new PlayerFlags();

	public PlayerSingleFlagWithHistory owner = new PlayerSingleFlagWithHistory();

	public Label maineth;

	public int population;

	public PrecinctID precinctId;

	public List<NodeDistrictData> districts;

	public RespectPerPlayer respect = new RespectPerPlayer();

	public HeatPerPlayer heat = new HeatPerPlayer();

	public NodeRaidInfo raidinfo;

	public NodeEdgeID[] edges = new NodeEdgeID[4];

	public bool HasValidRoadIsland => roadIslandId >= 0;

	public bool IsValid => id.IsValid;

	public GridTransform Transform => new GridTransform(pos, deg);

	public bool HasRoad => HasTransitType(TransitFlags.Road);

	public bool HasRail => HasTransitType(TransitFlags.Rail);

	public bool HasTerminal => HasTransitType(TransitFlags.Terminal);

	public bool HasAnyTransit => transit != TransitFlags.Empty;

	public bool HasNoTransit => transit == TransitFlags.Empty;

	public bool HasNoTransitAndOnGround
	{
		get
		{
			if (transit == TransitFlags.Empty)
			{
				return terraintype == TerrainType.Ground;
			}
			return false;
		}
	}

	public bool IsOnGround => terraintype == TerrainType.Ground;

	public bool IsOnWater => terraintype == TerrainType.Water;

	public bool IsOnMountain => terraintype == TerrainType.Mountain;

	public bool IsConnectionNode => procData.isConnection;

	public bool HasActiveRaid => GetRaidOrNull()?.HasActiveRaid ?? false;

	public bool IsInAnyDistrict
	{
		get
		{
			if (districts != null)
			{
				return districts.Count > 0;
			}
			return false;
		}
	}

	private string DebugString => $"NODE {id} {pos} {deg} deg, {terraintype}, {transit}";

	public Node()
	{
	}

	public Node(NodeID nid, MapNodesConfig cfg, GridTransform gridTransform)
	{
		id = nid;
		this.cfg = cfg.id;
		pos = gridTransform.pos;
		deg = gridTransform.deg;
		procData = default(NodeProcGenData);
		procData.railCost = cfg.railCost;
		procData.dontSkipRoads = cfg.dontSkipRoads;
		procData.forceBridges = cfg.forceBridges;
		procData.isConnection = cfg.connection;
	}

	public NodeEdgeID GetEdgeID(Direction dir)
	{
		return edges[(int)dir];
	}

	public bool HasTransitType(TransitFlags mask)
	{
		return (mask & transit) == mask;
	}

	public WorldPos GenerateDirectionDelta(Direction dir)
	{
		float facingRotation = DirectionUtil.GetFacingRotation(dir);
		return WorldPos.NORTH.Rotate(facingRotation + deg);
	}

	public NodeRaidInfo GetRaidOrNull()
	{
		return raidinfo;
	}

	public NodeRaidInfo GetRaidOrAdd()
	{
		return raidinfo ?? (raidinfo = new NodeRaidInfo());
	}

	public bool IsInMatchingDistrict(Label tag)
	{
		if (!IsInAnyDistrict)
		{
			return false;
		}
		foreach (NodeDistrictData district in districts)
		{
			TagList districtTags = district.GetDistrictTags();
			if (districtTags != null && districtTags.Contains(tag))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsInMatchingDistrict(TagList taglist)
	{
		if (!IsInAnyDistrict)
		{
			return false;
		}
		foreach (NodeDistrictData district in districts)
		{
			TagList districtTags = district.GetDistrictTags();
			if (districtTags != null && districtTags.ContainsAtLeastOneOf(taglist))
			{
				return true;
			}
		}
		return false;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
