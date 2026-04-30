using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Services;
using Game.Services.Input;
using Game.Services.Maps;
using Game.Services.Mods;
using Game.Session;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public class NewGameCityPopup : BasePopup
{
	private class MapSelectEntry
	{
		public MapConfig mapconfig;

		public ModMetadata modmeta;

		public ModData moddata;

		public bool IsCustom
		{
			get
			{
				if (mapconfig == null)
				{
					return modmeta == null;
				}
				return false;
			}
		}

		private MapSelectEntry()
		{
		}

		public static MapSelectEntry MakeBuiltIn(MapConfig mapconfig)
		{
			return new MapSelectEntry
			{
				mapconfig = mapconfig
			};
		}

		public static MapSelectEntry MakeModDef(ModMetadata meta, ModData data)
		{
			return new MapSelectEntry
			{
				modmeta = meta,
				moddata = data
			};
		}

		public static MapSelectEntry MakeCustom()
		{
			return new MapSelectEntry();
		}

		public string GetName()
		{
			return mapconfig?.CityName ?? modmeta?.GetIconAndName() ?? Loc.Get("ui.customcity.entry");
		}

		public string GetIcon()
		{
			if (mapconfig != null)
			{
				if (mapconfig.citylocicon != null)
				{
					return Loc.Get(mapconfig.citylocicon);
				}
				return "";
			}
			return Loc.Get("city-icon.custom");
		}

		public string GetDesc()
		{
			string text = mapconfig?.CityDesc;
			if (text != null)
			{
				return text;
			}
			object desc = modmeta.GetDesc();
			if (desc != null)
			{
				return Loc.Get("ui.options.mods.desc-header", "desc", desc);
			}
			return null;
		}

		public MapConfig GetMap()
		{
			MapConfig map = mapconfig;
			if (map == null)
			{
				ModData modData = moddata;
				if (modData == null)
				{
					return null;
				}
				map = modData.map;
			}
			return map;
		}
	}

	private const string CLOSE_BUTTON = "Close/";

	private const string CONTINUE_BUTTON = "Footer/Continue/";

	private const string CANCEL_BUTTON = "Footer/Cancel/";

	private const string MAP_SELECT = "Scenario/Cities/Viewport/Content";

	private const string RNG_BUTTON = "Scenario/Force Seed/Reroll";

	private const string RNG_SEED_TEXT = "Scenario/Force Seed/Input";

	private const string IMAGE_ROOT = "Scenario/City Image";

	private const string IMAGE_SPRITE = "Scenario/City Image/Backing/Sprite";

	private const string SCROLL_VIEW = "Scenario/Scroll View";

	private const string DESC_TEXT = "Scenario/Scroll View/Viewport/Content/Description";

	private const string TEMPLATE_CITY_SELECT_BUTTON = "Templates/New Game City Card";

	private readonly NewGameStartFunction _continuation;

	private uint _rngseed;

	private List<MapSelectEntry> _maps;

	private TMP_InputField _rngSeedText;

	private MapSelectEntry _selected;

	private GameObject _mapButtonTemplate;

	private const string BUTTON_TEXT = "Text";

	private const string BUTTON_TOGGLE = "Toggle";

	private const string BUTTON_ICON = "Icon";

	public static readonly string RESOURCES_FOLDER = "City Screenshots/";

	public static readonly string CUSTOM_SCREENIE = "Custom City Screenshot";

	public static readonly string WORKSHOP_SCREENIE = "Workshop Screenshot";

	public override UIReference UIReference => UIElements.NewGameCityPopup;

	public NewGameCityPopup(NewGameStartFunction continuation)
	{
		_continuation = continuation;
	}

	protected override void InitializeOnPush()
	{
		_panel.SetButtonListener("Close/", Close);
		_panel.SetButtonListener("Footer/Cancel/", Close);
		_panel.SetButtonListener("Footer/Continue/", ShowNext);
		_mapButtonTemplate = _go.GetChild("Templates/New Game City Card");
		SaveRngSeed(ScenarioConfig.MakeRngSeedForSession());
		InitializePanel();
		_selected = null;
		ReloadMapsAndRefresh();
		Game.serv.store.OnAfterDLCsReloaded.Add(ReloadMapsAndRefresh);
	}

	private void ReloadMapsAndRefresh()
	{
		string initialCityId = _selected?.mapconfig?.id;
		ReloadAvailableMaps(initialCityId);
		UpdateMapButtons();
		UpdateMapDescription();
	}

	private void ReloadAvailableMaps(string initialCityId)
	{
		IEnumerable<MapSelectEntry> source = from map in GetPurchasedBaseMapsSorted()
			select MapSelectEntry.MakeBuiltIn(map);
		IEnumerable<MapSelectEntry> second = GetEnabledModMapsSorted().Select(delegate(ModMetadata mod)
		{
			ModData data = Game.serv.mods.FindModDataOrNull(mod.modid);
			return MapSelectEntry.MakeModDef(mod, data);
		});
		_maps = source.Append(MapSelectEntry.MakeCustom()).Concat(second).ToList();
		initialCityId = initialCityId ?? "chicago";
		_selected = _maps.Where((MapSelectEntry x) => x.mapconfig.id == initialCityId).FirstOrDefault() ?? _maps.Where((MapSelectEntry x) => x.mapconfig.id == "chicago").FirstOrDefault();
	}

	public static IEnumerable<MapConfig> GetPurchasedBaseMapsSorted()
	{
		return from map in Game.serv.globals.mapgen.maps
			where Game.serv.store.IsMapAvailable(map.id)
			orderby map.CityName
			select map;
	}

	public static IEnumerable<ModMetadata> GetEnabledModMapsSorted()
	{
		return from mod in Game.serv.mods.GetEnabledModMetas()
			orderby mod.name
			select mod;
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.store.OnAfterDLCsReloaded.Remove(ReloadMapsAndRefresh);
		_maps = null;
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new CapturingKeyboardHandler(() => false, KeyboardHandler.Priority.HighestModalDialog);
	}

	private void ShowNext()
	{
		MapSelectEntry selected = _selected;
		GameParameters parameters = MakeNewGameParameters();
		BasePopup popup = (selected.IsCustom ? ((BasePopup)new NewGameCustomizePopup(parameters, CallContinuation)) : ((BasePopup)new NewGameBossPopup(parameters, selected.GetMap(), CallContinuation)));
		Game.serv.ui.AddPopup(popup);
	}

	private void CallContinuation(GameParameters parameters, MapConfig map)
	{
		Close();
		_continuation(parameters, map);
	}

	private GameParameters MakeNewGameParameters()
	{
		return new GameParameters
		{
			userrng = _rngseed,
			mapdef = _selected?.GetMap()?.id,
			playerdetails = default(PlayerStartupDetails)
		};
	}

	private void SaveRngSeed(uint seed)
	{
		_rngseed = seed;
	}

	private void InitializePanel()
	{
		_rngSeedText = _panel.GetChild<TMP_InputField>("Scenario/Force Seed/Input");
		_rngSeedText.SetTextWithoutNotify(_rngseed.ToString(CultureInfo.InvariantCulture));
		_rngSeedText.onValueChanged.SetListener(delegate
		{
			OnSeedChanged();
		});
		_panel.GetButton("Scenario/Force Seed/Reroll").onClick.SetListener(OnSeedRerollButton);
	}

	private void OnSeedRerollButton()
	{
		uint num = HashUtil.Hash((uint)DateTime.Now.Ticks);
		_rngSeedText.text = num.ToString();
	}

	private void OnSeedChanged()
	{
		string text = _rngSeedText.text;
		if (!uint.TryParse(text, out var result))
		{
			result = HashUtil.Hash(text);
		}
		SaveRngSeed(result);
	}

	private void OnMapSelectionChanged(int _)
	{
		UpdateMapDescription();
		OnSeedChanged();
	}

	private void UpdateMapButtons()
	{
		GameObject child = _panel.GetChild("Scenario/Cities/Viewport/Content");
		child.transform.DestroyAllChildren();
		child.EnsureChildCount(_maps, _mapButtonTemplate);
		child.InitializeChildren(_maps, InitializeMapButton);
	}

	private void InitializeMapButton(int i, GameObject card, MapSelectEntry map)
	{
		string text = _selected?.mapconfig?.id ?? "chicago";
		card.SetText("Text", map.GetName());
		card.SetText("Icon", map.GetIcon());
		Toggle toggle = card.GetToggle("Toggle");
		toggle.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				_selected = map;
				UpdateMapDescription();
			}
		});
		toggle.isOn = map.mapconfig?.id == text;
		toggle.group = _panel.GetChild<ToggleGroup>("Scenario/Cities/Viewport/Content");
	}

	private void UpdateMapDescription()
	{
		MapSelectEntry selected = _selected;
		MapConfig mapconfig = selected.mapconfig;
		string name;
		string text;
		if (selected.IsCustom)
		{
			name = CUSTOM_SCREENIE;
			text = Loc.Get("ui.customcity.desc");
		}
		else
		{
			name = mapconfig?.cityscreen ?? WORKSHOP_SCREENIE;
			text = selected.GetDesc() ?? Loc.Get("city-desc.default");
		}
		Texture2D texture2D = FindTexture2D(name);
		Sprite sprite = ((texture2D == null) ? null : Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0f, 0f)));
		_panel.SetTextOrHide("Scenario/Scroll View/Viewport/Content/Description", text);
		_panel.SetImageOrHide("Scenario/City Image/Backing/Sprite", sprite);
		_panel.SetActive("Scenario/City Image", sprite != null);
		_go.GetChild("Scenario/Scroll View").ResetScrollView();
	}

	private static Texture2D FindTexture2D(string name)
	{
		if (name == null)
		{
			return null;
		}
		return Resources.Load(RESOURCES_FOLDER + name) as Texture2D;
	}
}
