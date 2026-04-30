using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.Services;
using Game.Services.Maps;
using Game.Session.Entities;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class ResidenceTracker : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider
{
	internal class MoveInCandidateTracker
	{
		public struct CandidateEntry
		{
			public Entity entity;

			public float baseScore;
		}

		public struct ResultEntry
		{
			public Entity entity;

			public float score;
		}

		private class EntryComparerAscending : Comparer<ResultEntry>
		{
			public override int Compare(ResultEntry x, ResultEntry y)
			{
				float num = x.score - y.score;
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

		private static readonly EntryComparerAscending ENTRY_COMPARER = new EntryComparerAscending();

		private List<Label> _ethnicities;

		private Dictionary<Label, List<ResultEntry>> _entries;

		private List<CandidateEntry> _housecache;

		private List<Entity> tmp_entities = new List<Entity>(1024);

		public void Initialize()
		{
			_ethnicities = Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted();
			_entries = new Dictionary<Label, List<ResultEntry>>(new LabelEqualityComparer());
			foreach (Label ethnicity in _ethnicities)
			{
				_entries.Add(ethnicity, new List<ResultEntry>(1024));
			}
			_housecache = new List<CandidateEntry>(16384);
		}

		public void Release()
		{
			_housecache.Clear();
			_entries.ClearDeep();
		}

		public void ResetCaches()
		{
			_housecache.Clear();
			foreach (List<ResultEntry> value in _entries.Values)
			{
				value.Clear();
			}
		}

		public ResultEntry GetBestResidence(Label eth)
		{
			List<ResultEntry> list = _entries.FindOrNull(eth);
			int i = 0;
			for (int count = list.Count; i < count; i++)
			{
				ResultEntry result = list.RemoveLastOrDefault();
				if (result.entity.components.residence.HasEmptyApartments())
				{
					return result;
				}
			}
			return default(ResultEntry);
		}

		public void CacheResidenceData(IRandom rng)
		{
			ResetCaches();
			CacheLotsAndHouses(rng);
			foreach (Label ethnicity in _ethnicities)
			{
				CachePotentialResidencesForEth(rng, ethnicity);
			}
		}

		private void CacheLotsAndHouses(IRandom rng)
		{
			PrescoreCandidates(rng, TagConstants.TAG_RESIDENTIAL, _housecache);
		}

		private void PrescoreCandidates(IRandom rng, Label tag, List<CandidateEntry> outlist)
		{
			Heatmap heatmap = Game.ctx.heatmaps.Find(HeatmapType.Density, TagConstants.TAG_RESIDENTIAL);
			Heatmap heatmap2 = Game.ctx.heatmaps.Find(HeatmapType.Density, TagConstants.TAG_COMMERCIAL);
			Heatmap heatmap3 = Game.ctx.heatmaps.Find(HeatmapType.Density, TagConstants.TAG_INDUSTRIAL);
			Heatmap heatmap4 = Game.ctx.heatmaps.Find(HeatmapType.Rail);
			MapConfig.MapgenMoveInScores moveinScores = Game.ctx.session.mapconfig.moveinScores;
			tmp_entities.ClearAndAddRange(Game.ctx.entityman.GetCachedEntitiesByTagUnsafe(tag));
			tmp_entities.Sort(Entity.Comparison);
			foreach (Entity tmp_entity in tmp_entities)
			{
				if (tmp_entity.IsEnabled && (tmp_entity.components.residence == null || !tmp_entity.components.residence.HasNoMoreApartments()))
				{
					float num = rng.Generate(moveinScores.emptyLotBase);
					WorldPos worldpos = tmp_entity.data.board.worldpos;
					float valueFast = heatmap.GetValueFast(worldpos);
					float valueFast2 = heatmap3.GetValueFast(worldpos);
					float valueFast3 = heatmap2.GetValueFast(worldpos);
					float valueFast4 = heatmap4.GetValueFast(worldpos);
					float baseScore = num + valueFast * moveinScores.resResidentialAreaBoost + valueFast3 * moveinScores.resCommercialAreaBoost + valueFast2 * moveinScores.resIndustrialAreaBoost + valueFast4 * moveinScores.resRailOrTerminalBoost;
					outlist.Add(new CandidateEntry
					{
						entity = tmp_entity,
						baseScore = baseScore
					});
				}
			}
			tmp_entities.Clear();
		}

		private void CachePotentialResidencesForEth(IRandom _, Label eth)
		{
			Heatmap heatmap = Game.ctx.heatmaps.FindEthnicityMap(eth);
			float resSameEthnicityBoost = Game.ctx.session.mapconfig.moveinScores.resSameEthnicityBoost;
			List<ResultEntry> list = _entries[eth];
			foreach (CandidateEntry item in _housecache)
			{
				float valueFast = heatmap.GetValueFast(item.entity.data.board.worldpos);
				float score = item.baseScore + valueFast * resSameEthnicityBoost;
				list.Add(new ResultEntry
				{
					entity = item.entity,
					score = score
				});
			}
			list.Sort(ENTRY_COMPARER);
		}
	}

	private const int MAX_PEOPLE_PER_GROUP = 5;

	public ResidenceTrackerPersistedData data;

	private MoveInCandidateTracker _moveins;

	public void Initialize(SimulationManager manager)
	{
		_moveins = new MoveInCandidateTracker();
		_moveins.Initialize();
		data = new ResidenceTrackerPersistedData();
	}

	public void Release()
	{
		_moveins.Release();
		_moveins = null;
	}

	public void OnSystemTurn()
	{
		int daysPerTurn = Game.ctx.clock.State.daysPerTurn;
		int num = DailyImmigration(daysPerTurn);
		data.population += num;
	}

	private int DailyImmigration(float days)
	{
		_moveins.CacheResidenceData(data.rng);
		float num = days / 365f;
		IntRange procGenYears = Game.serv.globals.settings.general.generator.procGenYears;
		int num2 = procGenYears.to - procGenYears.from;
		float num3 = (float)Game.ctx.session.mapconfig.generator.totalInflux / (float)num2;
		int count = MathUtil.RoundProbabilistic(num * num3, data.rng);
		int result = DailyImmigrationHelper(count);
		_moveins.ResetCaches();
		return result;
	}

	private int DailyImmigrationHelper(int count)
	{
		List<Label> ethnicitiesAllSorted = Game.ctx.session.mapconfig.GetEthnicitiesAllSorted();
		int num = count;
		while (num > 0)
		{
			Label eth = data.rng.PickElement(ethnicitiesAllSorted);
			Entity entity = FindResidence(eth);
			if (entity == null)
			{
				break;
			}
			int num2 = MathUtil.ClampMin(entity.components.residence.CountEmptyApartments() / 2, 1);
			for (int i = 0; i < num2; i++)
			{
				if (num <= 0)
				{
					break;
				}
				int num3 = 1 + data.rng.Generate(0, 5);
				ApartmentData apt = CreateResidentGroup(eth, num3);
				num = MathUtil.ClampMin(num - num3, 0);
				entity.components.residence.AddResidents(apt);
			}
		}
		return count - num;
	}

	private Entity FindResidence(Label eth)
	{
		return _moveins.GetBestResidence(eth).entity;
	}

	private ApartmentData CreateResidentGroup(Label eth, int count)
	{
		return new ApartmentData
		{
			count = count,
			eth = eth
		};
	}

	public string GetNewspaperSubheadKey()
	{
		float num = data.rng.GenerateFloat();
		MapConfig.NewsType newsType = ((!((double)num < 0.4)) ? (((double)num < 0.7) ? MapConfig.NewsType.Dated : MapConfig.NewsType.Sequences) : MapConfig.NewsType.General);
		if (newsType == MapConfig.NewsType.Sequences)
		{
			if (data.newsSequenceRoot == null)
			{
				List<string> newspaperKeys = Game.ctx.session.mapconfig.GetNewspaperKeys(MapConfig.NewsType.Sequences);
				data.newsSequenceRoot = data.rng.PickElement(newspaperKeys);
				data.newsSequenceNextIndex = 1;
			}
			if (data.newsSequenceRoot != null)
			{
				string result = data.newsSequenceRoot + "." + data.newsSequenceNextIndex.ToString(CultureInfo.InvariantCulture);
				data.newsSequenceNextIndex++;
				string key = data.newsSequenceRoot + "." + data.newsSequenceNextIndex.ToString(CultureInfo.InvariantCulture);
				if (!Game.serv.loc.HasKey(key))
				{
					data.newsSequenceRoot = null;
					data.newsSequenceNextIndex = 0;
				}
				return result;
			}
		}
		if (newsType == MapConfig.NewsType.Dated)
		{
			List<string> newspaperKeys2 = Game.ctx.session.mapconfig.GetNewspaperKeys(MapConfig.NewsType.Dated);
			string text = data.rng.PickElement(newspaperKeys2) + "." + Game.ctx.clock.Now.YearsInt.ToString(CultureInfo.InvariantCulture);
			if (Game.serv.loc.HasKey(text))
			{
				return text;
			}
		}
		List<string> newspaperKeys3 = Game.ctx.session.mapconfig.GetNewspaperKeys(MapConfig.NewsType.General);
		return data.rng.PickElement(newspaperKeys3);
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(ResidenceTrackerPersistedData result)
		{
			this.data = result;
		});
		yield break;
	}
}
