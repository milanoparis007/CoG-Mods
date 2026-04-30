using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Services.Maps;
using Game.Session;
using Game.Session.Setup;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI;

public class NewGameCustomizePopup : BasePopup
{
	[DebuggerDisplay("{DebugString}")]
	public class RotatorState
	{
		public struct Entry
		{
			public bool ispercent;

			public int value;

			public string desc;

			public Fixnum AsFraction
			{
				get
				{
					if (!ispercent)
					{
						throw new InvalidOperationException();
					}
					return new Fixnum(value) / 100;
				}
			}

			public static Entry MakePercent(int percent)
			{
				return new Entry
				{
					ispercent = true,
					value = percent,
					desc = Loc.Percentage(new Fixnum(percent) / 100)
				};
			}

			public static Entry MakeEnum(string locroot, int index)
			{
				return new Entry
				{
					ispercent = false,
					value = index,
					desc = Loc.GetPluralized(locroot, index)
				};
			}
		}

		public const string ROW_LABEL = "Text";

		public const string ROT_LEFT = "Rotator/Left";

		public const string ROT_RIGHT = "Rotator/Right";

		public const string ROT_VALUE = "Rotator/Label";

		public GameObject row;

		public List<Entry> data;

		public int index;

		public string child;

		public Entry Current => data[index];

		public int CurrentValue => Current.value;

		internal string DebugString => string.Format("[{0} {1}]", child.Replace("Scroll View List/Viewport/Content/Row ", ""), CurrentValue);

		public RotatorState(GameObject panel, string child, int initial, params int[] percents)
		{
			Initialize(panel, child, percents.Select(Entry.MakePercent).ToList(), initial);
		}

		public RotatorState(GameObject panel, string child, int initial, string locroot, int total)
		{
			Initialize(panel, child, (from i in Enumerable.Range(0, total)
				select Entry.MakeEnum(locroot, i)).ToList(), initial);
		}

		private void Initialize(GameObject panel, string child, List<Entry> data, Fixnum initial)
		{
			row = panel.GetChild(child);
			this.child = child;
			this.data = data;
			index = IndexOf(initial);
			InitButtons();
			UpdateText();
		}

		public int IndexOf(Fixnum value)
		{
			return data.FindIndexOrFirst((Entry e) => e.value == value);
		}

		private void InitButtons()
		{
			row.GetButton("Rotator/Left").onClick.SetListener(delegate
			{
				OnClick(-1);
			});
			row.GetButton("Rotator/Right").onClick.SetListener(delegate
			{
				OnClick(1);
			});
		}

		private void OnClick(int delta)
		{
			index = MathUtil.Modulus(index + delta, data.Count);
			UpdateText();
		}

		private void UpdateText()
		{
			row.SetText("Rotator/Label", Current.desc);
		}
	}

	private const string CLOSE_BUTTON = "Close/";

	private const string CONTINUE_BUTTON = "Footer/Continue/";

	private const string CANCEL_BUTTON = "Footer/Cancel/";

	private const string SCROLL_VIEW = "Scroll View List";

	private const string CONTENT = "Scroll View List/Viewport/Content/";

	private const string CITY_INPUT = "Scroll View List/Viewport/Content/City/City Name";

	private const string STATE_INPUT = "Scroll View List/Viewport/Content/State/State Name";

	private const string SEED_INPUT = "Scroll View List/Viewport/Content/State/Seed";

	private const string CITY_DROPDOWN = "Scroll View List/Viewport/Content/City/Dropdown";

	private const string ROW_SIZE = "Scroll View List/Viewport/Content/Row Size";

	private const string ROW_GOONS = "Scroll View List/Viewport/Content/Row Goons";

	private const string ROW_GANGS = "Scroll View List/Viewport/Content/Row Gangs";

	private const string ROW_COPS = "Scroll View List/Viewport/Content/Row Cops";

	private const string ROW_RIVERS = "Scroll View List/Viewport/Content/Row Rivers";

	private const string ROW_HILLS = "Scroll View List/Viewport/Content/Row Hills";

	private const string ROW_COAST = "Scroll View List/Viewport/Content/Row Coast";

	private const string ROW_LAKE = "Scroll View List/Viewport/Content/Row Center Lake";

	private const string ROW_IND = "Scroll View List/Viewport/Content/Row Ind";

	private const string ROW_COM = "Scroll View List/Viewport/Content/Row Com";

	private const string ROW_RSIZE = "Scroll View List/Viewport/Content/Row Net Size";

	private const string ROW_RTYPE = "Scroll View List/Viewport/Content/Row Net Type";

	private readonly GameParameters _parameters;

	private readonly NewGameStartFunction _continuation;

	private TMP_InputField _cityNameText;

	private TMP_InputField _stateNameText;

	private TMP_InputField _rngSeedText;

	private TMP_Dropdown _cityDropdown;

	private List<MapConfig> _maps;

	public RotatorState rsize;

	public RotatorState rgangs;

	public RotatorState rgoons;

	public RotatorState rcops;

	public RotatorState rrivers;

	public RotatorState rhills;

	public RotatorState rcoast;

	public RotatorState rlake;

	public RotatorState rind;

	public RotatorState rcom;

	public RotatorState rroadsize;

	public RotatorState rroadtype;

	private const int ROADS_GRID = 0;

	private const int ROADS_ORGD = 1;

	private const int ROADS_MESS = 2;

	private const int ROADS_EURO = 3;

	private const int RIVER_NONE = 0;

	private const int RIVER_SIMP = 1;

	private const int RIVER_WIND = 2;

	private const int RIVER_MULT = 3;

	private const int HILLS_NONE = 0;

	private const int HILLS_AFEW = 1;

	private const int HILLS_SOME = 2;

	private const int HILLS_MANY = 3;

	private const int COAST_X = 0;

	private const int COAST_N = 1;

	private const int COAST_E = 2;

	private const int COAST_S = 3;

	private const int COAST_W = 4;

	private const int LAKE_X = 0;

	private const int LAKE_S = 1;

	private const int LAKE_L = 2;

	private const int LAKE_W = 3;

	public override UIReference UIReference => UIElements.NewGameCustomizePopup;

	public NewGameCustomizePopup(GameParameters parameters, NewGameStartFunction continuation)
	{
		_parameters = parameters;
		_continuation = continuation;
	}

	protected override void InitializeOnPush()
	{
		_panel.SetButtonListener("Close/", Close);
		_panel.SetButtonListener("Footer/Cancel/", Close);
		_panel.SetButtonListener("Footer/Continue/", ShowNext);
		_maps = NewGameCityPopup.GetPurchasedBaseMapsSorted().ToList();
		InitializePanel();
		InitializeRotators();
	}

	protected override void ReleaseOnPop()
	{
		_maps = null;
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new PopupHandlerWithTabSupport();
	}

	private void ShowNext()
	{
		GameParameters parameters = UpdateParameters(_parameters);
		MapConfig map = MakeCustomMap(GetOriginatingMap());
		NewGameBossPopup popup = new NewGameBossPopup(parameters, map, CallContinuation);
		Game.serv.ui.AddPopup(popup);
	}

	private void CallContinuation(GameParameters parameters, MapConfig map)
	{
		Close();
		_continuation(parameters, map);
	}

	private MapConfig GetOriginatingMap()
	{
		int value = _cityDropdown.value;
		if (value >= 0 && value < _maps.Count)
		{
			return _maps[value];
		}
		return _maps.FirstOrDefaultFast();
	}

	private GameParameters UpdateParameters(GameParameters inputs)
	{
		inputs.mapdef = GetOriginatingMap()?.id;
		return inputs;
	}

	private void InitializePanel()
	{
		_cityDropdown = _panel.GetChild<TMP_Dropdown>("Scroll View List/Viewport/Content/City/Dropdown");
		_cityDropdown.SetOptions(_maps.Select((MapConfig map) => map.CityName).ToList());
		_cityDropdown.onValueChanged.SetListener(delegate
		{
			RandomizeCityName();
		});
		_cityNameText = _panel.GetChild<TMP_InputField>("Scroll View List/Viewport/Content/City/City Name");
		_stateNameText = _panel.GetChild<TMP_InputField>("Scroll View List/Viewport/Content/State/State Name");
		_rngSeedText = _panel.GetChild<TMP_InputField>("Scroll View List/Viewport/Content/State/Seed");
		_rngSeedText.SetTextWithoutNotify(_parameters.userrng.ToString(CultureInfo.InvariantCulture));
		_go.GetChild("Scroll View List").ResetScrollView();
		RandomizeCityName();
	}

	private void RandomizeCityName()
	{
		MapConfig orDefaultFast = _maps.GetOrDefaultFast(_cityDropdown.value);
		if (orDefaultFast != null)
		{
			_cityNameText.SetTextWithoutNotify(Loc.Get("ui.customcity.newname", "name", orDefaultFast.CityName));
			_stateNameText.SetTextWithoutNotify(Loc.Get("ui.customcity.newname", "name", orDefaultFast.StateName));
		}
	}

	private void InitializeRotators()
	{
		rsize = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Size", 100, 75, 80, 90, 100);
		rgangs = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Gangs", 100, 0, 25, 50, 75, 100, 125, 150, 200);
		rgoons = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Goons", 100, 0, 25, 50, 75, 100, 125, 150, 200);
		rcops = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Cops", 100, 0, 25, 50, 75, 100, 125, 150, 200);
		rrivers = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Rivers", 1, "ui.customcity.rivers", 4);
		rhills = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Hills", 1, "ui.customcity.hills", 4);
		rcoast = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Coast", 0, "ui.customcity.coast", 5);
		rlake = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Center Lake", 0, "ui.customcity.centerlake", 4);
		rroadsize = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Net Size", 100, 75, 80, 90, 100);
		rroadtype = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Net Type", 1, "ui.customcity.nettype", 4);
		rind = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Ind", 100, 0, 50, 100, 150, 200);
		rcom = new RotatorState(_panel, "Scroll View List/Viewport/Content/Row Com", 100, 0, 50, 100, 150, 200);
	}

	private MapConfig MakeCustomMap(MapConfig original)
	{
		int gensym = 0;
		MapConfig def = Game.serv.serializer.instance.Clone(original);
		MapBoardConfig map = def.map;
		Xorshift rng = new Xorshift(_parameters.userrng);
		def.id = ((int)DateTime.UtcNow.Ticks).ToString(CultureInfo.InvariantCulture);
		def.proc = true;
		def.cityname = _cityNameText.text.Sanitize();
		def.statename = _stateNameText.text.Sanitize();
		def.newspaperExtras = null;
		UpdateGroup(rgangs, PlayerType.GangPlayer);
		UpdateGroup(rgoons, PlayerType.GoonPlayer);
		UpdateGroup(rcops, PlayerType.CopPlayer);
		UpdateMapSize();
		UpdateRoads();
		UpdateRail();
		UpdateRivers();
		UpdateCoast();
		UpdateLake();
		UpdateHills();
		UpdateDistricts();
		string text = TypeUtils.GetMemberInstances<RotatorState>(this).SelectToString((RotatorState r) => r.DebugString, " ");
		Logger.LogAlways($"Starting custom game with params {original.citytype} {original.id} {_parameters.userrng} {text}");
		return def;
		void AddBodyOfWater(WorldPos pos, WorldSize size, float wobblesize)
		{
			RandomRangeF wobble = new RandomRangeF(1f, wobblesize, 1);
			List<WorldPos> corners = new List<WorldPos>();
			AddCorner(0, 1);
			AddCorner(1, 1);
			AddCorner(0, 0);
			AddCorner(1, 0);
			WaterNodesConfig item = new WaterNodesConfig
			{
				id = Gensym(),
				body = true,
				allowBridges = false,
				deformers = corners,
				width = 20
			};
			def.terrain.waternodes.Add(item);
			void AddCorner(int xmul, int ymul)
			{
				float num = pos.x + size.width * (float)xmul;
				float num2 = pos.y + size.height * (float)ymul;
				float num3 = rng.Generate(wobble) * (float)((xmul > 0) ? 1 : (-1));
				float num4 = rng.Generate(wobble) * (float)((ymul > 0) ? 1 : (-1));
				WorldPos item2 = new WorldPos(num + num3, num2 + num4);
				corners.Add(item2);
			}
		}
		void AddDistrictOfType(List<MapNodesConfig> centers, bool industrial, Label tag, bool downtown)
		{
			if (centers.Count > 0)
			{
				MapNodesConfig mapNodesConfig = rng.PickAndRemoveElement(centers);
				int radius = (int)((float)mapNodesConfig.nodeSpacing.width * rng.Generate(2f, 3f));
				TagList tagList = new TagList { tag };
				if (downtown)
				{
					tagList.Add(DistrictConfig.TAG_DOWNTOWN);
				}
				string customname = (downtown ? Loc.Get("districts.downtown") : MakeDistrictName(industrial));
				DistrictConfig item = new DistrictConfig
				{
					start = mapNodesConfig.forceStart,
					radius = radius,
					customname = customname,
					tags = tagList
				};
				def.map.districts.Add(item);
			}
		}
		Label Gensym()
		{
			int num = ++gensym;
			return new Label(num.ToString(CultureInfo.InvariantCulture));
		}
		static string MakeDistrictName(bool industrial)
		{
			string text2 = Loc.Get(industrial ? "district.procgen.prefix.ind" : "district.procgen.prefix.com");
			return Loc.Get(industrial ? "district.procgen.suffix.ind" : "district.procgen.suffix.com", "noun", text2).ToUpperInvariant();
		}
		void UpdateCoast()
		{
			IntSize mapSize = map.mapSize;
			WorldSize size = default(WorldSize);
			WorldPos pos = default(WorldPos);
			switch (rcoast.CurrentValue)
			{
			case 1:
				pos = new WorldPos(0f, mapSize.height);
				size = new WorldSize(mapSize.width, 1f);
				break;
			case 3:
				pos = new WorldPos(0f, 0f);
				size = new WorldSize(mapSize.width, 1f);
				break;
			case 2:
				pos = new WorldPos(mapSize.width, 0f);
				size = new WorldSize(1f, mapSize.height);
				break;
			case 4:
				pos = new WorldPos(0f, 0f);
				size = new WorldSize(1f, mapSize.height);
				break;
			}
			if (size.height != 0f && size.width != 0f)
			{
				AddBodyOfWater(pos, size, 100f);
			}
		}
		void UpdateDistricts()
		{
			def.map.districts.Clear();
			float num = (float)original.map.districts.Count / 2f;
			List<MapNodesConfig> centers = new List<MapNodesConfig>(map.nodes);
			int num2 = (int)((float)rcom.CurrentValue * num / 100f);
			int num3 = (int)((float)rind.CurrentValue * num / 100f);
			bool downtown = true;
			do
			{
				if (num2-- > 0)
				{
					AddDistrictOfType(centers, industrial: false, DistrictConfig.TAG_COMCENTER, downtown);
					downtown = false;
				}
				if (num3-- > 0)
				{
					AddDistrictOfType(centers, industrial: true, DistrictConfig.TAG_INDCENTER, downtown: false);
				}
			}
			while (num2 > 0 || num3 > 0);
		}
		void UpdateGroup(RotatorState rot, PlayerType type)
		{
			if (rot.CurrentValue != 100)
			{
				def.groups.ForceCount(type, (def.groups.GetCount(type) * rot.Current.AsFraction).IntFloor());
			}
		}
		void UpdateHills()
		{
			def.terrain.mountainnodes.Clear();
			RandomRange randomRange = null;
			RandomRange range = null;
			switch (rhills.CurrentValue)
			{
			case 1:
				randomRange = new RandomRange(1, 4, 1);
				range = new RandomRange(1, 2, 1);
				break;
			case 2:
				randomRange = new RandomRange(4, 8, 1);
				range = new RandomRange(1, 4, 1);
				break;
			case 3:
				randomRange = new RandomRange(7, 20, 1);
				range = new RandomRange(1, 4, 1);
				break;
			}
			if (randomRange != null)
			{
				IntPos asIntPos = map.mapSize.AsIntPos;
				RandomRangeF range2 = new RandomRangeF(0f, asIntPos.x, 1);
				RandomRangeF range3 = new RandomRangeF(0f, asIntPos.y, 1);
				RandomRange range4 = new RandomRange(20, 50, 1);
				int num = rng.Generate(randomRange);
				for (int i = 0; i < num; i++)
				{
					WorldPos worldPos = new WorldPos(rng.Generate(range2), rng.Generate(range3));
					int num2 = rng.Generate(range);
					for (int j = 0; j < num2; j++)
					{
						int num3 = rng.Generate(range4);
						int num4 = rng.Generate(0, num3);
						int num5 = rng.Generate(0, num3);
						WorldPos forceStart = worldPos.Increment(num4, num5);
						MountainNodesConfig item = new MountainNodesConfig
						{
							id = Gensym(),
							forceStart = forceStart,
							size = num3
						};
						def.terrain.mountainnodes.Add(item);
					}
				}
			}
		}
		void UpdateLake()
		{
			int num = 0;
			float num2 = 0f;
			float wobblesize = 0f;
			switch (rlake.CurrentValue)
			{
			case 1:
				num = 3;
				num2 = 0.02f;
				wobblesize = 20f;
				break;
			case 2:
				num = 5;
				num2 = 0.05f;
				wobblesize = 30f;
				break;
			case 3:
				num = 9;
				num2 = 0.1f;
				wobblesize = 20f;
				break;
			}
			if (num != 0)
			{
				for (int i = 0; i < num; i++)
				{
					float num3 = 0.5f - num2;
					float num4 = 0.5f + num2;
					IntSize mapSize = map.mapSize;
					RandomRangeF range = new RandomRangeF((float)mapSize.width * num3, (float)mapSize.width * num4, 1);
					RandomRangeF range2 = new RandomRangeF((float)mapSize.height * num3, (float)mapSize.height * num4, 1);
					WorldPos pos = new WorldPos(rng.Generate(range), rng.Generate(range2));
					AddBodyOfWater(pos, new WorldSize(1f, 1f), wobblesize);
				}
			}
		}
		void UpdateMapSize()
		{
			if (rsize.CurrentValue != 100)
			{
				Fixnum asFraction = rsize.Current.AsFraction;
				IntSize mapSize = map.mapSize;
				int width = (asFraction * mapSize.width).IntFloor();
				int height = (asFraction * mapSize.height).IntFloor();
				map.mapSize = new IntSize(width, height);
			}
		}
		void UpdateRail()
		{
			map.rails.Clear();
			int orDefaultFast = new List<int> { 2, 3, 3, 4 }.GetOrDefaultFast(rroadsize.index);
			for (int i = 0; i < orDefaultFast; i++)
			{
				map.rails.Add(new RailConfig
				{
					id = Gensym(),
					random = true
				});
			}
		}
		void UpdateRivers()
		{
			def.terrain.waternodes.Clear();
			int num = 0;
			float num2 = 30f;
			float num3 = 2f;
			float num4 = 4f;
			switch (rrivers.CurrentValue)
			{
			case 1:
				num = 2;
				num2 = 30f;
				num3 = 0.5f;
				break;
			case 2:
				num = 2;
				num2 = 60f;
				break;
			case 3:
				num = 3;
				num2 = 45f;
				break;
			}
			if (num != 0)
			{
				float num5 = 0.4f;
				float num6 = 0.6f;
				IntPos asIntPos = map.mapSize.AsIntPos;
				RandomRangeF range = new RandomRangeF((float)asIntPos.x * num5, (float)asIntPos.x * num6, 1);
				RandomRangeF range2 = new RandomRangeF((float)asIntPos.y * num5, (float)asIntPos.y * num6, 1);
				WorldPos worldPos = new WorldPos(rng.Generate(range), rng.Generate(range2));
				for (int i = 0; i < num; i++)
				{
					float num7 = i switch
					{
						1 => rng.Generate(120f, 230f), 
						0 => rng.Generate(-50f, 50f), 
						_ => rng.Generate(0f, 360f), 
					};
					float num8 = rng.Generate(3f, 7f);
					float num9 = rng.Generate(10f, 30f);
					WorldPos forceStart = worldPos;
					int j = 0;
					for (int num10 = 40; j < num10; j++)
					{
						WaterNodesConfig waterNodesConfig = new WaterNodesConfig
						{
							id = Gensym(),
							forceStart = forceStart,
							width = (int)num8
						};
						if (j > 0)
						{
							waterNodesConfig.connectingNodes = new List<Label> { def.terrain.waternodes.LastOrDefaultFast().id };
						}
						def.terrain.waternodes.Add(waterNodesConfig);
						if (forceStart.x < 0f || forceStart.y < 0f || forceStart.x > (float)map.mapSize.width || forceStart.y > (float)map.mapSize.height)
						{
							break;
						}
						num7 += rng.Generate(0f - num2, num2);
						num8 = MathUtil.Clamp(num8 + rng.Generate(0f - num3, num3), 3f, 7f);
						num9 = MathUtil.Clamp(num9 + rng.Generate(0f - num4, num4), 10f, 30f);
						forceStart += new WorldPos(num9, 0f).Rotate(num7);
					}
				}
			}
		}
		void UpdateRoads()
		{
			map.nodes.Clear();
			float num = 1f;
			float num2 = 1f;
			float num3 = 0f;
			bool flag = false;
			switch (rroadtype.CurrentValue)
			{
			case 0:
				num = 0.5f;
				num2 = 2f;
				num3 = 1f;
				break;
			case 1:
				num = 0.5f;
				num2 = 2f;
				num3 = 0.7f;
				break;
			case 2:
				num = 1f;
				num2 = 1f;
				num3 = 0.4f;
				flag = true;
				break;
			case 3:
				num = 2f;
				num2 = 0.5f;
				num3 = 0f;
				flag = true;
				break;
			}
			int num4 = (15 * rsize.Current.AsFraction).IntFloor();
			int num5 = 100;
			int num6 = (int)((float)num4 * num);
			int forceMaxSize = (int)((float)num5 * num2);
			int num7 = (int)((float)num4 * num * num3);
			for (int i = 0; i < num6; i++)
			{
				MapNodesConfig mapNodesConfig = new MapNodesConfig
				{
					id = Gensym(),
					forceMaxSize = forceMaxSize,
					nodeSpacing = new IntSize(rng.Generate(6, 11), rng.Generate(6, 11))
				};
				if (i < num7)
				{
					mapNodesConfig.forceAngle = 0f;
				}
				IntSize nodeSpacing = mapNodesConfig.nodeSpacing;
				if (flag)
				{
					WorldPos worldPos = CreateMapNodes.FindRandomNodePosition(nodeSpacing, map.mapSize.Half, rng);
					WorldPos worldPos2 = CreateMapNodes.FindRandomNodePosition(nodeSpacing, map.mapSize.Half, rng);
					mapNodesConfig.forceStart = worldPos + worldPos2;
				}
				else
				{
					mapNodesConfig.forceStart = CreateMapNodes.FindRandomNodePosition(nodeSpacing, map.mapSize, rng);
				}
				map.nodes.Add(mapNodesConfig);
			}
		}
	}
}
