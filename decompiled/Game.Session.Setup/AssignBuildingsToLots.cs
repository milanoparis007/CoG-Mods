using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal class AssignBuildingsToLots
{
	private struct Streak
	{
		public int first;

		public int last;

		public Streak SetLast(int newlast)
		{
			return new Streak
			{
				first = first,
				last = newlast
			};
		}

		public bool HasEnough(int minLength)
		{
			return last - first + 1 >= minLength;
		}
	}

	public const int ITERATIONS_PER_FRAME = 1000;

	private Xorshift _rng;

	private List<Entity> _all;

	private Heatmap _appeal;

	private Heatmap _buildings;

	private MapConfig.GenClockConfig _genconfig;

	private int _countCorners;

	private int _countCenters;

	private const bool _showDebugCubes = false;

	private readonly ModelConfig.SlotOverride[] OVERRIDES_ROW_HOUSES = new ModelConfig.SlotOverride[2]
	{
		ModelConfig.SlotOverride.RowShort,
		ModelConfig.SlotOverride.RowTall
	};

	private readonly ModelConfig.SlotOverride[] OVERRIDES_BOARDWALK = new ModelConfig.SlotOverride[2]
	{
		ModelConfig.SlotOverride.BoardwalkBookendStart,
		ModelConfig.SlotOverride.BoardwalkBookendEnd
	};

	internal IEnumerator Start()
	{
		_genconfig = Game.ctx.session.mapconfig.generator;
		_rng = Game.ctx.scenario.MakeSeededRng<AssignBuildingsToLots>();
		_all = (from e in Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(TagConstants.TAG_EMPTY_LOT)
			where e.IsEnabled
			select e).ToList();
		_all.Sort((Entity a, Entity b) => b.Id.index - a.Id.index);
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.Appeal, 1);
		_appeal = Game.ctx.heatmaps.Find(HeatmapType.Appeal);
		_buildings = Game.ctx.heatmaps.Find(HeatmapType.Buildings);
		MarkLotsForBoardwalk();
		MarkLotsForDecos();
		int i = 0;
		while (_all.Count > 0)
		{
			Entity lot = _all.RemoveLast();
			Replace(lot);
			int num = i + 1;
			i = num;
			if (num % 1000 == 0)
			{
				yield return null;
			}
		}
		List<NodeEdge> allEdgesUnsafe = Game.ctx.board.nodes.GetAllEdgesUnsafe();
		foreach (NodeEdge item in allEdgesUnsafe)
		{
			MakeRowHouses(item, left: true);
			MakeRowHouses(item, left: false);
			MakeBoardwalkBookends(item, left: true);
			MakeBoardwalkBookends(item, left: false);
			int num = i + 1;
			i = num;
			if (num % 1000 == 0)
			{
				yield return null;
			}
		}
	}

	private void Replace(Entity lot)
	{
		LotData lot2 = lot.data.lot;
		LotConfig lot3 = lot.config.lot;
		float valueSafe = _appeal.GetValueSafe(lot.data.board.worldpos);
		Label name = Label.NULL;
		if (name.IsNotSet && lot2.saveForBoardwalk)
		{
			name = lot3.FindBoardwalk(lot, lot2.zone, _rng, valueSafe);
		}
		if (name.IsNotSet && lot2.saveForDeco)
		{
			name = lot3.FindLotDeco(lot, lot2.zone, _rng, valueSafe);
		}
		if (name.IsNotSet)
		{
			name = lot3.FindUpgrade(lot, lot2.zone, _rng, valueSafe);
		}
		if (name.IsNotSet)
		{
			Logger.Warning($"Lot upgrade not found for lot {lot2.zone} size {lot.config.board.lotsize}???");
			return;
		}
		EntityConfig newEntityConfig = Game.ctx.entityman.FindTemplate(name);
		Game.ctx.board.ReplaceEntityInPlace(lot, newEntityConfig);
	}

	private void MarkLotsForBoardwalk()
	{
		foreach (NodeEdge item in Game.ctx.board.nodes.GetAllEdgesUnsafe())
		{
			if (!item.IsPcgBoardwalkAny)
			{
				continue;
			}
			bool flag = item.pcgtype == ProcGenType.BoardwalkOnLeft;
			List<LotBead> lotBeads = item.lotBeads;
			for (int i = 0; i < lotBeads.Count; i++)
			{
				Entity entity = (flag ? lotBeads[i].leftId : lotBeads[i].rightId).FindEntity();
				LotData lotData = entity?.data.lot;
				if (lotData != null)
				{
					lotData.saveForBoardwalk = true;
					Game.serv.debugvis.AddCubeIf(pred: false, entity.data.board.worldpos, Color.cyan, 2f);
				}
			}
		}
	}

	private void MarkLotsForDecos()
	{
		if (_genconfig.decoNodes == null)
		{
			return;
		}
		List<Node> allNodes = _rng.ShuffleCopy(Game.ctx.board.nodes.GetAllNodesUnsafe());
		for (int i = 0; (float)i < _genconfig.decoNodes.centers; i++)
		{
			if (!StartMarkingLotsAt(allNodes))
			{
				break;
			}
		}
	}

	private bool StartMarkingLotsAt(List<Node> allNodes)
	{
		while (allNodes.Count > 0)
		{
			Node node = allNodes.SwapRemoveAt(0);
			if (ShouldMarkLotsAt(node) && TryMarkLotsAround(node, allNodes))
			{
				return true;
			}
		}
		return false;
	}

	private bool ShouldMarkLotsAt(Node node)
	{
		if (!node.HasRoad)
		{
			return false;
		}
		WorldPos pos = node.pos;
		float valueSafe = _appeal.GetValueSafe(pos);
		float valueSafe2 = _buildings.GetValueSafe(pos);
		if (valueSafe <= _genconfig.decoNodes.maxappeal)
		{
			return valueSafe2 <= _genconfig.decoNodes.maxdensity;
		}
		return false;
	}

	private bool TryMarkLotsAround(Node center, List<Node> allNodes)
	{
		int count = 0;
		bool flag = _appeal.GetValueSafe(center.pos) < _genconfig.lowAppealThreshold;
		int maxToMark = (flag ? _genconfig.decoNodes.spread.lowappeal : _genconfig.decoNodes.spread.@default);
		int maxNodes = maxToMark * 3;
		int num = Game.ctx.board.nodes.VisitNeighborhoodBFS(center, maxNodes, delegate(Node node)
		{
			if (TryMarkLotsAt(node))
			{
				count++;
			}
			allNodes.SwapRemove(node);
		}, (Node node) => ShouldMarkLotsAt(node), null, (Node node) => count >= maxToMark, onlyBizNodes: true);
		_countCorners += num;
		_countCenters += ((num > 0) ? 1 : 0);
		Game.serv.debugvis.AddCubeIf(pred: false, center.pos, Color.red, 2f);
		return num > 0;
	}

	private bool TryMarkLotsAt(Node node)
	{
		if (node.contained == null || node.contained.Count == 0)
		{
			return false;
		}
		float num = (float)((_appeal.GetValueSafe(node.pos) < _genconfig.lowAppealThreshold) ? _genconfig.decoNodes.lotpercent.lowappeal : _genconfig.decoNodes.lotpercent.@default) / 100f;
		int num2 = (int)((float)node.contained.Count * num);
		for (int i = 0; i < num2; i++)
		{
			LotData lotData = node.contained[i].FindEntity()?.data.lot;
			if (lotData != null)
			{
				lotData.saveForDeco = true;
			}
		}
		Game.serv.debugvis.AddCubeIf(pred: false, node.pos, Color.red, 1f);
		return true;
	}

	private void MakeRowHouses(NodeEdge edge, bool left)
	{
		if (edge?.lotBeads == null)
		{
			return;
		}
		using ListPool<Streak>.PooledBlockList pooledBlockList = ListPool<Streak>.Allocate();
		FindStreaks(edge, left, 3, OVERRIDES_ROW_HOUSES, pooledBlockList);
		foreach (Streak item in pooledBlockList)
		{
			ModelConfig.SlotOverride slotOverride = (MathUtil.IsEven(edge.neid.index) ? ModelConfig.SlotOverride.RowShort : ModelConfig.SlotOverride.RowTall);
			OverrideStreak(edge, left, item, slotOverride, slotOverride);
		}
	}

	private void MakeBoardwalkBookends(NodeEdge edge, bool left)
	{
		if (edge?.lotBeads == null)
		{
			return;
		}
		using ListPool<Streak>.PooledBlockList pooledBlockList = ListPool<Streak>.Allocate();
		FindStreaks(edge, left, 4, OVERRIDES_BOARDWALK, pooledBlockList);
		for (int i = 0; i < pooledBlockList.Count; i++)
		{
			Streak streak = pooledBlockList[i];
			OverrideStreak(edge, left, streak, ModelConfig.SlotOverride.BoardwalkBookendEnd, ModelConfig.SlotOverride.BoardwalkBookendStart);
		}
	}

	private void OverrideStreak(NodeEdge edge, bool left, Streak streak, ModelConfig.SlotOverride typeeven, ModelConfig.SlotOverride typeodd)
	{
		List<LotBead> lotBeads = edge.lotBeads;
		EntityID entityID = EntityID.INVALID;
		int num = 0;
		for (int i = streak.first; i <= streak.last; i++)
		{
			EntityID entityID2 = (left ? lotBeads[i].leftId : lotBeads[i].rightId);
			if (!(entityID2 == entityID))
			{
				ModelConfig.SlotOverride type = (MathUtil.IsEven(num) ? typeeven : typeodd);
				entityID2.FindEntity().components.model.UseModelOverride(type);
				entityID = entityID2;
				num++;
			}
		}
	}

	private void FindStreaks(NodeEdge edge, bool left, int minLength, ModelConfig.SlotOverride[] types, List<Streak> list)
	{
		Streak? current = null;
		List<LotBead> lotBeads = edge.lotBeads;
		for (int i = 0; i < lotBeads.Count; i++)
		{
			ModelComponent modelComponent = (left ? lotBeads[i].leftId : lotBeads[i].rightId).FindEntity()?.components.model;
			int num;
			if (modelComponent != null)
			{
				num = (modelComponent.CanUseModelOverride(types) ? 1 : 0);
				if (num != 0)
				{
					current = (current.HasValue ? current.Value.SetLast(i) : new Streak
					{
						first = i,
						last = i
					});
				}
			}
			else
			{
				num = 0;
			}
			if (num == 0)
			{
				MaybeStoreStreakAndReset();
			}
		}
		MaybeStoreStreakAndReset();
		void MaybeStoreStreakAndReset()
		{
			if (current.HasValue && current.Value.HasEnough(minLength))
			{
				list.Add(current.Value);
			}
			current = null;
		}
	}
}
