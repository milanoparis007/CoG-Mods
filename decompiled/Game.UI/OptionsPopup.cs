using System;
using System.Collections.Generic;
using System.Linq;
using Game.Services;
using Game.Services.Filesystem;
using Game.Services.Input;
using Game.Services.Mods;
using Game.UI.Mouseovers;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public class OptionsPopup : BasePopup
{
	private struct KeyHandlerInfo
	{
		public TMP_InputField inputField;

		public bool isMain;

		public KeyMappingTuple action;

		public bool IsValid => inputField != null;
	}

	private struct KeyReassign
	{
		public KeyMappingTuple action;

		public KeyCode? newMainKey;

		public KeyCode? newAltKey;
	}

	private class CustomKeyboardHandler : KeyboardHandler
	{
		private OptionsPopup _popup;

		private List<KeyInput> _handlers;

		public override Priority priority => Priority.HighestModalDialog;

		public override List<KeyInput> keyhandlers => _handlers;

		public override Fallthrough fallthrough
		{
			get
			{
				if (!_popup.ShouldCaptureKeypresses())
				{
					return Fallthrough.Always;
				}
				return Fallthrough.Never;
			}
		}

		public CustomKeyboardHandler(OptionsPopup popup)
		{
			_popup = popup;
			_handlers = ALLOWED_SHORTCUT_KEYS.Select((KeyCode code) => new KeyInput(code, delegate
			{
				KeyCodeCallback(code);
			})).ToList();
		}

		public override void Reset()
		{
			_popup = null;
			_handlers = new List<KeyInput>();
		}

		private void KeyCodeCallback(KeyCode key)
		{
			_popup.OnKeyboardHandler(key);
		}
	}

	private Toggle _graphicsTab;

	private Toggle _audiosfxTab;

	private Toggle _gameplayTab;

	private Toggle _controlsTab;

	private Toggle _modsTab;

	private Toggle _aboutTab;

	private GameObject _graphicsMenu;

	private GameObject _gameplayMenu;

	private GameObject _audiosfxMenu;

	private GameObject _controlsMenu;

	private GameObject _modsMenu;

	private GameObject _aboutMenu;

	private GameObject _actionTemplate;

	private Slider _musicSlider;

	private Slider _ambientSlider;

	private Slider _uiSfxSlider;

	private TextMeshProUGUI _uiScaleLabel;

	private TextMeshProUGUI _musicLabel;

	private TextMeshProUGUI _ambientLabel;

	private TextMeshProUGUI _uiSfxLabel;

	private Toggle _fullscreen;

	private Toggle _weather;

	private Toggle _traffic;

	private TMP_Dropdown _resolution;

	private TMP_Dropdown _uiscale;

	private TMP_Dropdown _vSync;

	private TMP_Dropdown _compute;

	private TMP_Dropdown _detail;

	private TMP_Dropdown _postProcessing;

	private TMP_Dropdown _language;

	private TMP_Dropdown _timeOfDay;

	private TMP_Dropdown _seasons;

	private TMP_Dropdown _tutorial;

	private TMP_Dropdown _numFormat;

	private TMP_Dropdown _volFormat;

	private TMP_Dropdown _dateFormat;

	private TMP_Dropdown _autosaveTime;

	private TMP_Dropdown _deliveryNotifs;

	private List<KeyReassign> _keysToReassign = new List<KeyReassign>();

	private KeyHandlerInfo currentKeyToReassign;

	private const string BUTTON_CLOSE = "Close Button";

	private const string BUTTON_GRAPHICS_APPLY = "Graphics/Apply";

	private const string BUTTON_AUDIO_APPLY = "Audio/Apply";

	private const string BUTTON_GAMEPLAY_APPLY = "Gameplay/Apply";

	private const string BUTTON_CONTROLS_APPLY = "Controls/Apply";

	private const string TAB_GROUP = "Tabs";

	private const string TAB_GAMEPLAY = "Tabs/Gameplay";

	private const string TAB_GRAPHICS = "Tabs/Graphics";

	private const string TAB_AUDIO = "Tabs/Audio";

	private const string TAB_CONTROL = "Tabs/Controls";

	private const string TAB_MODS = "Tabs/Mods";

	private const string TAB_ABOUT = "Tabs/About";

	private const string SUBMENU_GAMEPLAY = "Gameplay";

	private const string SUBMENU_GRAPHICS = "Graphics";

	private const string SUBMENU_AUDIO = "Audio";

	private const string SUBMENU_CONTROL = "Controls";

	private const string SUBMENU_MODS = "Mods";

	private const string SUBMENU_ABOUT = "About";

	private const string GRAPHICS = "Graphics/Elements/Viewport/Content";

	private const string TOGGLE_FULLSCREEN = "Graphics/Elements/Viewport/Content/Fullscreen/Toggle";

	private const string DROPDOWN_RESOLUTION = "Graphics/Elements/Viewport/Content/Resolution/Dropdown";

	private const string DROPDOWN_VSYNC = "Graphics/Elements/Viewport/Content/VSync/Dropdown";

	private const string DROPDOWN_COMPUTE = "Graphics/Elements/Viewport/Content/Compute/Dropdown";

	private const string DROPDOWN_DETAIL = "Graphics/Elements/Viewport/Content/Detail/Dropdown";

	private const string DROPDOWN_UISCALE = "Graphics/Elements/Viewport/Content/UI Scale/Dropdown";

	private const string DROPDOWN_POST_PROCESSING = "Graphics/Elements/Viewport/Content/Post Processing/Dropdown";

	private const string TOGGLE_WEATHER = "Graphics/Elements/Viewport/Content/Weather/Toggle";

	private const string TOGGLE_TRAFFIC = "Graphics/Elements/Viewport/Content/Traffic/Toggle";

	private const string GAMEPLAY = "Gameplay/Elements/Viewport/Content";

	private const string DROPDOWN_LANGUAGE = "Gameplay/Elements/Viewport/Content/Language/Dropdown";

	private const string DROPDOWN_TIMEOFDAY = "Gameplay/Elements/Viewport/Content/TimeOfDay/Dropdown";

	private const string DROPDOWN_SEASONS = "Gameplay/Elements/Viewport/Content/Seasons/Dropdown";

	private const string DROPDOWN_NUMBERS = "Gameplay/Elements/Viewport/Content/Numbers/Dropdown";

	private const string DROPDOWN_VOLUMES = "Gameplay/Elements/Viewport/Content/Volumes/Dropdown";

	private const string DROPDOWN_DATES = "Gameplay/Elements/Viewport/Content/Dates/Dropdown";

	private const string DROPDOWN_TUTORIAL = "Gameplay/Elements/Viewport/Content/Tutorial/Dropdown";

	private const string DROPDOWN_AUTOSAVE = "Gameplay/Elements/Viewport/Content/Autosave/Dropdown";

	private const string DROPDOWN_DELIVERY_NOTIFS = "Gameplay/Elements/Viewport/Content/Notifications/Dropdown";

	private const string BUTTON_RESET_HINTS = "Gameplay/Elements/Viewport/Content/Reset Hints/Button";

	private const string AUDIO = "Audio/Elements/Viewport/Content";

	private const string MUSIC_SLIDER = "Audio/Elements/Viewport/Content/Music/Slider";

	private const string MUSIC_LABEL = "Audio/Elements/Viewport/Content/Music/Label";

	private const string AMBIENT_SLIDER = "Audio/Elements/Viewport/Content/Ambient/Slider";

	private const string AMBIENT_LABEL = "Audio/Elements/Viewport/Content/Ambient/Label";

	private const string UISFX_SLIDER = "Audio/Elements/Viewport/Content/UI Sounds/Slider";

	private const string UISFX_LABEL = "Audio/Elements/Viewport/Content/UI Sounds/Label";

	private const string CONTROL_ELEMENTS = "Controls/Elements/Viewport/Content";

	private const string ACTION_TEMPLATE = "Templates/Action";

	private const string ACTION_KEY_A = "Key A";

	private const string ACTION_KEY_B = "Key B";

	private const string ACTION_TEXT = "Label";

	private const string MODS_BTN_MORE = "Mods/Workshop";

	private const string MODS_BTN_DOCS = "Mods/Docs";

	private const string MODS_ELEMENTS = "Mods/Elements/Viewport/Content";

	private const string MODS_TEMPLATE = "Templates/Options Mod Row";

	private const string MODS_CARD_TEXT = "Label";

	private const string MODS_CARD_TOGGLE = "Checkbox";

	private const string MODS_CARD_STATUS = "Status/Text";

	private const string MODS_BTN_CREATE = "Status/Share Button";

	private const string MODS_BTN_UPDATE = "Status/Update Button";

	private const string MODS_BTN_FOLDER = "Status/Folder Button";

	private const string MODS_BTN_DELETE = "Status/Delete Button";

	private const string ABOUT_TEXT_FIELD = "About/Elements/Viewport/Content/Text";

	private int _resolutionIndex;

	private int _vSyncIndex;

	private int _detailIndex;

	private bool _enableCompute;

	private bool _enablePostProcessing;

	private bool _isFullscreen;

	private bool _isWeather;

	private bool _isTraffic;

	private float _volmusic;

	private float _volambient;

	private float _voluisfx;

	private bool _isDirty;

	private bool _isInitialized;

	private List<GameObject> actions = new List<GameObject>();

	public static List<KeyCode> ALLOWED_SHORTCUT_KEYS = new List<KeyCode>
	{
		KeyCode.Alpha0,
		KeyCode.Alpha1,
		KeyCode.Alpha2,
		KeyCode.Alpha3,
		KeyCode.Alpha4,
		KeyCode.Alpha5,
		KeyCode.Alpha6,
		KeyCode.Alpha7,
		KeyCode.Alpha8,
		KeyCode.Alpha9,
		KeyCode.Keypad0,
		KeyCode.Keypad1,
		KeyCode.Keypad2,
		KeyCode.Keypad3,
		KeyCode.Keypad4,
		KeyCode.Keypad5,
		KeyCode.Keypad6,
		KeyCode.Keypad7,
		KeyCode.Keypad8,
		KeyCode.Keypad9,
		KeyCode.KeypadDivide,
		KeyCode.KeypadEquals,
		KeyCode.KeypadMinus,
		KeyCode.KeypadMultiply,
		KeyCode.KeypadPeriod,
		KeyCode.KeypadPlus,
		KeyCode.A,
		KeyCode.B,
		KeyCode.C,
		KeyCode.D,
		KeyCode.E,
		KeyCode.F,
		KeyCode.G,
		KeyCode.H,
		KeyCode.I,
		KeyCode.J,
		KeyCode.K,
		KeyCode.L,
		KeyCode.M,
		KeyCode.N,
		KeyCode.O,
		KeyCode.P,
		KeyCode.Q,
		KeyCode.R,
		KeyCode.S,
		KeyCode.T,
		KeyCode.U,
		KeyCode.V,
		KeyCode.W,
		KeyCode.X,
		KeyCode.Y,
		KeyCode.Z,
		KeyCode.Comma,
		KeyCode.Period,
		KeyCode.Slash,
		KeyCode.Semicolon,
		KeyCode.Quote,
		KeyCode.LeftBracket,
		KeyCode.RightBracket,
		KeyCode.Backslash,
		KeyCode.BackQuote,
		KeyCode.Minus,
		KeyCode.Equals,
		KeyCode.DownArrow,
		KeyCode.UpArrow,
		KeyCode.LeftArrow,
		KeyCode.RightArrow,
		KeyCode.Space,
		KeyCode.Tab,
		KeyCode.F1,
		KeyCode.F2,
		KeyCode.F3,
		KeyCode.F4,
		KeyCode.F5,
		KeyCode.F6,
		KeyCode.F7,
		KeyCode.F8,
		KeyCode.F9,
		KeyCode.F10,
		KeyCode.F11,
		KeyCode.F12,
		KeyCode.F13,
		KeyCode.F14,
		KeyCode.F15
	};

	public override UIReference UIReference => UIElements.OptionsPopup;

	protected override void InitializeOnPush()
	{
		if (!_isInitialized)
		{
			_graphicsMenu = _panel.GetChild("Graphics");
			_audiosfxMenu = _panel.GetChild("Audio");
			_gameplayMenu = _panel.GetChild("Gameplay");
			_controlsMenu = _panel.GetChild("Controls");
			_modsMenu = _panel.GetChild("Mods");
			_aboutMenu = _panel.GetChild("About");
			_graphicsTab = _panel.GetToggle("Tabs/Graphics");
			_gameplayTab = _panel.GetToggle("Tabs/Gameplay");
			_audiosfxTab = _panel.GetToggle("Tabs/Audio");
			_controlsTab = _panel.GetToggle("Tabs/Controls");
			_modsTab = _panel.GetToggle("Tabs/Mods");
			_aboutTab = _panel.GetToggle("Tabs/About");
			_modsTab.transform.gameObject.SetActive(Game.settings.IsModdingEnabled);
			_graphicsTab.onValueChanged.SetListener(delegate(bool isOn)
			{
				OnTabSwitch(isOn, _graphicsMenu, ApplyGraphicsSettings, LoadGraphics);
			});
			_gameplayTab.onValueChanged.SetListener(delegate(bool isOn)
			{
				OnTabSwitch(isOn, _gameplayMenu, ApplyGameplaySettings, LoadGameplay);
			});
			_audiosfxTab.onValueChanged.SetListener(delegate(bool isOn)
			{
				OnTabSwitch(isOn, _audiosfxMenu, ApplyAudioSfxSettings, LoadAudioSfx);
			});
			_controlsTab.onValueChanged.SetListener(delegate(bool isOn)
			{
				OnTabSwitch(isOn, _controlsMenu, ApplyControlsSettings, LoadControls);
			});
			_modsTab.onValueChanged.SetListener(delegate(bool isOn)
			{
				OnTabSwitch(isOn, _modsMenu, NoOp, LoadMods);
			});
			_aboutTab.onValueChanged.SetListener(delegate(bool isOn)
			{
				OnTabSwitch(isOn, _aboutMenu, NoOp, NoOp);
			});
			_fullscreen = _panel.GetToggle("Graphics/Elements/Viewport/Content/Fullscreen/Toggle");
			_resolution = _panel.GetDropdown("Graphics/Elements/Viewport/Content/Resolution/Dropdown");
			_vSync = _panel.GetDropdown("Graphics/Elements/Viewport/Content/VSync/Dropdown");
			_compute = _panel.GetDropdown("Graphics/Elements/Viewport/Content/Compute/Dropdown");
			_detail = _panel.GetDropdown("Graphics/Elements/Viewport/Content/Detail/Dropdown");
			_postProcessing = _panel.GetDropdown("Graphics/Elements/Viewport/Content/Post Processing/Dropdown");
			_weather = _panel.GetToggle("Graphics/Elements/Viewport/Content/Weather/Toggle");
			_traffic = _panel.GetToggle("Graphics/Elements/Viewport/Content/Traffic/Toggle");
			_language = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Language/Dropdown");
			_timeOfDay = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/TimeOfDay/Dropdown");
			_seasons = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Seasons/Dropdown");
			_numFormat = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Numbers/Dropdown");
			_volFormat = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Volumes/Dropdown");
			_dateFormat = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Dates/Dropdown");
			_tutorial = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Tutorial/Dropdown");
			_uiscale = _panel.GetDropdown("Graphics/Elements/Viewport/Content/UI Scale/Dropdown");
			_autosaveTime = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Autosave/Dropdown");
			_deliveryNotifs = _panel.GetDropdown("Gameplay/Elements/Viewport/Content/Notifications/Dropdown");
			_musicSlider = _panel.GetSlider("Audio/Elements/Viewport/Content/Music/Slider");
			_musicLabel = _panel.GetText("Audio/Elements/Viewport/Content/Music/Label");
			_ambientSlider = _panel.GetSlider("Audio/Elements/Viewport/Content/Ambient/Slider");
			_ambientLabel = _panel.GetText("Audio/Elements/Viewport/Content/Ambient/Label");
			_uiSfxSlider = _panel.GetSlider("Audio/Elements/Viewport/Content/UI Sounds/Slider");
			_uiSfxLabel = _panel.GetText("Audio/Elements/Viewport/Content/UI Sounds/Label");
			LoadGraphics();
			LoadGameplay();
			LoadAudioSfx();
			LoadControls();
			LoadMods();
			_fullscreen.onValueChanged.SetListener(OnToggleFullscreen);
			_resolution.onValueChanged.SetListener(OnResolutionChanged);
			_uiscale.onValueChanged.SetListener(OnUIScale);
			_vSync.onValueChanged.SetListener(OnVSyncChanged);
			_compute.onValueChanged.SetListener(OnComputeChanged);
			_detail.onValueChanged.SetListener(OnDetailChanged);
			_postProcessing.onValueChanged.SetListener(OnPostProcessingChanged);
			_weather.onValueChanged.SetListener(OnToggleWeather);
			_traffic.onValueChanged.SetListener(OnToggleTraffic);
			_language.onValueChanged.SetListener(MarkAsDirty);
			_timeOfDay.onValueChanged.SetListener(MarkAsDirty);
			_seasons.onValueChanged.SetListener(MarkAsDirty);
			_numFormat.onValueChanged.SetListener(MarkAsDirty);
			_volFormat.onValueChanged.SetListener(MarkAsDirty);
			_dateFormat.onValueChanged.SetListener(MarkAsDirty);
			_tutorial.onValueChanged.SetListener(MarkAsDirty);
			_panel.SetButtonListener("Gameplay/Elements/Viewport/Content/Reset Hints/Button", OnResetHintsClick);
			_musicSlider.onValueChanged.SetListener(OnMusicOrSoundSlider);
			_ambientSlider.onValueChanged.SetListener(OnMusicOrSoundSlider);
			_uiSfxSlider.onValueChanged.SetListener(OnMusicOrSoundSlider);
			_panel.SetButtonListener("Mods/Docs", OnModsDocsClick);
			_panel.SetButtonListener("Mods/Workshop", OnModsMoreClick);
			_panel.SetButtonListener("Close Button", Close);
			_panel.SetButtonListener("Graphics/Apply", ApplyGraphicsSettings);
			_panel.SetButtonListener("Gameplay/Apply", ApplyGameplaySettings);
			_panel.SetButtonListener("Audio/Apply", ApplyAudioSfxSettings);
			_panel.SetButtonListener("Controls/Apply", ApplyControlsSettings);
			_isInitialized = true;
			Game.serv.loc.OnLanguageChanged.Add(OnLanguageChanged);
		}
		static void NoOp()
		{
		}
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		if (pushed)
		{
			bool flag = Game.ctx == null;
			Toggle obj = (flag ? _gameplayTab : _graphicsTab);
			obj.SetIsOnWithoutNotify(value: false);
			obj.isOn = true;
			_compute.interactable = flag;
			_gameplayTab.interactable = flag;
			_modsTab.interactable = flag;
			_compute.GetComponent<MouseoverTrigger>().enabled = !flag;
			_gameplayTab.GetComponent<MouseoverTrigger>().enabled = !flag;
			_modsTab.GetComponent<MouseoverTrigger>().enabled = !flag;
		}
	}

	private void OnTabSwitch(bool isOn, GameObject tab, Action ok, Action cancel)
	{
		if (isOn)
		{
			tab.SetActive(value: true);
			return;
		}
		tab.SetActive(value: false);
		if (_isDirty)
		{
			OkPopup.ShowOkCancel(Loc.Get("ui.options.savechanges.body"), Loc.Get("ui.options.savechanges.apply"), Loc.Get("ui.options.savechanges.revert"), ok, cancel);
		}
	}

	private Resolution[] GetValidResolutions()
	{
		Resolution[] source = Screen.resolutions.Where((Resolution r) => r.height >= 720).ToArray();
		source = (from x in source
			group x by new { x.height, x.width } into x
			select x.First()).ToArray();
		if (source.Length == 0)
		{
			source = Screen.resolutions.ToArray();
		}
		return source;
	}

	private void LoadGraphics()
	{
		GamePreferences game = Game.serv.saveload.prefs.game;
		_isFullscreen = game.fullscreen;
		_fullscreen.SetIsOnWithoutNotify(_isFullscreen);
		_isWeather = game.weatherEnabled;
		_weather.SetIsOnWithoutNotify(_isWeather);
		_isTraffic = game.trafficEnabled;
		_traffic.SetIsOnWithoutNotify(_isTraffic);
		Resolution[] validResolutions = GetValidResolutions();
		List<string> names = validResolutions.Select((Resolution r) => Loc.Get("ui.options.resolution.format", "width", r.width, "height", r.height)).ToList();
		_resolution.SetOptions(names);
		int width = game.width;
		int height = game.height;
		for (int num = 0; num < validResolutions.Length; num++)
		{
			if (validResolutions[num].width == width && validResolutions[num].height == height)
			{
				_resolution.value = num;
				break;
			}
		}
		_resolutionIndex = _resolution.value;
		int maxscale = CameraService.FindMaxUIScale(width, height);
		List<int> uI_SCALES = CameraService.UI_SCALES;
		List<string> names2 = uI_SCALES.SelectIntoNewList((int n) => TextUtil.ColorEnabledIf(n <= maxscale, Loc.Percentage((float)n / 100f)));
		_uiscale.SetOptions(names2);
		int num2 = uI_SCALES.IndexOf(game.uiscale);
		if (num2 < 0)
		{
			num2 = uI_SCALES.IndexOf(100);
		}
		_uiscale.SetValueWithoutNotify(num2);
		_vSyncIndex = game.vsync;
		_vSync.SetOptions(Loc.Get("ui.options.vsync.off"), Loc.Get("ui.options.vsync.everyframe"), Loc.Get("ui.options.vsync.everytwoframes"));
		_vSync.SetValueWithoutNotify(_vSyncIndex);
		bool isIndirectInstancingAvailable = Game.serv.camera.IsIndirectInstancingAvailable;
		_enableCompute = isIndirectInstancingAvailable && game.computeShaders;
		_compute.SetOptions(Loc.Get("ui.options.compute.on"), Loc.Get("ui.options.compute.off"));
		_compute.SetValueWithoutNotify((!_enableCompute) ? 1 : 0);
		_compute.interactable = isIndirectInstancingAvailable;
		List<string> list = new List<string>();
		for (int num3 = 0; num3 < QualitySettings.names.Length; num3++)
		{
			list.Add(Loc.Get($"ui.options.quality{num3}"));
		}
		_detailIndex = QualitySettings.GetQualityLevel();
		_detail.SetOptions(list);
		_detail.SetValueWithoutNotify(_detailIndex);
		_enablePostProcessing = game.postEnabled;
		_postProcessing.SetOptions(Loc.Get("ui.options.postprocessing.on"), Loc.Get("ui.options.postprocessing.off"));
		_postProcessing.SetValueWithoutNotify((!_enablePostProcessing) ? 1 : 0);
		_isDirty = false;
	}

	private void LoadGameplay()
	{
		GamePreferences game = Game.serv.saveload.prefs.game;
		List<LanguageChoice> languages = Game.serv.loc.GetLanguages();
		UIUtil.SetOptions(names: languages.SelectIntoNewList((LanguageChoice lang) => lang.langname), dropdown: _language);
		string currentLang = Game.serv.loc.currentLang;
		int valueWithoutNotify = languages.SelectIntoNewList((LanguageChoice lang) => lang.langid).IndexOf(currentLang);
		_language.SetValueWithoutNotify(valueWithoutNotify);
		Populate<GamePreferences.TimeOfDayOption>(_timeOfDay, (int)game.timeofday, "ui.options.timeofday", GamePreferences.ALL_TIMES_OF_DAY);
		Populate<GamePreferences.SeasonsOption>(_seasons, (int)game.seasons, "ui.options.seasons", GamePreferences.ALL_SEASONS);
		Populate<GamePreferences.TutorialType>(_tutorial, (int)game.tutorial, "ui.options.tutorial", GamePreferences.ALL_TUTORIALS);
		Populate<GamePreferences.NumberFormat>(_numFormat, (int)game.numformat, "ui.options.numformat", GamePreferences.ALL_NUMFORMATS);
		Populate<GamePreferences.VolumeFormat>(_volFormat, (int)game.volformat, "ui.options.volformat", GamePreferences.ALL_VOLFORMATS);
		Populate<GamePreferences.DateFormat>(_dateFormat, (int)game.dateformat, "ui.options.dates", GamePreferences.ALL_DATEFORMATS);
		Populate<GamePreferences.AutosaveType>(_autosaveTime, (int)game.autosaveType, "ui.options.autosave", GamePreferences.ALL_AUTOSAVE);
		Populate<GamePreferences.DeliveryNotifSettings>(_deliveryNotifs, (int)game.deliveryNotifType, "ui.options.delivery-notifs", GamePreferences.ALL_DELIVERY_NOTIF_TYPES);
		_isDirty = false;
		static void Populate<T>(TMP_Dropdown dropdown, int currentValue, string keyroot, List<T> enums)
		{
			List<int> list = enums.Cast<int>().ToList();
			List<string> names = list.SelectIntoNewList((int val) => Loc.GetPluralized(keyroot, val));
			int valueWithoutNotify2 = MathUtil.ClampMin(list.IndexOf(currentValue), 0);
			dropdown.SetOptions(names);
			dropdown.SetValueWithoutNotify(valueWithoutNotify2);
		}
	}

	private void LoadAudioSfx()
	{
		_musicSlider.SetValueWithoutNotify(Game.serv.saveload.prefs.game.volmusic);
		_ambientSlider.SetValueWithoutNotify(Game.serv.saveload.prefs.game.volambient);
		_uiSfxSlider.SetValueWithoutNotify(Game.serv.saveload.prefs.game.voluisfx);
		SetAudioVolumesFromSliders();
		_isDirty = false;
	}

	private void LoadControls()
	{
		_keysToReassign.Clear();
		foreach (GameObject action in actions)
		{
			UnityEngine.Object.Destroy(action);
		}
		GameObject child = _panel.GetChild("Templates/Action");
		GameObject child2 = _panel.GetChild("Controls/Elements/Viewport/Content");
		List<KeyMappingTuple> mappings = Game.serv.keyboard.keymapper.mappings;
		int i = 0;
		for (int count = mappings.Count; i < count; i++)
		{
			KeyMappingTuple entry = mappings[i];
			GameObject gameObject = UnityEngine.Object.Instantiate(child, child2.transform);
			actions.Add(gameObject);
			TMP_InputField keyA = gameObject.GetChild("Key A").GetComponent<TMP_InputField>();
			TMP_InputField keyB = gameObject.GetChild("Key B").GetComponent<TMP_InputField>();
			TextMeshProUGUI text = gameObject.GetText("Label");
			keyA.text = Loc.GetKey(entry.mainkey);
			keyB.text = Loc.GetKey(entry.altkey);
			text.text = Loc.GetAction(entry.action);
			keyA.onSelect.AddListener(delegate
			{
				_isDirty = true;
				currentKeyToReassign = new KeyHandlerInfo
				{
					inputField = keyA,
					isMain = true,
					action = entry
				};
			});
			keyB.onSelect.AddListener(delegate
			{
				_isDirty = true;
				currentKeyToReassign = new KeyHandlerInfo
				{
					inputField = keyB,
					isMain = false,
					action = entry
				};
			});
		}
		_isDirty = false;
	}

	private void LoadMods()
	{
		List<ModMetadata> allModDefinitions = Game.serv.mods.GetAllModDefinitions();
		GameObject child = _panel.GetChild("Templates/Options Mod Row");
		GameObject child2 = _panel.GetChild("Mods/Elements/Viewport/Content");
		child2.EnsureChildCount(1, child);
		foreach (ModMetadata item in allModDefinitions)
		{
			GameObject card = UnityEngine.Object.Instantiate(child, child2.transform);
			InitializeModRow(card, item);
		}
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.loc.OnLanguageChanged.Remove(OnLanguageChanged);
		_graphicsTab.onValueChanged.RemoveAllListeners();
		_gameplayTab.onValueChanged.RemoveAllListeners();
		_audiosfxTab.onValueChanged.RemoveAllListeners();
		_controlsTab.onValueChanged.RemoveAllListeners();
		_modsTab.onValueChanged.RemoveAllListeners();
		_aboutTab.onValueChanged.RemoveAllListeners();
		_fullscreen.onValueChanged.RemoveAllListeners();
		_resolution.onValueChanged.RemoveAllListeners();
		_vSync.onValueChanged.RemoveAllListeners();
		_compute.onValueChanged.RemoveAllListeners();
		_detail.onValueChanged.RemoveAllListeners();
		_postProcessing.onValueChanged.RemoveAllListeners();
		_weather.onValueChanged.RemoveAllListeners();
		_traffic.onValueChanged.RemoveAllListeners();
		_language.onValueChanged.RemoveAllListeners();
		_timeOfDay.onValueChanged.RemoveAllListeners();
		_seasons.onValueChanged.RemoveAllListeners();
		_numFormat.onValueChanged.RemoveAllListeners();
		_volFormat.onValueChanged.RemoveAllListeners();
		_dateFormat.onValueChanged.RemoveAllListeners();
		_tutorial.onValueChanged.RemoveAllListeners();
		_uiscale.onValueChanged.RemoveAllListeners();
		_panel.GetButton("Gameplay/Elements/Viewport/Content/Reset Hints/Button").onClick.RemoveAllListeners();
		_musicSlider.onValueChanged.RemoveAllListeners();
		_ambientSlider.onValueChanged.RemoveAllListeners();
		_uiSfxSlider.onValueChanged.RemoveAllListeners();
		_panel.GetButton("Close Button").onClick.RemoveAllListeners();
		_panel.GetButton("Graphics/Apply").onClick.RemoveAllListeners();
		_panel.GetButton("Gameplay/Apply").onClick.RemoveAllListeners();
		_panel.GetButton("Audio/Apply").onClick.RemoveAllListeners();
		_panel.GetButton("Controls/Apply").onClick.RemoveAllListeners();
		foreach (GameObject action in actions)
		{
			UnityEngine.Object.Destroy(action);
		}
		actions.Clear();
		currentKeyToReassign = default(KeyHandlerInfo);
		_keyhandler.Reset();
		_keyhandler = null;
	}

	private void SetAudioVolumesFromSliders()
	{
		_volmusic = _musicSlider.value;
		_volambient = _ambientSlider.value;
		_voluisfx = _uiSfxSlider.value;
		Game.serv.audio.SetMusicVolume(_volmusic);
		Game.serv.audio.SetAmbientVolume(_volambient);
		Game.serv.audio.SetUiSfxVolume(_voluisfx);
		SetAudioLabelsFromVolumes();
	}

	private void SetAudioLabelsFromVolumes()
	{
		_musicLabel.text = Loc.Get("ui.options.volmusic", "value", Loc.Percentage(_volmusic));
		_ambientLabel.text = Loc.Get("ui.options.volambient", "value", Loc.Percentage(_volambient));
		_uiSfxLabel.text = Loc.Get("ui.options.voluisfx", "value", Loc.Percentage(_voluisfx));
	}

	private void ApplyGameplaySettings()
	{
		_isDirty = false;
		LanguageChoice languageChoice = Game.serv.loc.GetLanguages()[_language.value];
		GamePreferences game = Game.serv.saveload.prefs.game;
		game.SetLanguage(languageChoice.langid);
		game.timeofday = (GamePreferences.TimeOfDayOption)_timeOfDay.value;
		game.seasons = (GamePreferences.SeasonsOption)_seasons.value;
		game.tutorial = (GamePreferences.TutorialType)_tutorial.value;
		game.numformat = (GamePreferences.NumberFormat)_numFormat.value;
		game.dateformat = (GamePreferences.DateFormat)_dateFormat.value;
		game.volformat = (GamePreferences.VolumeFormat)_volFormat.value;
		game.SetAutosave((GamePreferences.AutosaveType)_autosaveTime.value);
		game.deliveryNotifType = (GamePreferences.DeliveryNotifSettings)_deliveryNotifs.value;
		Game.serv.saveload.SavePrefs();
		Game.serv.loc.OnGameplaySettingsChanged(languageChoice.langid);
		LoadGraphics();
		LoadGameplay();
	}

	private void ApplyGraphicsSettings()
	{
		_isDirty = false;
		Resolution[] validResolutions = GetValidResolutions();
		int num = MathUtil.Clamp(_resolutionIndex, 0, validResolutions.Length);
		Resolution resolution = validResolutions[num];
		int orDefaultFast = CameraService.UI_SCALES.GetOrDefaultFast(_uiscale.value, 100);
		GamePreferences game = Game.serv.saveload.prefs.game;
		game.SetResolution(resolution.width, resolution.height, _isFullscreen);
		game.SetFullscreen(_isFullscreen);
		game.width = resolution.width;
		game.height = resolution.height;
		game.fullscreen = _isFullscreen;
		game.weatherEnabled = _isWeather;
		game.trafficEnabled = _isTraffic;
		game.computeShaders = _enableCompute;
		game.SetUIScale(orDefaultFast);
		game.SetVSync(_vSyncIndex);
		game.SetPostFX(_enablePostProcessing);
		Game.serv.saveload.SavePrefs();
		QualitySettings.SetQualityLevel(_detailIndex);
		LoadGameplay();
	}

	private void ApplyAudioSfxSettings()
	{
		_isDirty = false;
		GamePreferences game = Game.serv.saveload.prefs.game;
		game.volmusic = _volmusic;
		game.volambient = _volambient;
		game.voluisfx = _voluisfx;
		Game.serv.saveload.SavePrefs();
	}

	private void ApplyControlsSettings()
	{
		foreach (KeyReassign item in _keysToReassign)
		{
			Game.serv.keyboard.ModifyMapping(item.action.action, item.newMainKey, item.newAltKey);
		}
		if (Game.serv.keyboard.keymapper.IsSameAsDefaults())
		{
			Game.serv.saveload.prefs.game.keymapper = null;
		}
		else
		{
			Game.serv.saveload.prefs.game.keymapper = Game.serv.keyboard.keymapper.Clone();
		}
		Game.serv.saveload.SavePrefs();
		_keysToReassign.Clear();
		_isDirty = false;
	}

	private void OnLanguageChanged()
	{
		SetAudioLabelsFromVolumes();
		LoadControls();
		LoadMods();
	}

	private void OnToggleFullscreen(bool value)
	{
		_isDirty = true;
		_isFullscreen = value;
	}

	private void OnToggleWeather(bool value)
	{
		_isDirty = true;
		_isWeather = value;
	}

	private void OnToggleTraffic(bool value)
	{
		_isDirty = true;
		_isTraffic = value;
	}

	private void OnResolutionChanged(int resolutionIndex)
	{
		_isDirty = true;
		_resolutionIndex = resolutionIndex;
	}

	private void OnVSyncChanged(int qualityIndex)
	{
		_isDirty = true;
		_vSyncIndex = qualityIndex;
	}

	private void OnComputeChanged(int index)
	{
		_isDirty = true;
		_enableCompute = index == 0;
	}

	private void OnDetailChanged(int detailsIndex)
	{
		_isDirty = true;
		_detailIndex = detailsIndex;
	}

	private void OnResetHintsClick()
	{
		OkPopup.ShowOkCancel(Loc.Get("ui.options.reset-hints-popup"), delegate
		{
			Game.serv.saveload.progress.hints.Clear();
			Game.serv.saveload.SaveProgress();
			TimerUtil.RunAfterTime(delegate
			{
				OkPopup.Show(Loc.Get("ui.options.reset-hints-done"));
			}, 0.1f);
		}, delegate
		{
		});
	}

	private void MarkAsDirty(int _)
	{
		_isDirty = true;
	}

	private void OnUIScale(int _index)
	{
		_isDirty = true;
	}

	private void OnMusicOrSoundSlider(float _value)
	{
		_isDirty = true;
		SetAudioVolumesFromSliders();
	}

	private void OnPostProcessingChanged(int postProcessingIndex)
	{
		_isDirty = true;
		_enablePostProcessing = postProcessingIndex == 0;
	}

	private void OnModsDocsClick()
	{
		Game.serv.mods.OnModsOrDocsClick(mods: false);
	}

	private void OnModsMoreClick()
	{
		Game.serv.mods.OnModsOrDocsClick(mods: true);
	}

	private void InitializeModRow(GameObject card, ModMetadata mod)
	{
		ModsService mods = Game.serv.mods;
		Toggle toggle = card.GetToggle("Checkbox");
		toggle.SetIsOnWithoutNotify(mods.IsModEnabled(mod.modid));
		toggle.onValueChanged.SetListener(delegate(bool on)
		{
			mods.SetModEnabled(mod.modid, on);
		});
		RefreshModRowText(card, mod);
		RefreshModRowButtons(card, mod);
		card.SetButtonListener("Status/Share Button", delegate
		{
			TryCreate(card, mod);
		});
		card.SetButtonListener("Status/Update Button", delegate
		{
			TryUpdate(card, mod);
		});
		card.SetButtonListener("Status/Delete Button", delegate
		{
			TryDelete(card, mod);
		});
		card.SetButtonListener("Status/Folder Button", delegate
		{
			Game.serv.mods.ShowModFolder(mod);
		});
	}

	private void RefreshModRowButtons(GameObject card, ModMetadata mod)
	{
		if (base.IsShowing)
		{
			card.SetActive("Status/Share Button", mod.IsLocalOnly);
			card.SetActive("Status/Update Button", mod.IsLocalShared);
			card.SetActive("Status/Delete Button", mod.IsLocalOnly || mod.IsLocalShared);
			card.SetActive("Status/Folder Button", mod.IsLocalOnly || mod.IsLocalShared);
			card.GetButton("Status/Share Button").interactable = true;
			card.GetButton("Status/Update Button").interactable = true;
			card.GetButton("Status/Delete Button").interactable = true;
			card.GetButton("Status/Folder Button").interactable = true;
		}
	}

	private void RefreshModRowText(GameObject card, ModMetadata mod)
	{
		if (base.IsShowing)
		{
			string text = Loc.Get("ui.options.mods.cardname", "name", mod.name, "desc", mod.GetDesc());
			string key = (mod.IsLocalOnly ? "ui.options.mods.status-local" : (mod.IsLocalShared ? "ui.options.mods.status-shared" : "ui.options.mods.status-remote"));
			card.SetText("Label", text);
			card.SetText("Status/Text", Loc.Get(key));
		}
	}

	private void TryCreate(GameObject card, ModMetadata mod)
	{
		if (!mod.IsLocalOnly && !mod.CanBeShared)
		{
			return;
		}
		UpdateShareProgress(card, mod, null, Loc.Get("ui.options.mods.status.creating"));
		Game.serv.mods.WorkshopLoader.Handler.CreateNewItem(mod, delegate(bool failure, string message)
		{
			UpdateShareProgress(card, mod, failure, "");
			if (failure)
			{
				RefreshModRowText(card, mod);
				OkPopup.Show(message);
			}
			else
			{
				Game.serv.mods.LocalLoader.Handler.SaveModDefinition(mod);
				TryUpdate(card, mod);
			}
		});
	}

	private void TryUpdate(GameObject card, ModMetadata mod)
	{
		UpdateShareProgress(card, mod, null, Loc.Get("ui.options.mods.status.uploading"));
		Game.serv.mods.WorkshopLoader.Handler.UploadItem(mod, delegate(bool failure, string message)
		{
			UpdateShareProgress(card, mod, failure, "");
			RefreshModRowButtons(card, mod);
			RefreshModRowText(card, mod);
			OkPopup.Show(message);
		});
	}

	private void TryDelete(GameObject _, ModMetadata mod)
	{
		bool num = mod.IsLocalOnly || mod.IsLocalShared;
		string key = (mod.IsLocalOnly ? "ui.options.mods.delete-local" : (mod.IsLocalShared ? "ui.options.mods.delete-shared" : "ui.options.mods.delete-remote"));
		if (num)
		{
			OkPopup.ShowOkCancel(Loc.Get(key), delegate
			{
				DoDelete();
			}, delegate
			{
			});
		}
		else
		{
			OkPopup.Show(Loc.Get(key));
		}
		void DoDelete()
		{
			Game.serv.mods.DeleteModPermanently(mod);
			LoadMods();
		}
	}

	private void UpdateShareProgress(GameObject card, ModMetadata mod, bool? failure, string message)
	{
		if (base.IsShowing)
		{
			card.GetButton("Status/Share Button").interactable = failure.HasValue;
			card.GetButton("Status/Update Button").interactable = failure.HasValue;
			card.GetButton("Status/Delete Button").interactable = failure.HasValue;
			card.GetButton("Status/Folder Button").interactable = failure.HasValue;
			card.SetActive("Status/Delete Button", mod.IsLocalOnly || mod.IsLocalShared);
			card.SetActive("Status/Folder Button", mod.IsLocalOnly || mod.IsLocalShared);
			card.SetText("Status/Text", message);
		}
	}

	internal void OnKeyboardHandler(KeyCode key)
	{
		if (currentKeyToReassign.IsValid && currentKeyToReassign.inputField.isFocused)
		{
			currentKeyToReassign.inputField.DeactivateInputField();
			bool flag = key != KeyCode.Escape && string.IsNullOrEmpty(currentKeyToReassign.inputField.text);
			ProcessKeyCodeAssignment(currentKeyToReassign.isMain, (!flag) ? key : KeyCode.None);
			currentKeyToReassign.inputField.text = Loc.GetKey(key);
		}
	}

	private void ProcessKeyCodeAssignment(bool isMain, KeyCode key)
	{
		KeyMappingTuple action = currentKeyToReassign.action;
		KeyCode? newMainKey = (isMain ? new KeyCode?(key) : ((KeyCode?)null));
		KeyCode? newAltKey = ((!isMain) ? new KeyCode?(key) : ((KeyCode?)null));
		_keysToReassign.Add(new KeyReassign
		{
			action = action,
			newMainKey = newMainKey,
			newAltKey = newAltKey
		});
		_isDirty = true;
	}

	internal bool ShouldCaptureKeypresses()
	{
		return false;
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new CustomKeyboardHandler(this);
	}
}
