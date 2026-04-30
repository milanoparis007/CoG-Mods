using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Services.Filesystem;
using Game.Session;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Session.Crew;
using Game.UI.Session.Ledger;
using Game.UI.Session.Popups;
using Game.UI.Util;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session.HUD;

public sealed class HUDBar : BaseHUDDialog
{
	private sealed class StatsCounter : MonoBehaviour
	{
		public float refresh = 0.25f;

		public float fps;

		public float bps;

		private long lastRefreshTicks;

		private int lastRefreshFrame;

		private void Update()
		{
			int renderedFrameCount = Time.renderedFrameCount;
			long ticks = DateTime.UtcNow.Ticks;
			float num = (float)new TimeSpan(ticks - lastRefreshTicks).TotalSeconds;
			if (num > refresh)
			{
				fps = (float)(renderedFrameCount - lastRefreshFrame) / num;
				lastRefreshTicks = ticks;
				lastRefreshFrame = renderedFrameCount;
			}
		}
	}

	public sealed class CrewMouseover : BaseCustomTextMouseover
	{
		private HUDBar _bar;

		public CrewMouseover(HUDBar bar)
		{
			_bar = bar;
		}

		protected override string ProduceText()
		{
			PlayerCrew.NumAndMax numAndMax = default(PlayerCrew.NumAndMax);
			string text = null;
			string key = null;
			PlayerCrew crew = Game.ctx.players.Human.crew;
			if (context == _bar._peepsButton)
			{
				numAndMax = GetPeepCount();
				text = GetPeepExplanation();
				key = "ui.hud.peeps.mo";
			}
			else if (context == _bar._carsButton)
			{
				numAndMax = GetVehicleCount(AvatarType.Car);
				text = GetVehicleExplanation(AvatarType.Car);
				key = "ui.hud.cars.mo";
			}
			else if (context == _bar._trucksButton)
			{
				numAndMax = GetVehicleCount(AvatarType.Truck);
				text = GetVehicleExplanation(AvatarType.Truck);
				key = "ui.hud.trucks.mo";
			}
			else if (context == _bar._cornersButton)
			{
				int count = Game.ctx.players.Human.territory.GetAllOwnedNodesUnsafe().Count;
				key = "ui.hud.corners.mo";
				return Loc.Get(key, "num", count);
			}
			return Loc.Get(key, "max", numAndMax.max, "explanation", text);
			PlayerCrew.NumAndMax GetPeepCount()
			{
				int max = (int)crew.CrewGrowth.currentCap;
				return new PlayerCrew.NumAndMax(crew.LivingCrewCount, max);
			}
			string GetPeepExplanation()
			{
				return crew.CrewGrowth.ExplainCrewCap(crew, includeZeros: false);
			}
			PlayerCrew.NumAndMax GetVehicleCount(AvatarType type)
			{
				int vehicleCap = crew.CrewGrowth.GetVehicleCap(crew, type == AvatarType.Car);
				return new PlayerCrew.NumAndMax(crew.CountVehiclesByType(type), vehicleCap);
			}
			string GetVehicleExplanation(AvatarType type)
			{
				return crew.CrewGrowth.ExplainVehicleCap(crew, type == AvatarType.Car, includeZeros: false);
			}
		}
	}

	public const string BUTTON_OVERLAYS = "Corner/Maps";

	public const string BUTTON_RESOURCES = "Corner/Resources";

	public const string BUTTON_REPORTS = "Corner/Reports";

	public const string BUTTON_ENCYCLOPEDIA = "Corner/Encyclopedia";

	public const string BUTTON_GAMEMENU = "Corner/Menu";

	public const string TEXT_MONEY = "Info/MoneyDisplay/Money";

	public const string TEXT_CORNERS = "Info/CornersDisplay/Corners";

	public const string CREW_PEEPS = "Info/PeepsDisplay/Peeps";

	public const string CREW_CARS = "Info/CarsDisplay/Cars";

	public const string CREW_TRUCKS = "Info/TrucksDisplay/Trucks";

	public const string BUTTON_MONEY = "Info/MoneyDisplay/MoneyShade";

	public const string BUTTON_CORNERS = "Info/CornersDisplay/CornerShade";

	public const string BUTTON_PEEPS = "Info/PeepsDisplay/PeepShade";

	public const string BUTTON_CARS = "Info/CarsDisplay/CarShade";

	public const string BUTTON_TRUCKS = "Info/TrucksDisplay/TruckShade";

	public const string DATE = "Clock/Date";

	public const string DEBUG = "Clock/Debug";

	public const string NEXT_TURN_BUTTON = "Clock/Next Turn Button";

	public const string NEXT_TURN_BUTTON_OV = "Clock/Next Turn Button/Overlay";

	public const string NEXT_TURN_BUTTON_TEXT = "Clock/Next Turn Button/Text";

	public const string PROGRESS_BAR = "Clock/Progress Bar";

	public const string PROGRESS_BAR_SLIDER = "Clock/Progress Bar/Slider";

	public const string DECO_PARENT = "BG/Deco";

	public const string DECO = "BG/Deco/Chrome";

	public const string SAVING_PANEL = "Saving";

	public const string WATERMARK = "Watermark";

	public const string WATERMARK_TEXT = "Watermark/Text";

	public const int PROGRESS_BAR_SLIDER_WIDTH = 250;

	private StatsCounter _stats;

	private TextMeshProUGUI _clock;

	private TextMeshProUGUI _debug;

	private TextMeshProUGUI _money;

	private TextMeshProUGUI _peeps;

	private TextMeshProUGUI _cars;

	private TextMeshProUGUI _trucks;

	private TextMeshProUGUI _corners;

	private GameObject _cornersButton;

	private GameObject _peepsButton;

	private GameObject _carsButton;

	private GameObject _trucksButton;

	private GameObject _moneyButton;

	private bool _dirtyMoney;

	private bool _dirtyCrew;

	internal PlayerID _lastShownPlayer = PlayerID.INVALID;

	internal SimTime _lastShownPlayerTime = SimTime.MIN_DATE;

	public override bool ShowAtStartup => true;

	public override TweenType Tween => TweenType.None;

	public override UIReference UIReference => UIElements.HUDBar;

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		_go.SetButtonListener("Corner/Maps", delegate
		{
			Game.ctx.hud.overlaysBar.Toggle();
		});
		_go.SetButtonListener("Corner/Resources", delegate
		{
			Game.ctx.hud.resourcesBar.Toggle();
		});
		_go.SetButtonListener("Corner/Reports", delegate
		{
			Game.ctx.hud.reportsBar.Toggle();
		});
		_go.SetButtonListener("Corner/Encyclopedia", delegate
		{
			Game.serv.ui.AddPopup<EncyclopediaPopup>();
		});
		_go.SetButtonListener("Corner/Menu", delegate
		{
			Game.ctx.RequestQuit();
		});
		_go.SetButtonListener("Info/MoneyDisplay/MoneyShade", delegate
		{
			Game.ctx.hud.ledger.Controller.Show();
		});
		_go.SetButtonListener("Info/PeepsDisplay/PeepShade", delegate
		{
			Game.ctx.hud.crewinfolist.Show();
		});
		_go.SetButtonListener("Info/CarsDisplay/CarShade", delegate
		{
			Game.serv.ui.AddPopup(new CrewManagementPopup());
		});
		_go.SetButtonListener("Info/TrucksDisplay/TruckShade", delegate
		{
			Game.serv.ui.AddPopup(new CrewManagementPopup());
		});
		_go.SetButtonListener("Info/CornersDisplay/CornerShade", delegate
		{
			Game.ctx.hud.ledger.Controller.ShowReport(ReportType.Fronts);
		});
		_money = _go.GetText("Info/MoneyDisplay/Money");
		_clock = _go.GetText("Clock/Date");
		_clock.text = "";
		_debug = _go.GetText("Clock/Debug");
		_debug.text = "";
		_debug.gameObject.SetActive(Game.settings.DoEnableFPS);
		_stats = _go.AddComponent<StatsCounter>();
		_corners = _go.GetText("Info/CornersDisplay/Corners");
		_peeps = _go.GetText("Info/PeepsDisplay/Peeps");
		_cars = _go.GetText("Info/CarsDisplay/Cars");
		_trucks = _go.GetText("Info/TrucksDisplay/Trucks");
		_cornersButton = _go.GetChild("Info/CornersDisplay/CornerShade");
		_carsButton = _go.GetChild("Info/CarsDisplay/CarShade");
		_trucksButton = _go.GetChild("Info/TrucksDisplay/TruckShade");
		_peepsButton = _go.GetChild("Info/PeepsDisplay/PeepShade");
		_moneyButton = _go.GetChild("Info/MoneyDisplay/MoneyShade");
		_go.GetImage("Clock/Next Turn Button/Overlay").color = Game.ctx.players.Human.territory.colorInfo.GetPlayerColor();
		_go.SetButtonListener("Clock/Next Turn Button", OnNextTurnClick);
		_go.SetText("Clock/Next Turn Button/Text", "");
		EnableEthReplacement();
		_go.SetActive("Saving", value: false);
		_go.SetActive("Watermark", Game.settings.DoEnableWatermark);
		_go.SetTextOrHide("Watermark/Text", $"{Game.settings.CurrentBuildStage} build {GameSettings.GetVersionString()}");
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHumanPlayerTurnStarted);
		Game.ctx.events.AddListener(SessionEventType.OnSavingStatusChanged, OnSaving);
		Game.ctx.events.AddListener(SessionEventType.PlayerRespectChanged, OnSomePlayerEvent);
		Game.ctx.events.AddListener(SessionEventType.PlayerFinancesChanged, OnSomePlayerEvent);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleCreated, OnCrewEvent);
		Game.ctx.events.AddListener(SessionEventType.CrewVehicleRemoved, OnCrewEvent);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberAdded, OnCrewEvent);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberKilled, OnCrewEvent);
		Game.serv.mouseovers.Register(MouseoverType.HUDBarMoneyMouseover, new MoneyMouseover());
		Game.serv.mouseovers.Register(MouseoverType.HUDBarCrewMouseover, new CrewMouseover(this));
	}

	protected override void OnAfterHide()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.HUDBarMoneyMouseover);
		Game.serv.mouseovers.Unregister(MouseoverType.HUDBarCrewMouseover);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnHumanPlayerTurnStarted);
		Game.ctx.events.RemoveListener(SessionEventType.OnSavingStatusChanged, OnSaving);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerRespectChanged, OnSomePlayerEvent);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerFinancesChanged, OnSomePlayerEvent);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleCreated, OnCrewEvent);
		Game.ctx.events.RemoveListener(SessionEventType.CrewVehicleRemoved, OnCrewEvent);
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberAdded, OnCrewEvent);
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberKilled, OnCrewEvent);
		_clock = null;
		_stats = null;
		_money = (_peeps = (_cars = (_trucks = null)));
		base.OnAfterHide();
	}

	private void OnSaving(SessionEvent _)
	{
		string sourceText = "";
		_go.SetActive("Saving", value: false);
		switch (Game.ctx.Saving)
		{
		case SessionContext.SavingStatus.Serializing:
			sourceText = Loc.Get("ui.hud.saving.up");
			_go.SetActive("Saving", value: true);
			break;
		case SessionContext.SavingStatus.Writing:
			sourceText = Loc.Get("ui.hud.writing.up");
			break;
		case SessionContext.SavingStatus.Finished:
			_lastShownPlayer = PlayerID.INVALID;
			_lastShownPlayerTime = SimTime.MIN_DATE;
			break;
		}
		_clock.SetText(sourceText);
		UpdateNextTurnButton();
	}

	private void OnNextTurnClick()
	{
		Game.ctx.players.FinishActivePlayerTurn();
		UpdateNextTurnButton();
	}

	private void OnSomePlayerEvent(SessionEvent sev)
	{
		RefreshIfHuman(sev.pid);
	}

	private void OnCrewEvent(SessionEvent sev)
	{
		RefreshIfHuman(sev.pid);
	}

	private void OnHumanPlayerTurnStarted(SessionEvent sev)
	{
		MaybeAutosave();
		RefreshIfHuman(PlayerID.HumanPlayer);
	}

	private void RefreshIfHuman(PlayerID pid)
	{
		if (pid.IsHumanPlayer)
		{
			RefreshContents();
		}
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		_dirtyMoney = true;
		_dirtyCrew = true;
	}

	public void TutForceRefresh()
	{
		RefreshIfHuman(PlayerID.HumanPlayer);
	}

	private void MaybeAutosave()
	{
		GamePreferences game = Game.serv.saveload.prefs.game;
		if (game.IsAutosaveEnabled && Game.ctx.CanAutosaveGame && Game.ctx.clock.CurrentTurn % game.autosaveTurns == 0)
		{
			Game.ctx.AutosaveGame();
		}
	}

	internal override void UpdateAnimations(GameAnimUpdate anim)
	{
		base.UpdateAnimations(anim);
		if (_debug.gameObject.activeSelf)
		{
			_debug.SetText($"FPS: {(int)_stats.fps}");
		}
		TryRefreshMoney();
		TryRefreshTurn();
		TryRefreshCrew();
		TryRefreshCorners();
		ProcessHyperlink();
	}

	private void ProcessHyperlink()
	{
		if (LinkHandler.HasAnyLinkID())
		{
			EncyclopediaPopup.HandleLinkClick(LinkHandler.PopLinkID());
		}
	}

	private void TryRefreshTurn()
	{
		PlayerID currentPlayer = Game.ctx.clock.CurrentPlayer;
		SimTime now = Game.ctx.clock.Now;
		if (!(_lastShownPlayer == currentPlayer) || _lastShownPlayerTime.days != now.days)
		{
			float num = (currentPlayer.IsHumanPlayer ? 0f : (currentPlayer.IsSystem ? 0f : ((float)currentPlayer.ArrayIndex / (float)Game.ctx.players.all.Count)));
			string sourceText = (currentPlayer.IsHumanPlayer ? Loc.Get("ui.hud.dateturn", "date", Loc.FormatDateLong(Game.ctx.clock.Now), "turn", Loc.FormatNumber(Game.ctx.clock.CurrentTurn)) : Loc.Get("ui.hud.player", "percent", Loc.Percentage(num)));
			_clock.SetText(sourceText);
			_go.GetChild("Clock/Progress Bar").SetActive(!currentPlayer.IsHumanPlayer);
			RectTransform obj = _go.GetChild("Clock/Progress Bar/Slider").transform as RectTransform;
			obj.sizeDelta = obj.sizeDelta.SetX(250f * num);
			UpdateNextTurnButton();
			_lastShownPlayer = currentPlayer;
			_lastShownPlayerTime = now;
		}
	}

	private void UpdateNextTurnButton()
	{
		bool num = Game.ctx.clock.CurrentPlayer.FindPlayer().GetPlayerTurnStatus() == PlayerTurnStatus.TurnWaitingForInput;
		bool isSaving = Game.ctx.IsSaving;
		bool flag = num && !isSaving;
		_go.GetButton("Clock/Next Turn Button").interactable = flag;
		_go.SetText("Clock/Next Turn Button/Text", TextUtil.ColorEnabledIf(flag, Loc.Get("ui.hud.nextturn")));
	}

	private void TryRefreshMoney()
	{
		if (_dirtyMoney)
		{
			_dirtyMoney = false;
			MoneyPerTurnListing moneyThisTurn = Game.ctx.players.Human.finances.GetMoneyThisTurn();
			Money endMoney = moneyThisTurn.endMoney;
			string text = Loc.Money(endMoney);
			Price deltaMoney = moneyThisTurn.DeltaMoney;
			string text2 = TextUtil.ColorGreenRed(deltaMoney.cash, Loc.Price(deltaMoney));
			string key = ((endMoney.cash >= 10000 || deltaMoney.Abs.cash >= 1000) ? "ui.hud.money-turn-short" : "ui.hud.money-turn");
			_money.SetText(Loc.Get(key, "money", text, "deltaString", text2));
		}
	}

	private void TryRefreshCorners()
	{
		List<NodeID> allOwnedNodesUnsafe = Game.ctx.players.Human.territory.GetAllOwnedNodesUnsafe();
		SetText(_corners, "ui.hud.corners", allOwnedNodesUnsafe.Count);
		static void SetText(TextMeshProUGUI text, string lockey, int v)
		{
			text.SetText(Loc.Get(lockey, "num", v));
		}
	}

	private void TryRefreshCrew()
	{
		if (_dirtyCrew)
		{
			_dirtyCrew = false;
			PlayerCrew crew = Game.ctx.players.Human.crew;
			SetText(_peeps, "ui.hud.peeps", crew.GetPeepCount());
			SetText(_cars, "ui.hud.cars", crew.GetVehicleCount(AvatarType.Car));
			SetText(_trucks, "ui.hud.trucks", crew.GetVehicleCount(AvatarType.Truck));
		}
		static void SetText(TextMeshProUGUI text, string lockey, PlayerCrew.NumAndMax v)
		{
			text.SetText(Loc.Get(lockey, "num", v.num, "max", v.max));
		}
	}

	public void EnableEthReplacement()
	{
		GameObject ethChild = _go.GetEthChild("BG/Deco/Chrome", GetEthnicityString());
		_go.GetChild("BG/Deco").SetActiveOnlyOneChild(ethChild);
		static string GetEthnicityString()
		{
			if (!PlayerCrew.HasEthPackForCurrEth())
			{
				return "";
			}
			return Game.ctx.session.scenario.newgamepars.playerdetails.player.ethnicity.ToString().ToUpper();
		}
	}
}
