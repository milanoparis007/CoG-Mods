using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Heatmaps;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal class AssignLotsToZones
{
	public struct ResultEntry
	{
		public Entity entity;

		public float comscore;

		public float indscore;

		public float resscore;

		public bool IsNotValid => entity == null;
	}

	private class EntryComparerAscendingCom : Comparer<ResultEntry>
	{
		public override int Compare(ResultEntry x, ResultEntry y)
		{
			float num = x.comscore - y.comscore;
			if (!(num < 0f))
			{
				if (!(num > 0f))
				{
					return 0;
				}
				return 1;
			}
			return -1;
		}
	}

	private class EntryComparerAscendingInd : Comparer<ResultEntry>
	{
		public override int Compare(ResultEntry x, ResultEntry y)
		{
			float num = x.indscore - y.indscore;
			if (!(num < 0f))
			{
				if (!(num > 0f))
				{
					return 0;
				}
				return 1;
			}
			return -1;
		}
	}

	private class EntryComparerAscendingRes : Comparer<ResultEntry>
	{
		public override int Compare(ResultEntry x, ResultEntry y)
		{
			float num = x.resscore - y.resscore;
			if (!(num < 0f))
			{
				if (!(num > 0f))
				{
					return 0;
				}
				return 1;
			}
			return -1;
		}
	}

	private static readonly EntryComparerAscendingCom ENTRY_COMPARER_COM = new EntryComparerAscendingCom();

	private static readonly EntryComparerAscendingInd ENTRY_COMPARER_IND = new EntryComparerAscendingInd();

	private static readonly EntryComparerAscendingRes ENTRY_COMPARER_RES = new EntryComparerAscendingRes();

	private const int MIN_PER_FRAME = 100;

	private const int MAX_FRAMES = 5;

	private Xorshift _rng;

	private List<ResultEntry> _all = new List<ResultEntry>();

	private List<ResultEntry> _candidatesInd = new List<ResultEntry>();

	private List<ResultEntry> _candidatesCom = new List<ResultEntry>();

	private List<ResultEntry> _candidatesRes = new List<ResultEntry>();

	private SetupOrchestratorContext _ctx;

	public AssignLotsToZones(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	internal IEnumerator Start()
	{
		_rng = Game.ctx.scenario.MakeSeededRng<AssignLotsToZones>();
		_ctx.bizSetupData = GenerateBusinessData();
		_all = (from e in Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(TagConstants.TAG_EMPTY_LOT)
			where e.IsEnabled
			select new ResultEntry
			{
				entity = e
			}).ToList();
		UpdateCaches(_all);
		yield return AssignLots();
		yield return AssignRemainingLots();
	}

	private void UpdateCaches(List<ResultEntry> all)
	{
		for (int num = all.Count - 1; num >= 0; num--)
		{
			if (all[num].IsNotValid || all[num].entity.data.lot.zone != ZoneType.Unknown)
			{
				all.SwapRemoveAt(num);
			}
		}
		_candidatesCom.ClearAndAddRange(all);
		_candidatesInd.ClearAndAddRange(all);
		_candidatesRes.ClearAndAddRange(all);
		_candidatesCom.Sort(ENTRY_COMPARER_COM);
		_candidatesInd.Sort(ENTRY_COMPARER_IND);
		_candidatesRes.Sort(ENTRY_COMPARER_RES);
	}

	private IEnumerator AssignLots()
	{
		BusinessSetupData setup = _ctx.bizSetupData;
		int comPerFrame = setup.comTotal / 5;
		int indPerFrame = setup.indTotal / 5;
		int resPerFrame = Math.Max(comPerFrame, indPerFrame);
		SeedHeatmaps();
		while (_candidatesCom.Count > 0 && _candidatesInd.Count > 0 && setup.comToAssign > 0 && setup.indToAssign > 0)
		{
			yield return null;
			ScoreLists();
			UpdateCaches(_all);
			if (_all.Count != 0)
			{
				setup.indToAssign = AssignLotsOfType(_candidatesInd, ZoneType.Ind, indPerFrame, setup.indToAssign);
				setup.comToAssign = AssignLotsOfType(_candidatesCom, ZoneType.Com, comPerFrame, setup.comToAssign);
				AssignLotsOfType(_candidatesRes, ZoneType.Res, resPerFrame, int.MaxValue);
				UpdateHeatmaps();
				continue;
			}
			break;
		}
	}

	private IEnumerator AssignRemainingLots()
	{
		int interval = _all.Count / 5;
		interval = MathUtil.ClampMin(interval, 100);
		while (_all.Count > 0)
		{
			Entity entity = _all.RemoveLast().entity;
			AssignZone(entity, ZoneType.Res);
			if (_all.Count % interval == 0)
			{
				yield return null;
			}
		}
		if (Game.serv.globals.settings.general.debug.showDebugZoneColors)
		{
			while (true)
			{
				yield return null;
			}
		}
	}

	private int AssignLotsOfType(List<ResultEntry> lots, ZoneType zone, int thisFrame, int totalToProcess)
	{
		while (thisFrame > 0 && totalToProcess > 0 && lots.Count > 0)
		{
			ResultEntry resultEntry = lots.RemoveLastOrDefault();
			if (resultEntry.entity.data.lot.zone == ZoneType.Unknown)
			{
				AssignZone(resultEntry.entity, zone);
				totalToProcess--;
				thisFrame--;
			}
		}
		return totalToProcess;
	}

	private void AssignZone(Entity entity, ZoneType zone)
	{
		entity.data.lot.zone = zone;
		if (Game.serv.globals.settings.general.debug.showDebugZoneColors)
		{
			Color c = zone switch
			{
				ZoneType.Ind => Color.yellow, 
				ZoneType.Res => Color.green, 
				ZoneType.Com => Color.blue, 
				_ => Color.black, 
			};
			entity.components.model.DebugRecolorExpensive(c);
		}
	}

	private void SeedHeatmaps()
	{
		MapConfig.MapgenMoveInScores moveinScores = Game.ctx.session.mapconfig.moveinScores;
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.Rail, 1);
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.GroundRoadRail, 1);
		_ctx.lotPlacementData = new LotPlacementData();
		GenerateCenters(ref _ctx.lotPlacementData.commercialCenters, moveinScores.comInitialSeeds, TagConstants.TAG_COMMERCIAL);
		GenerateCenters(ref _ctx.lotPlacementData.industryCenters, moveinScores.indInitialSeeds, TagConstants.TAG_INDUSTRIAL);
		GenerateCenters(ref _ctx.lotPlacementData.residentialCenters, moveinScores.resInitialSeeds, TagConstants.TAG_RESIDENTIAL);
	}

	private void GenerateCenters(ref List<WorldPos> list, int count, Label tag)
	{
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		list = new List<WorldPos>();
		for (int i = 0; i < count; i++)
		{
			list.Add(new WorldPos(_rng.Generate(0, mapSize.width), _rng.Generate(0, mapSize.height)));
		}
		(Game.ctx.heatmaps.Find(HeatmapType.LotZone, tag) as LotZoneHeatmap).ClearAndSeed(list);
	}

	private void UpdateHeatmaps()
	{
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.LotZone, TagConstants.TAG_COMMERCIAL, 1);
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.LotZone, TagConstants.TAG_INDUSTRIAL, 1);
		Game.ctx.heatmaps.ManualUpdate(HeatmapType.LotZone, TagConstants.TAG_RESIDENTIAL, 1);
	}

	private void ScoreLists()
	{
		HeatmapManager heatmaps = Game.ctx.heatmaps;
		MapConfig.MapgenMoveInScores moveinScores = Game.ctx.session.mapconfig.moveinScores;
		Heatmap heatmap = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_RESIDENTIAL);
		Heatmap heatmap2 = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_COMMERCIAL);
		Heatmap heatmap3 = heatmaps.Find(HeatmapType.LotZone, TagConstants.TAG_INDUSTRIAL);
		Heatmap heatmap4 = heatmaps.Find(HeatmapType.Rail);
		for (int i = 0; i < _all.Count; i++)
		{
			Entity entity = _all[i].entity;
			if (entity.IsEnabled && entity.data.lot.zone == ZoneType.Unknown)
			{
				HeatmapPos pos = heatmap.WorldToHeatmap(entity.data.board.worldpos);
				float num = _rng.Generate(moveinScores.emptyLotBase);
				float valueFast = heatmap.GetValueFast(pos);
				float valueFast2 = heatmap2.GetValueFast(pos);
				float valueFast3 = heatmap3.GetValueFast(pos);
				float valueFast4 = heatmap4.GetValueFast(pos);
				float comscore = num + valueFast * moveinScores.comResidentialAreaBoost + valueFast2 * moveinScores.comCommercialAreaBoost + valueFast3 * moveinScores.comIndustrialAreaBoost + valueFast4 * moveinScores.comRailOrTerminalBoost;
				float indscore = num + valueFast * moveinScores.indResidentialAreaBoost + valueFast2 * moveinScores.indCommercialAreaBoost + valueFast3 * moveinScores.indIndustrialAreaBoost + valueFast4 * moveinScores.indRailOrTerminalBoost;
				float resscore = num + valueFast * moveinScores.resResidentialAreaBoost + valueFast2 * moveinScores.resCommercialAreaBoost + valueFast3 * moveinScores.resIndustrialAreaBoost + valueFast4 * moveinScores.resRailOrTerminalBoost;
				_all[i] = new ResultEntry
				{
					entity = entity,
					comscore = comscore,
					indscore = indscore,
					resscore = resscore
				};
			}
		}
	}

	private static BusinessSetupData GenerateBusinessData()
	{
		BusinessSetupData businessSetupData = new BusinessSetupData();
		businessSetupData.rng = Game.ctx.scenario.MakeSeededRng<BusinessSetupData>();
		businessSetupData.popTotal = Game.ctx.session.mapconfig.generator.totalInflux;
		businessSetupData.popToAssign = 0;
		VisitState visit = VisitState.MakeForBuilding(null);
		foreach (EntityConfig item in Game.ctx.entityman.GetCachedConfigsByComponentUnsafe<BizConfig>())
		{
			if (item.biz.popneeded <= 0)
			{
				continue;
			}
			VisitRequirementList reqsToInstall = item.biz.reqsToInstall;
			if (reqsToInstall == null || reqsToInstall.AllPass(visit))
			{
				int num = Mathf.CeilToInt((float)businessSetupData.popTotal / (float)item.biz.popneeded);
				businessSetupData.bizCountsByType.Add(new BusinessSetupData.BizEntry(item, item.biz.type, item.biz.movesinto, num));
				if (item.biz.type == ZoneType.Com)
				{
					businessSetupData.comTotal += num;
					businessSetupData.comToAssign += num;
				}
				if (item.biz.type == ZoneType.Ind)
				{
					businessSetupData.indTotal += num;
					businessSetupData.indToAssign += num;
				}
			}
		}
		GenerateInterestingNodeCounts(businessSetupData);
		return businessSetupData;
	}

	private static void GenerateInterestingNodeCounts(BusinessSetupData data)
	{
		float potentialNodeProbability = Game.ctx.session.mapconfig.moveinScores.potentialNodeProbability;
		List<int> list = new List<int>();
		List<float> list2 = new List<float>();
		FillProbabilities(list, list2);
		bool normalized = list2.Normalize();
		data.countsPerNode.Clear();
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			int desiredInteresting = data.rng.PickElement(list, list2, normalized);
			int desiredPotential = (data.rng.CheckProbability(potentialNodeProbability) ? 1 : 0);
			data.countsPerNode.Add(item.id, new BusinessSetupData.BizCounts
			{
				desiredInteresting = desiredInteresting,
				desiredPotential = desiredPotential
			});
		}
	}

	private static void FillProbabilities(List<int> counts, List<float> probs)
	{
		foreach (KeyValuePair<int, float> interestingNodeProbability in Game.ctx.session.mapconfig.moveinScores.interestingNodeProbabilities)
		{
			counts.Add(interestingNodeProbability.Key);
			probs.Add(interestingNodeProbability.Value);
		}
	}
}
