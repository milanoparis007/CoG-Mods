using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreateEmptyLots
{
	private enum Stage
	{
		Default,
		Police,
		TrainStation,
		Civics
	}

	private const int ITERATIONS_PER_FRAME = 200;

	private const bool allowFastPath = true;

	private BoardManager _board;

	private NodeManager _nodes;

	private Heatmap _heatmap;

	private SetupOrchestratorContext _ctx;

	private MapBoardConfig _mapconfig;

	private MapConfig.GenClockConfig _genconfig;

	private MapConfig.GenClockConfig.Specials.DefStates _specials;

	private IRandom _rng;

	private const int ROAD_MARGIN_FROM_EDGE = 10;

	public CreateEmptyLots(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
		_rng = Game.ctx.scenario.MakeSeededRng<CreateEmptyLots>();
	}

	internal IEnumerator Start()
	{
		_board = Game.ctx.board;
		_nodes = _board.nodes;
		_heatmap = Game.ctx.heatmaps.Find(HeatmapType.Buildings);
		_genconfig = Game.ctx.session.mapconfig.generator;
		_mapconfig = Game.ctx.session.mapconfig.map;
		_specials = _genconfig.specialBuildings.GenerateStatesForCivics(_rng);
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.Buildings, 5);
		using (new BlockStopwatch("procgen", "Updating edges"))
		{
			foreach (NodeEdge item in Game.ctx.board.nodes.GetAllEdgesUnsafe())
			{
				SetPCGFlags(item);
			}
		}
		using (new BlockStopwatch("procgen", "Generating lots"))
		{
			List<NodeEdge> edgesForCops = _ctx.copStationData.MakeEdges();
			yield return MakeLotsAxisAligned(edgesForCops, vertical: true, Stage.Police);
			yield return MakeLotsAxisAligned(edgesForCops, vertical: false, Stage.Police);
			List<NodeEdge> edgesForTrains = _ctx.trainStationData.MakeEdges();
			yield return MakeLotsAxisAligned(edgesForTrains, vertical: true, Stage.TrainStation);
			yield return MakeLotsAxisAligned(edgesForTrains, vertical: false, Stage.TrainStation);
			List<NodeEdge> edgesAll = _nodes.GetAllEdgesUnsafe();
			yield return MakeLotsAxisAligned(edgesAll, vertical: true, Stage.Civics);
			yield return MakeLotsAxisAligned(edgesAll, vertical: false, Stage.Civics);
			yield return MakeLotsAxisAligned(edgesAll, vertical: true, Stage.Default);
			yield return MakeLotsAxisAligned(edgesAll, vertical: false, Stage.Default);
		}
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.GroundBuildings, 2);
	}

	private void SetPCGFlags(NodeEdge edge)
	{
		Node node = edge.a.FindNode();
		Node node2 = edge.b.FindNode();
		if (node == null || node2 == null)
		{
			return;
		}
		MapNodesConfig nodesConfigByID = _mapconfig.GetNodesConfigByID(node.cfg);
		MapNodesConfig nodesConfigByID2 = _mapconfig.GetNodesConfigByID(node2.cfg);
		if (nodesConfigByID != null && nodesConfigByID2 != null)
		{
			if (nodesConfigByID.connection && nodesConfigByID2.connection)
			{
				edge.pcgtype = ProcGenType.Connection;
			}
			else if (nodesConfigByID.boardwalk && nodesConfigByID2.boardwalk && nodesConfigByID == nodesConfigByID2)
			{
				bool flag = edge.abDir == Direction.S || edge.abDir == Direction.W;
				edge.pcgtype = (flag ? ProcGenType.BoardwalkOnLeft : ProcGenType.BoardwalkOnRight);
			}
		}
	}

	private IEnumerator MakeLotsAxisAligned(List<NodeEdge> edges, bool vertical, Stage stage)
	{
		int i = 0;
		IntSize map = _board.MapConfig.mapSize;
		foreach (NodeEdge edge in edges)
		{
			if (CanAddLots(map, edge, stage, vertical))
			{
				MakeLotsAlongEdge(edge, stage);
				int num = i + 1;
				i = num;
				if (num % 200 == 0)
				{
					yield return null;
				}
			}
		}
	}

	private bool CanAddLots(IntSize map, NodeEdge edge, Stage stage, bool vertical)
	{
		if (!edge.IsRoad || edge.IsVertical != vertical)
		{
			return false;
		}
		if (IsSafeFromMargin(edge.a) && IsSafeFromMargin(edge.b))
		{
			return !edge.IsPcgConnection;
		}
		return false;
		bool IsSafeFromMargin(NodeID nodeId)
		{
			WorldPos pos = nodeId.FindNode().pos;
			int num = 10;
			int num2 = map.width - 10;
			int num3 = 10;
			int num4 = map.height - 10;
			if (pos.x > (float)num && pos.x < (float)num2 && pos.y > (float)num3)
			{
				return pos.y < (float)num4;
			}
			return false;
		}
	}

	private void MakeLotsAlongEdge(NodeEdge edge, Stage stage)
	{
		bool flag = stage == Stage.Police || stage == Stage.TrainStation;
		bool isVertical = edge.IsVertical;
		int i = 0;
		for (int num = edge.lotBeads.Count - 1; i <= num; i++)
		{
			if (isVertical ? (i != num) : ((byte)i != 0))
			{
				bool flag2 = TryAddSpecialBuildingOrLot(edge, i, left: false, stage);
				if (flag && flag2)
				{
					return;
				}
			}
		}
		int num2 = edge.lotBeads.Count - 1;
		for (int num3 = num2; num3 >= 0; num3--)
		{
			if (isVertical ? ((byte)num3 != 0) : (num3 != num2))
			{
				bool flag3 = TryAddSpecialBuildingOrLot(edge, num3, left: true, stage);
				if (flag && flag3)
				{
					break;
				}
			}
		}
	}

	private bool TryAddSpecialBuildingOrLot(NodeEdge edge, int i, bool left, Stage stage)
	{
		bool num = stage == Stage.Police || stage == Stage.Default;
		bool flag = stage == Stage.TrainStation || stage == Stage.Default;
		bool flag2 = stage == Stage.Civics || stage == Stage.Default;
		bool flag3 = stage == Stage.Default;
		if ((!num || !TryAddPolice(edge, i, left)) && (!flag || !TryAddTrain(edge, i, left)) && (!flag2 || !TryAddCivic(edge, i, left)))
		{
			if (flag3)
			{
				return TryAddLot(edge, i, left);
			}
			return false;
		}
		return true;
	}

	private bool TryAddTrain(NodeEdge edge, int i, bool left)
	{
		return TryAddSpecialStageHelper(_ctx.trainStationData, _genconfig.specialBuildings.trainStation, edge, i, left);
	}

	private bool TryAddPolice(NodeEdge edge, int i, bool left)
	{
		return TryAddSpecialStageHelper(_ctx.copStationData, _genconfig.specialBuildings.policeStation, edge, i, left);
	}

	private bool TryAddSpecialStageHelper(SpecialBuildingLocationData data, MapConfig.GenClockConfig.Specials.Def def, NodeEdge edge, int i, bool left)
	{
		if (edge.HasBoardwalkOnSide(left))
		{
			return false;
		}
		WorldPos pos = edge.lotBeads[i].pos;
		SpecialBuildingLocationData.Unplaced unplaced = data.FindClosestUnplaced(pos);
		if (unplaced == null)
		{
			return false;
		}
		if (def == null)
		{
			return false;
		}
		Entity entity = TryAddSpecialHelper(edge, i, left, def);
		if (entity != null)
		{
			data.unplaced.Remove(unplaced);
			data.placed.Add(entity);
		}
		return entity != null;
	}

	private bool TryAddCivic(NodeEdge edge, int i, bool left)
	{
		int num = _rng.Generate(0, _specials.Count);
		if (edge.HasBoardwalkOnSide(left))
		{
			return false;
		}
		for (int j = num; j < num + _specials.Count; j++)
		{
			MapConfig.GenClockConfig.Specials.State modulus = _specials.GetModulus(j);
			if (--modulus.leftToSkip <= 0 && TryAddSpecialHelper(edge, i, left, modulus.def, modulus) != null)
			{
				modulus.ResetLeftToSkip(_rng);
				return true;
			}
		}
		return false;
	}

	private Entity TryAddSpecialHelper(NodeEdge edge, int i, bool left, MapConfig.GenClockConfig.Specials.Def def, MapConfig.GenClockConfig.Specials.State state = null)
	{
		List<Label> configs = def.configs;
		LotBead bead = edge.lotBeads[i];
		bool flag = false;
		Node node = null;
		if (def.districtTags != null && def.districtTags.Count > 0)
		{
			(node, flag) = VerifyDistrictTags(bead, def.districtTags);
			if (!flag)
			{
				return null;
			}
		}
		bool flag2 = flag && state != null && def.maxPerDistrict > 0;
		if (flag2 && !state.DoesPassMaxPerDistrict(node, def))
		{
			return null;
		}
		Label name = _rng.PickElement(configs);
		EntityConfig config = Game.ctx.entityman.FindTemplate(name);
		Entity entity = TryAdd(edge, bead, i, left, config);
		if (flag2 && entity != null)
		{
			state.IncrementCountPerDistrict(node);
		}
		return entity;
	}

	private (Node node, bool match) VerifyDistrictTags(LotBead bead, TagList districtTags)
	{
		Node node = bead.nodeOwner.FindNode();
		return (node: node, match: node.IsInMatchingDistrict(districtTags));
	}

	private bool TryAddLot(NodeEdge edge, int i, bool left)
	{
		LotBead lotBead = edge.lotBeads[i];
		float valueSafe = _heatmap.GetValueSafe(lotBead.pos);
		int j = 0;
		for (int count = _genconfig.lots.Count; j < count; j++)
		{
			MapConfig.GenClockConfig.LotInfo lotInfo = _genconfig.lots[j];
			if ((!lotInfo.skipOnBoardwalk || !edge.HasBoardwalkOnSide(left)) && (!(lotInfo.probfail > 0f) || j >= count - 1 || !_rng.CheckProbability(lotInfo.probfail)) && valueSafe >= lotInfo.mindensity)
			{
				EntityConfig config = Game.ctx.entityman.FindTemplate(lotInfo.lot);
				if (TryAdd(edge, lotBead, i, left, config) != null)
				{
					return true;
				}
			}
		}
		return false;
	}

	private Entity TryAdd(NodeEdge edge, LotBead bead, int i, bool left, EntityConfig config)
	{
		if (!Game.ctx.board.nodes.CanEntityAttachToBeads(config, edge, i, left))
		{
			return null;
		}
		if (!config.board.canBeStretchedToSize)
		{
			return TryAddUsingFastPath(edge, bead, left, config);
		}
		return TryAddUsingDefaultPath(edge, bead, left, config);
	}

	private Entity TryAddUsingFastPath(NodeEdge edge, LotBead bead, bool left, EntityConfig config)
	{
		WorldSize lotsize = config.board.lotsize;
		GridTransform tr = BoardComponent.FindPositionWhenAttachedToBead(lotsize, edge, bead, left);
		if (config.board == null)
		{
			return null;
		}
		if (_board.DoesEntityIntersectAnyEntity(lotsize, tr))
		{
			return null;
		}
		if (_board.DoesEntityIntersectAnyTerrain(lotsize, tr))
		{
			return null;
		}
		Entity entity = Game.ctx.board.CreateEntityAtStartup(config, bead.Transform);
		if (!entity.components.board.AttachAndMoveToBead(edge, bead, left))
		{
			Game.ctx.board.DestroyEntity(entity, shutdown: false);
			return null;
		}
		return entity;
	}

	private Entity TryAddUsingDefaultPath(NodeEdge edge, LotBead bead, bool left, EntityConfig config)
	{
		Entity entity = Game.ctx.board.CreateEntityAtStartup(config, bead.Transform);
		if (!entity.components.board.AttachAndMoveToBead(edge, bead, left) || _board.DoesEntityIntersectAnyEntity(entity) || _board.DoesEntityIntersectAnyTerrain(entity))
		{
			Game.ctx.board.DestroyEntity(entity, shutdown: false);
			return null;
		}
		return entity;
	}
}
