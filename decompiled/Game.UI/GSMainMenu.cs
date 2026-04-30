using System.Collections.Generic;
using Game.Core;
using Game.Platform;
using Game.Services;
using Game.Services.Input;
using Game.Services.Maps;
using Game.Session;
using UnityEngine;

namespace Game.UI;

public sealed class GSMainMenu : GSBase, IKeyboardHandler
{
	private sealed class RightClickHandler : AbstractInputHandler
	{
		private GSMainMenu _mainmenu;

		public RightClickHandler(GSMainMenu mainmenu)
		{
			_mainmenu = mainmenu;
		}

		public override bool OnSecondary(Vector2 before, Vector2 after, InputPhase phase)
		{
			if (phase == InputPhase.ButtonEnded)
			{
				return _mainmenu.CloseTopPopup();
			}
			return false;
		}
	}

	private MainMenuPopup _popup;

	private KeyboardHandler _keyhandler;

	private IInputHandler _inputhandler;

	private bool _pendingSessionStart;

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		MainMenuPopup popup = new MainMenuPopup(null, ShowCustomGamePopup, PlayTutorial, LoadLatestGame, OptionsDialog, QuitGame);
		_popup = Game.serv.ui.AddPopup(popup);
		_keyhandler = MakeKeyHandler();
		Game.serv.keyboard.PushHandler(this);
		_inputhandler = new RightClickHandler(this);
		Game.serv.input.Push(_inputhandler);
		Game.serv.saveload.OnSavedPrefs.Add(OnSavedPrefs);
		if (!_pendingSessionStart)
		{
			Game.serv.audio.MusicStartMainMenu();
		}
	}

	public override void OnDeactivated(bool popped)
	{
		Game.serv.ui.RemovePopup(_popup);
		Game.serv.saveload.OnSavedPrefs.Remove(OnSavedPrefs);
		Game.serv.input.Pop();
		_inputhandler = null;
		Game.serv.keyboard.RemoveHandler(this);
		_keyhandler = null;
		base.OnDeactivated(popped);
	}

	private void OnSavedPrefs(string _)
	{
		_popup.RefreshTutorialButton();
	}

	public KeyboardHandler GetKeyHandler()
	{
		return _keyhandler;
	}

	private KeyboardHandler MakeKeyHandler()
	{
		return new BasicKeyboardHandler(KeyboardHandler.Priority.HighNonModalDialog, new List<KeyInput>
		{
			new KeyInput(KeyAction.Cancel, delegate
			{
				CloseTopPopup();
			})
		}, KeyboardHandler.Fallthrough.OnlyIfNotProcessed);
	}

	private bool CloseTopPopup()
	{
		if (Game.serv.ui.TopPopupUnsafe != _popup)
		{
			Game.serv.ui.RemoveTopPopup();
			return true;
		}
		return false;
	}

	private void PlayTutorial()
	{
		TutorialSettings tutorial = Game.serv.globals.settings.general.tutorial;
		PeepCreationDetails player = new PeepCreationDetails(EthnicitySettings.DEFAULT_ETHNICITY, Loc.Get(tutorial.firstName), Loc.Get(tutorial.lastName), Gender.M, Skin.Light);
		GameParameters parameters = new GameParameters
		{
			userrng = tutorial.rngseed,
			mapdef = tutorial.mapdef,
			playerdetails = new PlayerStartupDetails(player, tutorial: true, default(Label))
		};
		MapConfig map = Game.serv.globals.mapgen.FindBuiltInMapConfigByID(tutorial.mapdef);
		StartGameWithParameters(parameters, map);
	}

	private void ShowCustomGamePopup()
	{
		NewGameCityPopup popup = new NewGameCityPopup(StartGameWithParameters);
		Game.serv.ui.AddPopup(popup);
	}

	private void StartGameWithParameters(GameParameters parameters, MapConfig map)
	{
		ScenarioConfig scenario = ScenarioConfig.MakeForNewGame(parameters);
		Game.serv.screens.Add(new GSPrepareForSession(scenario, map, OnPrepareComplete));
	}

	private void LoadLatestGame()
	{
		Game.serv.ui.AddPopup(SaveLoadPopup.MakeForLoading(StartLoading));
	}

	private void OptionsDialog()
	{
		Game.serv.ui.AddPopup(new OptionsPopup());
	}

	private void StartLoading(IPlatformSaveSlotDescriptor latest)
	{
		Game.serv.screens.Add(new GSPrepareForSession(latest, OnPrepareComplete));
	}

	private void QuitGame()
	{
		Game.instance.Quit();
	}

	private void OnPrepareComplete(SessionPrepResults results)
	{
		_pendingSessionStart = true;
		Game.serv.screens.Pop();
		Game.serv.screens.Add(new GSStartSession(results));
		_pendingSessionStart = false;
	}
}
