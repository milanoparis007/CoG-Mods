using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using SomaSim.Util;

namespace Game.Services.Maps;

public sealed class MapConfig
{
	public sealed class TerrainConfig
	{
		public float terrainscale;

		public float groundheight;

		public float waterheight;

		public float xywobble;

		public float zwobble;

		public IntSize tileverts;

		public IntSize chunktiles;

		public bool flattenHills;

		public List<WaterNodesConfig> waternodes;

		public List<MountainNodesConfig> mountainnodes;

		public float GetTerrainScale(bool isMountain)
		{
			if (!isMountain || !flattenHills)
			{
				return terrainscale;
			}
			return 1E-07f;
		}
	}

	public sealed class EthnicMakeup
	{
		public sealed class SkinTypes
		{
			public List<Skin> names;

			public List<float> proportions;
		}

		public static Label DEFAULT_SKIN = (Label)"others";

		public LabelDictionary<int> nationalities = new LabelDictionary<int>();

		public LabelDictionary<SkinTypes> skin = new LabelDictionary<SkinTypes>();

		public List<Label> MakeUniqueEthsSorted()
		{
			return nationalities.Keys.OrderBy((Label l) => l.String).ToList();
		}

		public List<Label> MakeExpandedEthsSorted()
		{
			return (from l in MakeProportionalNationalities()
				orderby l.String
				select l).ToList();
		}

		public SkinTypes FindSkinTypesForEth(Label eth)
		{
			return skin.FindOrNull(eth) ?? skin.FindOrNull(DEFAULT_SKIN);
		}

		public Skin GetRandomSkinForEth(IRandom rng, Label eth)
		{
			SkinTypes skinTypes = FindSkinTypesForEth(eth);
			if (skinTypes == null)
			{
				return Skin.Unknown;
			}
			return rng.PickElement(skinTypes.names, skinTypes.proportions);
		}

		private IEnumerable<Label> MakeProportionalNationalities()
		{
			foreach (KeyValuePair<Label, int> entry in nationalities)
			{
				int i = 0;
				for (int count = entry.Value; i < count; i++)
				{
					yield return entry.Key;
				}
			}
		}
	}

	public class GroupDef
	{
		public PlayerType type;

		public int count;
	}

	public sealed class GroupCounts : List<GroupDef>
	{
		public GroupDef FindOrNull(PlayerType type)
		{
			return Find((GroupDef def) => def.type == type);
		}

		public int GetCount(PlayerType type)
		{
			return FindOrNull(type)?.count ?? 0;
		}

		public void ForceCount(PlayerType type, int count)
		{
			GroupDef groupDef = FindOrNull(type);
			if (groupDef != null)
			{
				groupDef.count = count;
			}
		}
	}

	public sealed class MapgenMoveInScores
	{
		public RandomRangeF emptyLotBase;

		public float resSameEthnicityBoost;

		public int resInitialSeeds;

		public float resResidentialAreaBoost;

		public float resCommercialAreaBoost;

		public float resIndustrialAreaBoost;

		public float resRailOrTerminalBoost;

		public int indInitialSeeds;

		public float indResidentialAreaBoost;

		public float indCommercialAreaBoost;

		public float indIndustrialAreaBoost;

		public float indRailOrTerminalBoost;

		public int comInitialSeeds;

		public float comResidentialAreaBoost;

		public float comCommercialAreaBoost;

		public float comIndustrialAreaBoost;

		public float comRailOrTerminalBoost;

		public Dictionary<int, float> interestingNodeProbabilities;

		public float potentialNodeProbability;
	}

	public sealed class FamilyGenConfig
	{
		public float initialFamiliesRatio;

		public float marriageRatio;

		public RandomRangeF marriageAgeRange;

		public List<int> kidsCount;

		public float childMortalityRatio;

		public RandomRangeF childMortalityAgeRange;

		public RandomRangeF adultMortalityAgeRange;

		public RandomRange friendsCount;

		public RandomRange friendsAge;

		public IntRange traitsPerPerson;

		public float probTraitInherited;

		public float probTraitOther;
	}

	public sealed class GenClockConfig
	{
		public struct LotInfo
		{
			public float mindensity;

			public float probfail;

			public Label lot;

			public bool skipOnBoardwalk;
		}

		public struct DecoInfo
		{
			public int @default;

			public int lowappeal;
		}

		public class DecoNodes
		{
			public float maxdensity;

			public float maxappeal;

			public float centers;

			public DecoInfo spread;

			public DecoInfo lotpercent;
		}

		public class Specials
		{
			public class Def
			{
				public List<Label> configs;

				public TagList districtTags;

				public int maxPerDistrict;

				public RandomRange skipEvery;
			}

			public class State
			{
				public Def def;

				public int leftToSkip;

				public Dictionary<int, int> countPerDistrict;

				public State()
				{
				}

				public State(IRandom rng, Def def)
				{
					this.def = def;
					countPerDistrict = new Dictionary<int, int>();
					ResetLeftToSkip(rng);
				}

				public void ResetLeftToSkip(IRandom rng)
				{
					leftToSkip = rng.Generate(def.skipEvery);
				}

				public bool DoesPassMaxPerDistrict(Node node, Def def)
				{
					if (node.districts != null)
					{
						foreach (NodeDistrictData district in node.districts)
						{
							if (countPerDistrict.FindOrDefault(district.index, 0) >= def.maxPerDistrict)
							{
								return false;
							}
						}
					}
					return true;
				}

				public void IncrementCountPerDistrict(Node node)
				{
					if (node.districts == null)
					{
						return;
					}
					foreach (NodeDistrictData district in node.districts)
					{
						countPerDistrict.Increment(district.index, 1);
					}
				}
			}

			public class DefStates : List<State>
			{
				public DefStates()
				{
				}

				public DefStates(IEnumerable<State> data)
					: base(data)
				{
				}

				public State GetModulus(int i)
				{
					return this.GetOrDefaultFast(i % base.Count);
				}
			}

			public Def policeStation;

			public Def trainStation;

			public List<Def> civicsAndOthers;

			public DefStates GenerateStatesForCivics(IRandom rng)
			{
				return new DefStates(civicsAndOthers.Select((Def d) => new State(rng, d)));
			}
		}

		public int totalInflux;

		public List<LotInfo> lots;

		public DecoNodes decoNodes;

		public Specials specialBuildings;

		public float nodeCenterScore;

		public float otherCenterScore;

		public int otherCenterCount;

		public float lowAppealThreshold;

		public float midAppealThreshold;
	}

	public sealed class PlayerStartConfig
	{
		public enum CityStarterPack
		{
			NoStarterPack,
			PhiladelphiaStarterPack,
			NewYorkStarterPack
		}

		public int nodesKnownAtStart;

		public int nodesFriendlyAtStart;

		public float mapEdgePlacementMargin = 30f;

		public float mapEdgePlacementPenalty = 0.1f;

		public List<Label> resourcesUnlocked = new List<Label>();

		public TagList copsSkipDistricts;

		public CityStarterPack starterPack;
	}

	public enum NewsType
	{
		General,
		Dated,
		Sequences
	}

	public string id;

	public string citytype;

	public string cityname;

	public string statename;

	public string citydesc;

	public string citylockey;

	public string citypolunitkey;

	public string statelockey;

	public string citydesckey;

	public string cityscreen;

	public string citylocicon;

	public bool proc;

	public GenClockConfig generator = new GenClockConfig();

	public PlayerStartConfig playerStart = new PlayerStartConfig();

	public MapBoardConfig map = new MapBoardConfig();

	public TerrainConfig terrain = new TerrainConfig();

	public EthnicMakeup ethnicMakeup = new EthnicMakeup();

	public MapgenMoveInScores moveinScores = new MapgenMoveInScores();

	public FamilyGenConfig familyGenerator = new FamilyGenConfig();

	public SeasonConfig seasons = new SeasonConfig();

	public GroupCounts groups = new GroupCounts();

	public NewspaperConfig newspaperExtras = new NewspaperConfig();

	private List<Label> _uniqueEthnicitiesSorted;

	private List<Label> _allEthnicitiesSorted;

	public string CityName => GetCityName(default(LocReplacementContext));

	public string StateName => GetStateName(default(LocReplacementContext));

	public string CityDesc => GetCityDesc(default(LocReplacementContext));

	public bool IsProcGen => proc;

	public string GetCityName(LocReplacementContext ctx)
	{
		return cityname ?? Loc.Get(citylockey, ctx);
	}

	public string GetStateName(LocReplacementContext ctx)
	{
		return statename ?? Loc.Get(statelockey, ctx);
	}

	public string GetCityDesc(LocReplacementContext ctx)
	{
		return citydesc ?? Loc.Get(citydesckey, ctx);
	}

	public List<Label> GetEthnicitiesUniqueSorted()
	{
		if (_uniqueEthnicitiesSorted == null)
		{
			_uniqueEthnicitiesSorted = ethnicMakeup.MakeUniqueEthsSorted();
		}
		return _uniqueEthnicitiesSorted;
	}

	public List<Label> GetEthnicitiesAllSorted()
	{
		if (_allEthnicitiesSorted == null)
		{
			_allEthnicitiesSorted = ethnicMakeup.MakeExpandedEthsSorted();
		}
		return _allEthnicitiesSorted;
	}

	public IEnumerable<EthnicityDef> MakePlayableEthnicitiesForThisMap()
	{
		return from id in GetEthnicitiesUniqueSorted()
			select Game.serv.globals.settings.ethnicities.FindEthnicityDef(id) into def
			where def != null && !def.hidden
			select def;
	}

	public List<string> GetNewspaperKeys(NewsType type)
	{
		NewspaperConfig newspaperDefaults = Game.serv.globals.ui.newspaperDefaults;
		NewspaperConfig newspaperConfig = newspaperExtras;
		List<string> list = null;
		List<string> list2 = null;
		switch (type)
		{
		case NewsType.General:
			list = newspaperConfig.subheadsGeneral ?? newspaperDefaults.subheadsGeneral;
			list2 = newspaperConfig.subheadsCityGeneral ?? newspaperDefaults.subheadsCityGeneral;
			break;
		case NewsType.Dated:
			list = newspaperConfig.subheadsDated ?? newspaperDefaults.subheadsDated;
			list2 = newspaperConfig.subheadsCityDated ?? newspaperDefaults.subheadsCityDated;
			break;
		case NewsType.Sequences:
			list = newspaperConfig.subheadsSequences ?? newspaperDefaults.subheadsSequences;
			list2 = newspaperConfig.subheadsCitySequences ?? newspaperDefaults.subheadsCitySequences;
			break;
		}
		List<string> list3 = new List<string>();
		if (list != null)
		{
			list3.AddRange(list);
		}
		if (list2 != null)
		{
			list3.AddRange(list2);
		}
		return list3;
	}
}
