using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class AssignBusinessesToBuildings
{
	internal class CandidatesByBuilding
	{
		public List<EntityConfig> configs = new List<EntityConfig>();

		public List<float> weights = new List<float>();

		public void Add(EntityConfig config, float weight)
		{
			configs.Add(config);
			weights.Add(weight);
		}
	}

	internal class Candidates : LabelDictionary<CandidatesByBuilding>
	{
	}

	private class NodeEntry
	{
		public Node node;

		public int maxInterestingLeft;

		public int maxPotentialLeft;

		public List<Entity> buildings;

		public List<Entity> businesses;

		public NodeEntry(Node node)
		{
			this.node = node;
		}
	}

	private struct CornerEntry
	{
		public Entity building;

		public Entity biz;

		public float distance;
	}

	public const int ITERATIONS_PER_FRAME = 1000;

	private SetupOrchestratorContext _ctx;

	private static VisitState _s_visit = VisitState.MakeForBuilding(null);

	private const string TRAIN_STATION_PASSENGER = "train-station-passenger";

	private const string TRAIN_STATION_FREIGHT = "train-station-freight";

	private static readonly List<string> FORCED_BIZZES = new List<string> { "train-station-freight", "train-station-passenger" };

	public AssignBusinessesToBuildings(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	internal IEnumerator Start()
	{
		Candidates candidates = GenerateBusinessCandidates();
		List<NodeEntry> nodes = GenerateNodeLists();
		int i = 0;
		foreach (NodeEntry item in nodes)
		{
			AttachBusinessesToNodes(item, candidates);
			int num = i + 1;
			i = num;
			if (num % 1000 == 0)
			{
				yield return null;
			}
		}
		foreach (NodeEntry item2 in nodes)
		{
			SetInterestingBuildingsForNode(item2);
		}
		foreach (NodeEntry item3 in nodes)
		{
			SetResidentialEventHosts(item3);
		}
		MaybeProduceDebugDocs();
	}

	private Candidates GenerateBusinessCandidates()
	{
		Candidates candidates = new Candidates();
		foreach (BusinessSetupData.BizEntry item in _ctx.bizSetupData.bizCountsByType)
		{
			if (!candidates.TryGetValue(item.movesinto, out var value))
			{
				CandidatesByBuilding candidatesByBuilding = (candidates[item.movesinto] = new CandidatesByBuilding());
				value = candidatesByBuilding;
			}
			value.Add(item.config, item.count);
		}
		return candidates;
	}

	private List<NodeEntry> GenerateNodeLists()
	{
		List<NodeEntry> list = new List<NodeEntry>();
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			NodeEntry nodeEntry = new NodeEntry(item);
			nodeEntry.buildings = item.contained.Select((EntityID id) => id.FindEntity()).ToList();
			nodeEntry.businesses = new List<Entity>();
			int count = nodeEntry.buildings.Count;
			BusinessSetupData.BizCounts valueOrDefault = _ctx.bizSetupData.countsPerNode.FindOrNullable(item.id).GetValueOrDefault();
			nodeEntry.maxInterestingLeft = Math.Min(valueOrDefault.desiredInteresting, count);
			count -= nodeEntry.maxInterestingLeft;
			nodeEntry.maxPotentialLeft = Math.Min(valueOrDefault.desiredPotential, count);
			count -= nodeEntry.maxPotentialLeft;
			list.Add(nodeEntry);
		}
		return list;
	}

	private static bool IsType(Entity e, ZoneType type)
	{
		BuildingConfig building = e.config.building;
		if (building == null)
		{
			return false;
		}
		return building.type == type;
	}

	private void AttachBusinessesToNodes(NodeEntry node, Candidates candidates)
	{
		foreach (Entity building in node.buildings)
		{
			AddBusinessToBuilding(node, building, candidates);
		}
	}

	private void AddBusinessToBuilding(NodeEntry entry, Entity building, Candidates candidates)
	{
		CandidatesByBuilding candidatesByBuilding = candidates.FindOrNull(building.config.Template);
		if (candidatesByBuilding == null || candidatesByBuilding.configs == null || candidatesByBuilding.configs.Count == 0)
		{
			if (building.config.building.type != ZoneType.Unknown && building.config.residence == null)
			{
				Logger.Warning("Cannot find businesses that fit building template " + building.config.Template.ToString());
			}
		}
		else
		{
			EntityConfig config = FindRandom(building, candidatesByBuilding);
			Entity entity = Game.ctx.simman.businesses.CreateBusinessUnattached(config);
			Game.ctx.simman.businesses.AttachBusinessToBuilding(entity, building);
			entry.businesses.Add(entity);
			_ = building.components.modules.inventory;
		}
	}

	private EntityConfig FindRandom(Entity building, CandidatesByBuilding biztypes)
	{
		int count = biztypes.configs.Count;
		int num = _ctx.bizSetupData.rng.PickIndex(biztypes.configs, biztypes.weights, normalized: false);
		for (int i = 0; i < count; i++)
		{
			int index = (num + i) % count;
			EntityConfig entityConfig = biztypes.configs[index];
			if (CanAttachBiz(entityConfig, building))
			{
				return entityConfig;
			}
		}
		return null;
	}

	private bool CanAttachBiz(EntityConfig bizconfig, Entity building)
	{
		if (bizconfig.biz.reqsToAttachToBuilding == null)
		{
			return true;
		}
		_s_visit.building = building;
		return bizconfig.biz.reqsToAttachToBuilding.AllPass(_s_visit);
	}

	private void SetInterestingBuildingsForNode(NodeEntry entry)
	{
		SimTime now = Game.ctx.clock.Now;
		WorldPos pos = entry.node.pos;
		using ListPool<CornerEntry>.PooledBlockList pooledBlockList = ListPool<CornerEntry>.Allocate();
		foreach (Entity business in entry.businesses)
		{
			Entity entity = BuildingUtil.FindBuildingForBiz(business);
			float distance = ((entity != null) ? (entity.data.board.worldpos - pos).Magnitude : 10000f);
			pooledBlockList.Add(new CornerEntry
			{
				biz = business,
				building = entity,
				distance = distance
			});
		}
		_ctx.bizSetupData.rng.Shuffle(pooledBlockList);
		pooledBlockList.StableSort((CornerEntry x, CornerEntry y) => (int)(10f * (x.distance - y.distance)));
		using ListPool<EntityConfig>.PooledBlockList interestingByType = ListPool<EntityConfig>.Allocate();
		foreach (CornerEntry item in pooledBlockList)
		{
			if (item.building != null && item.building.components.modules.HasInterestingModule())
			{
				SetBuildingStatus(entry, item.building, item.biz, now, interestingByType);
			}
		}
	}

	private void SetResidentialEventHosts(NodeEntry node)
	{
		using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
		foreach (Entity building in node.buildings)
		{
			if (building.components.residence != null)
			{
				pooledBlockList.Add(building);
			}
		}
		if (pooledBlockList.Count > 0)
		{
			_ctx.bizSetupData.rng.Shuffle(pooledBlockList);
			pooledBlockList.FirstOrDefaultFast().components.residence.MarkAsEventHostingSpace();
		}
	}

	private void SetBuildingStatus(NodeEntry entry, Entity building, Entity biz, SimTime time, List<EntityConfig> interestingByType)
	{
		bool flag = false;
		bool flag2 = interestingByType.Contains(biz.config);
		if (!flag && FORCED_BIZZES.Contains(biz.config.Template.String) && !Game.ctx.tutorial.IsPlayingTutorialCity)
		{
			building.components.modules.SetEnabledOn(time);
			Game.ctx.board.SetInteresting(building, entry.node);
			interestingByType.Add(biz.config);
			flag = true;
		}
		if (!flag && entry.maxInterestingLeft > 0 && !flag2)
		{
			building.components.modules.SetEnabledOn(time);
			Game.ctx.board.SetInteresting(building, entry.node);
			interestingByType.Add(biz.config);
			entry.maxInterestingLeft--;
			flag = true;
		}
		if (!flag && entry.maxPotentialLeft > 0)
		{
			building.components.modules.SetEnabledOn(SimTime.MAX_DATE);
			Game.ctx.board.SetPotential(building, entry.node);
			entry.maxPotentialLeft--;
			flag = true;
		}
		if (!flag)
		{
			building.components.modules.SetEnabledOn(SimTime.MAX_DATE);
		}
	}

	private void MaybeProduceDebugDocs()
	{
	}
}
