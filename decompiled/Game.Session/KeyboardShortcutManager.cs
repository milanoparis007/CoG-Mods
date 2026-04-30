using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services.Input;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.HUD;
using UnityEngine;

namespace Game.Session;

public class KeyboardShortcutManager : AbstractSessionManager, IKeyboardHandler
{
	private bool _cheatKeysEnabled;

	private BasicKeyboardHandler _keyhandler;

	private List<KeyInput> _standard;

	private List<KeyInput> _cheats;

	private static bool IsShiftDown => KeyUtil.IsShiftDown;

	private static bool IsCtrlDown => KeyUtil.IsCtrlDown;

	private static bool IsAltDown => KeyUtil.IsAltDown;

	public KeyboardHandler GetKeyHandler()
	{
		return _keyhandler;
	}

	public override void OnInitializeDone()
	{
		_standard = new List<KeyInput>
		{
			new KeyInput(KeyAction.FinishTurn, delegate
			{
				Game.ctx.players.FinishActivePlayerTurn();
			}),
			new KeyInput(KeyAction.AdvanceToNextCrew, delegate
			{
				Game.ctx.players.AdvanceToNextCrew(IsShiftDown);
			}),
			new KeyInput(KeyAction.Cancel, delegate
			{
				Game.ctx.selection.HandleClosePopupOrDeselect(fromKeyboard: true);
			}),
			new KeyInput(KeyAction.HideUI, delegate
			{
				if (IsShiftDown && IsCtrlDown)
				{
					Game.serv.ui.ToggleCanvases(all: false);
				}
				else if (IsShiftDown)
				{
					Game.ctx.hud.picks.CheatTogglePicks();
				}
				else if (IsCtrlDown)
				{
					PlayerTerritoryDisplay.ToggleBorders();
				}
				else
				{
					Game.serv.ui.ToggleCanvases(all: true);
				}
			}),
			new KeyInput(KeyAction.Quicksave, delegate
			{
				if (IsShiftDown)
				{
					Game.ctx.AutosaveGame();
				}
				else
				{
					Game.ctx.ShowSaveDialog();
				}
			}),
			new KeyInput(KeyAction.CheatConsole, delegate
			{
				Game.ctx.hud.console.Toggle();
			}),
			new KeyInput(KeyAction.Select1, delegate
			{
				SelectDriver(1);
			}),
			new KeyInput(KeyAction.Select2, delegate
			{
				SelectDriver(2);
			}),
			new KeyInput(KeyAction.Select3, delegate
			{
				SelectDriver(3);
			}),
			new KeyInput(KeyAction.Select4, delegate
			{
				SelectDriver(4);
			}),
			new KeyInput(KeyAction.Select5, delegate
			{
				SelectDriver(5);
			}),
			new KeyInput(KeyAction.Select6, delegate
			{
				SelectDriver(6);
			}),
			new KeyInput(KeyAction.Select7, delegate
			{
				SelectDriver(7);
			}),
			new KeyInput(KeyAction.Select8, delegate
			{
				SelectDriver(8);
			}),
			new KeyInput(KeyAction.Select9, delegate
			{
				SelectDriver(9);
			}),
			new KeyInput(KeyAction.Select0, delegate
			{
				SelectDriver(10);
			}),
			new KeyInput(KeyAction.BuildingSelect1, delegate
			{
				SelectBuilding(0);
			}),
			new KeyInput(KeyAction.BuildingSelect2, delegate
			{
				SelectBuilding(1);
			}),
			new KeyInput(KeyAction.BuildingSelect3, delegate
			{
				SelectBuilding(2);
			}),
			new KeyInput(KeyAction.BuildingSelect4, delegate
			{
				SelectBuilding(3);
			}),
			new KeyInput(KeyAction.BuildingSelect5, delegate
			{
				SelectBuilding(4);
			}),
			new KeyInput(KeyAction.BuildingSelect6, delegate
			{
				SelectBuilding(5);
			}),
			new KeyInput(KeyAction.BuildingSelect7, delegate
			{
				SelectBuilding(6);
			}),
			new KeyInput(KeyAction.BuildingSelect8, delegate
			{
				SelectBuilding(7);
			}),
			new KeyInput(KeyAction.BuildingSelect9, delegate
			{
				SelectBuilding(8);
			}),
			new KeyInput(KeyAction.BuildingSelect10, delegate
			{
				SelectBuilding(9);
			}),
			new KeyInput(KeyAction.OverlayHeat, delegate
			{
				OpenHeat();
			}),
			new KeyInput(KeyAction.OverlayResources, delegate
			{
				OpenResources();
			}),
			new KeyInput(KeyAction.OverlayRespect, delegate
			{
				OpenRespect();
			}),
			new KeyInput(KeyAction.OpenOrgChart, delegate
			{
				OpenOrgChart();
			})
		};
		_cheats = new List<KeyInput>
		{
			new KeyInput(KeyCode.H, delegate
			{
				Game.ctx.board.terrain.UpdateColors(forceHeatmapUpdate: true);
			}),
			new KeyInput(KeyCode.M, delegate
			{
				Price delta = new Price(IsShiftDown ? (-1000) : 1000);
				Game.ctx.players.Human.finances.DoChangeMoneyOnPlayerPeep(delta, MoneyReason.Other);
			}),
			new KeyInput(KeyCode.P, delegate
			{
				_ = IsCtrlDown;
			}),
			new KeyInput(KeyCode.L, delegate
			{
				Game.ctx.RequestQuit();
			}),
			new KeyInput(KeyCode.KeypadPlus, delegate
			{
				if (IsShiftDown && IsCtrlDown)
				{
					ModifyUIScale(25);
				}
			}),
			new KeyInput(KeyCode.KeypadMinus, delegate
			{
				if (IsShiftDown && IsCtrlDown)
				{
					ModifyUIScale(-25);
				}
			})
		};
	}

	private void OpenResources()
	{
		Game.ctx.overlays.HideAnyOverlay();
		Game.ctx.hud.resourcesBar.Toggle();
	}

	private void OpenHeat()
	{
		OpenOverlay("OB Heat");
	}

	private void OpenRespect()
	{
		OpenOverlay("OB Respect");
	}

	private void OpenOverlay(string name)
	{
		List<HUDDialogItemDefBase> defs = OverlaysBarItems.GetDefs("main");
		defs = defs.Where((HUDDialogItemDefBase x) => x.spriteName == name).ToList();
		Game.ctx.hud.overlaysBar.Show();
		Game.ctx.hud.overlaysBar.ShowOrHide((HUDDialogButtonDef)defs[0]);
	}

	private void OpenOrgChart()
	{
		if (Game.serv.ui.ContainsPopup<OrgChartPopup>() && !(Game.serv.ui.TopPopupUnsafe is OrgChartPopup))
		{
			Game.serv.ui.RemovePopup<OrgChartPopup>();
		}
		if (!(Game.serv.ui.TopPopupUnsafe is OrgChartPopup))
		{
			Game.serv.ui.AddPopup(new OrgChartPopup());
		}
	}

	private void SelectDriver(int num)
	{
		Game.ctx?.players?.AdvanceToSpecificCrew(num, IsShiftDown);
	}

	private void SelectBuilding(int num)
	{
		List<EntityID> allControlledBuildingsUnsafe = Game.ctx.players.Human.territory.GetAllControlledBuildingsUnsafe();
		if (num < allControlledBuildingsUnsafe.Count)
		{
			Game.ctx.selection.HandleClosePopupOrDeselect(fromKeyboard: false);
			PersonInfoUtil.TweenCameraToEntity(allControlledBuildingsUnsafe[num].FindEntity());
		}
	}

	private void ModifyUIScale(int percent)
	{
		int num = Game.serv.saveload.prefs.game.uiscale + percent;
		if (num > 0)
		{
			Game.serv.saveload.prefs.game.SetUIScale(num);
			Game.serv.saveload.SavePrefs();
		}
	}

	public override void OnPreInteractive()
	{
		_cheatKeysEnabled = Game.settings.IsEditor;
		List<KeyInput> list = new List<KeyInput>();
		list.AddRange(_standard);
		if (_cheatKeysEnabled)
		{
			list.AddRange(_cheats);
		}
		_keyhandler = new BasicKeyboardHandler(KeyboardHandler.Priority.LowKeyboardShortcuts, list, KeyboardHandler.Fallthrough.Always);
		Game.serv.keyboard.PushHandler(this);
	}

	public override void OnReleased()
	{
		Game.serv.keyboard.RemoveHandler(this);
		_keyhandler.Reset();
		_keyhandler = null;
	}
}
