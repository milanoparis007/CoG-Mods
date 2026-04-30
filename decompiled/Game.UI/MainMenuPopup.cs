using System;
using System.Collections.Generic;
using Game.Services;
using Game.UI.Mouseovers;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public sealed class MainMenuPopup : BasePopup
{
	private class DLCButtonContext : MonoBehaviour
	{
		public DLCSettings.Entry entry;

		public bool installed;

		public DLCButtonContext Set(DLCSettings.Entry entry, bool installed)
		{
			this.entry = entry;
			this.installed = installed;
			return this;
		}
	}

	public sealed class DLCButtonMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTRSmall;

		protected override string ProduceText()
		{
			return Loc.Get(context.GetComponent<DLCButtonContext>().entry.locname);
		}
	}

	private const string VER_TEXT = "Version/Text";

	private const string VER_SOMASIM = "Version/Button SomaSim";

	private const string VER_KASEDO = "Version/Button Kasedo";

	private const string VER_YOUTUBE = "Version/Button YouTube";

	private const string VER_DISCORD = "Version/Button Discord";

	private const string VER_TWITTER = "Version/Button Twitter";

	private const string VER_WEBSITE = "Version/Button Website";

	private const string BUTTONS = "Buttons";

	private const string NEWGAME_BUTTON = "Buttons/New Game Button";

	private const string TUTORIAL_BUTTON = "Buttons/Tutorial Button";

	private const string LOADGAME_BUTTON = "Buttons/Load Game Button";

	private const string SETTINGS_BUTTON = "Buttons/Settings Button";

	private const string QUIT_BUTTON = "Buttons/Quit Button";

	private const string DLC_BUTTON_CONTAINER = "Buttons/DLCs";

	private const string STEAMDEMO = "Steam Demo";

	private const string STEAMDEMO_BTN = "Steam Demo/Button";

	private const string ANNOUNCEMENT = "Announcement";

	private const string ANN_TEXT = "Announcement/Text";

	private const string ANN_BTN_STEAM = "Announcement/Button Steam";

	private const string ANN_BTN_CLOSE = "Announcement/Button Close";

	private const string ANN_BTN_HIDE = "Announcement/Button Hide";

	private const string QUICKSTARTS = "Quickstarts";

	private const string QUICKSTART_CHI = "Quickstarts/Chicago";

	private const string QUICKSTART_PIT = "Quickstarts/Pittsburgh";

	private const string QUICKSTART_DET = "Quickstarts/Detroit";

	private const string QUICKSTART_CIN = "Quickstarts/Cincinnati";

	private const string QUICKSTART_MINI = "Quickstarts/MiniMap";

	private Action<string> quickStart;

	private Action newGame;

	private Action tutorial;

	private Action loadGame;

	private Action settingsDialog;

	private Action quitGame;

	private static bool _hasShownAnnouncements;

	public override UIReference UIReference => UIElements.MainMenu;

	public MainMenuPopup(Action<string> quickStart, Action newGame, Action tutorial, Action loadGame, Action settingsDialog, Action quitGame)
	{
		this.quickStart = quickStart;
		this.newGame = newGame;
		this.tutorial = tutorial;
		this.loadGame = loadGame;
		this.settingsDialog = settingsDialog;
		this.quitGame = quitGame;
	}

	protected override void InitializeOnPush()
	{
		_panel.SetText("Version/Text", GetMainMenuFooter());
		_panel.SetButtonListener("Version/Button SomaSim", delegate
		{
			Application.OpenURL("http://somasim.com");
		});
		_panel.SetButtonListener("Version/Button Kasedo", delegate
		{
			Application.OpenURL("http://kasedogames.com");
		});
		_panel.SetButtonListener("Version/Button YouTube", delegate
		{
			Application.OpenURL("https://www.youtube.com/watch?v=iJqSSHfLoWA&list=PL-AySLCs87QiGmOGJLxLocK6pz2j2Rwwv");
		});
		_panel.SetButtonListener("Version/Button Twitter", delegate
		{
			Application.OpenURL("https://twitter.com/CityOfGangsters");
		});
		_panel.SetButtonListener("Version/Button Discord", delegate
		{
			Application.OpenURL("https://discord.gg/CityofGangsters");
		});
		_panel.SetButtonListener("Version/Button Website", delegate
		{
			Application.OpenURL("https://www.kasedogames.com/cityofgangsters");
		});
		_panel.SetButtonListener("Buttons/New Game Button", newGame);
		_panel.SetButtonListener("Buttons/Tutorial Button", tutorial);
		_panel.SetButtonListener("Buttons/Load Game Button", loadGame);
		_panel.SetButtonListener("Buttons/Settings Button", settingsDialog);
		_panel.SetButtonListener("Buttons/Quit Button", quitGame);
		RefreshTutorialButton();
		RefreshDLCButtons();
		RefreshAnnouncement();
		if (Game.settings.IsExpired)
		{
			string[] array = new string[3] { "Buttons/New Game Button", "Buttons/Load Game Button", "Buttons/Settings Button" };
			foreach (string path in array)
			{
				_panel.SetActive(path, value: false);
			}
		}
		_panel.SetActive("Quickstarts", value: false);
		_panel.SetButtonListener("Quickstarts/Chicago", delegate
		{
			quickStart("chicago");
		});
		_panel.SetButtonListener("Quickstarts/Pittsburgh", delegate
		{
			quickStart("pittsburgh");
		});
		_panel.SetButtonListener("Quickstarts/Detroit", delegate
		{
			quickStart("detroit");
		});
		_panel.SetButtonListener("Quickstarts/Cincinnati", delegate
		{
			quickStart("cincinnati");
		});
		_panel.SetButtonListener("Quickstarts/MiniMap", delegate
		{
			quickStart("minitest");
		});
		_panel.SetActive("Steam Demo", value: false);
		_panel.SetButtonListener("Steam Demo/Button", delegate
		{
			Game.serv.store.handler.OpenStorePage();
		});
		Game.serv.mouseovers.Register(MouseoverType.MainMenuDLCButton, new DLCButtonMouseover());
		Game.serv.remotesettings.OnInitializationFinished.Add(OnRemoteSettings);
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.remotesettings.OnInitializationFinished.Remove(OnRemoteSettings);
		Game.serv.mouseovers.Unregister(MouseoverType.MainMenuDLCButton);
		quickStart = null;
		newGame = null;
		loadGame = null;
		quitGame = null;
	}

	private string GetMainMenuFooter()
	{
		string text = "";
		if (Game.settings.IsExpired)
		{
			text += Loc.Get("ui.mainmenu.expired", "exp", Game.settings.ExpirationDate.ToShortDateString());
			text += " - ";
		}
		return text + Loc.Get("ui.mainmenu.version", "year", "2021, 2022", "ver", GameSettings.GetVersionString(), "build", GameSettings.build);
	}

	public void RefreshTutorialButton()
	{
		_panel.SetActive("Buttons/Tutorial Button", Game.serv.saveload.prefs.game.CanShowTutorial);
	}

	private void OnRemoteSettings()
	{
		RefreshAnnouncement();
	}

	private void RefreshAnnouncement()
	{
		RemoteSettingsDefinition.Announcement ann = Game.serv.remotesettings.FindRandomAnnouncementOrNull();
		bool flag = ann != null && !_hasShownAnnouncements;
		_panel.SetActive("Announcement", flag);
		if (flag)
		{
			_panel.SetText("Announcement/Text", Loc.Get(ann.loc));
			string text = null;
			string text2 = null;
			string text3 = null;
			RemoteSettingsDefinition.AnnouncementButtons buttons = ann.buttons;
			if (buttons != RemoteSettingsDefinition.AnnouncementButtons.Ok && buttons == RemoteSettingsDefinition.AnnouncementButtons.Steam)
			{
				text = Loc.Get("announce.button-steam");
				text2 = Loc.Get("announce.button-close");
				text3 = Loc.Get("announce.button-hide");
			}
			else
			{
				text3 = Loc.Get("announce.button-ok");
			}
			_panel.SetChildText("Announcement/Button Steam", text);
			_panel.SetChildText("Announcement/Button Close", text2);
			_panel.SetChildText("Announcement/Button Hide", text3);
			_panel.SetActive("Announcement/Button Steam", text != null);
			_panel.SetActive("Announcement/Button Close", text2 != null);
			_panel.SetActive("Announcement/Button Hide", text3 != null);
			_panel.SetButtonListener("Announcement/Button Steam", delegate
			{
				ProcessClick(openStore: true, hideAnnouncement: true);
			});
			_panel.SetButtonListener("Announcement/Button Close", delegate
			{
				ProcessClick(openStore: false, hideAnnouncement: false);
			});
			_panel.SetButtonListener("Announcement/Button Hide", delegate
			{
				ProcessClick(openStore: false, hideAnnouncement: true);
			});
			_hasShownAnnouncements = true;
		}
		void ProcessClick(bool openStore, bool hideAnnouncement)
		{
			if (openStore)
			{
				Game.serv.store.handler.OpenStorePage();
			}
			if (hideAnnouncement)
			{
				Game.serv.remotesettings.HideAnnouncement(ann.id);
			}
			_panel.SetActive("Announcement", value: false);
		}
	}

	private List<(DLCSettings.Entry, bool)> GetDLCSettings()
	{
		return Game.serv.globals.settings.general.dlcs.entries.SelectIntoNewList((DLCSettings.Entry e) => (e: e, Game.serv.store.IsPackInstalled(e.packid)));
	}

	private void RefreshDLCButtons()
	{
		bool flag = true;
		_panel.SetActive("Buttons/DLCs", flag);
		if (flag)
		{
			GameObject child = _panel.GetChild("Buttons/DLCs");
			GameObject gameObject = child.transform.GetChild(0).gameObject;
			List<(DLCSettings.Entry, bool)> dLCSettings = GetDLCSettings();
			child.EnsureChildCount(dLCSettings, gameObject);
			child.InitializeChildren(dLCSettings, Init);
		}
	}

	private void Init(int index, GameObject card, (DLCSettings.Entry entry, bool installed) tuple)
	{
		DLCButtonContext ctx = card.GetOrAddComponent<DLCButtonContext>().Set(tuple.entry, tuple.installed);
		string message = Loc.Get(tuple.entry.locicon);
		card.GetChildText().SetText(TextUtil.ColorIf(!tuple.installed, message, "#ffffff60"));
		card.GetComponent<Button>().onClick.SetListener(delegate
		{
			OnClick(ctx);
		});
	}

	private void OnClick(DLCButtonContext ctx)
	{
		Game.serv.store.handler.OpenStorePageForDLC(ctx.entry.packid);
	}
}
