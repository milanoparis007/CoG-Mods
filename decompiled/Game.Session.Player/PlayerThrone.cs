using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Store;
using Game.Session.Data;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Player;

public sealed class PlayerThrone : PlayerSubmanager, ITurnHandlingPlayerSubmanager
{
	private PlayerThroneData _tdata;

	private Dictionary<SessionEventType, List<Trophy>> trophyMap = new Dictionary<SessionEventType, List<Trophy>>();

	private static readonly string THRONE_RESOURCES_FOLDER = "UI Images/Throne/";

	public readonly Label STARTER_TROPHY_1 = new Label("basic-chair");

	public readonly Label STARTER_TROPHY_2 = new Label("basic-desk");

	public readonly Label STARTER_TROPHY_3 = new Label("basic-desk-nyc");

	public readonly Label STARTER_TROPHY_4 = new Label("basic-chair-nyc");

	public PlayerThroneData Data => _tdata;

	public ThroneSettings Settings => Game.serv.globals.settings.throne;

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		if (!_player.PID.IsHumanPlayer || !CanSeeThrone())
		{
			return;
		}
		foreach (Trophy trophy in Game.serv.globals.settings.throne.trophies)
		{
			foreach (SessionEventType updateEvent in trophy.updateEvents)
			{
				trophyMap.AddToList(updateEvent, trophy);
			}
		}
		foreach (KeyValuePair<SessionEventType, List<Trophy>> pair in trophyMap)
		{
			Game.ctx.events.AddListener(pair.Key, delegate
			{
				TryGrantTrophies(pair.Value);
			});
		}
	}

	public override void OnPreRelease()
	{
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		base.OnPreRelease();
		if (!_player.PID.IsHumanPlayer || !CanSeeThrone())
		{
			return;
		}
		foreach (KeyValuePair<SessionEventType, List<Trophy>> pair in trophyMap)
		{
			Game.ctx.events.RemoveListener(pair.Key, delegate
			{
				TryGrantTrophies(pair.Value);
			});
		}
	}

	public override void OnPostSetDataSource(bool loaded)
	{
		_tdata = _data.throne ?? _tdata;
	}

	private void OnNewGame(SessionEvent sev)
	{
	}

	public void OnGlobalTurnSetAdvanced()
	{
	}

	public void OnPlayerTurnStarted()
	{
		if (CanSeeThrone() && Data.trophies.Count >= 1 && Data.chosenThrone.IsNotSet)
		{
			Game.ctx.hud.tickers.AddThroneTicker(Loc.Get("ui.tickers.throne-select"), toThrone: false, isUnlock: true);
		}
	}

	public void OnPlayerTurnEnded()
	{
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		return PlayerTurnStatus.TurnFinished;
	}

	public void AddToThroneRoom(Label trophy, string signature)
	{
		if (CanSeeThrone() && !_tdata.removed.Contains(trophy) && !Game.ctx.tutorial.IsThroneRoomSuppressed)
		{
			if (Data.trophies.Count == 0)
			{
				Game.serv.ui.AddPopup(new ThroneSelectionPopup());
				Game.ctx.hud.tickers.AddThroneTicker(Loc.Get("ui.tickers.throne-select"), toThrone: true, isUnlock: true);
			}
			Trophy trophyForId = Settings.GetTrophyForId(trophy);
			if (trophyForId.render.swapId.IsSet && IsInThrone(trophyForId.render.swapId))
			{
				RemoveFromThroneRoom(trophyForId.render.swapId);
			}
			if (!IsLockedOut(trophyForId.id))
			{
				Data.trophies.Add(new IdToSignature(trophy, signature));
				Game.ctx.hud.tickers?.AddThroneTicker(Loc.Get("ui.tickers.throne-added"), toThrone: true);
			}
		}
		bool IsLockedOut(Label toAdd)
		{
			foreach (IdToSignature trophy2 in _tdata.trophies)
			{
				if (Settings.GetTrophyForId(trophy2.id).render.swapId == toAdd)
				{
					return true;
				}
			}
			return false;
		}
	}

	public void RemoveFromThroneRoom(Label trophy)
	{
		IdToSignature item = Data.trophies.Find((IdToSignature x) => x.id == trophy);
		Data.trophies.Remove(item);
		Data.removed.Add(trophy);
	}

	public string GetSignature(Label trophy)
	{
		foreach (IdToSignature trophy2 in Data.trophies)
		{
			if (trophy2.id == trophy)
			{
				return trophy2.signature;
			}
		}
		return null;
	}

	public bool IsInThrone(Label trophy)
	{
		foreach (IdToSignature trophy2 in Data.trophies)
		{
			if (trophy2.id == trophy)
			{
				return true;
			}
		}
		return false;
	}

	public void ChooseThroneStyle(Label throneId)
	{
		_tdata.chosenThrone = throneId;
	}

	public Sprite GetThroneSprite(ThroneSettings.ThroneType type)
	{
		return GetThroneSprite(type.Image);
	}

	public Sprite GetThroneSprite(Trophy trophy)
	{
		return GetThroneSprite(trophy.Image);
	}

	public Sprite GetThroneSprite(string suffix)
	{
		return Resources.Load<Sprite>(THRONE_RESOURCES_FOLDER + suffix);
	}

	public Vector2 GetTrophyPosition(Trophy trophy)
	{
		return Data.chosenThrone.String switch
		{
			"throne-lodge" => trophy.render.drawPos[0], 
			"throne-skyscraper" => trophy.render.drawPos[1], 
			_ => new Vector2(0f, 0f), 
		};
	}

	public Label GetThroneStyle()
	{
		return _tdata.chosenThrone;
	}

	public void TryGrantTrophies(List<Label> trophies, bool ignoreReqCount = false)
	{
		TryGrantTrophies(trophies.Select((Label x) => Settings.GetTrophyForId(x)).ToList(), ignoreReqCount);
	}

	public void TryGrantTrophies(List<Trophy> trophies, bool ignoreReqCount = false)
	{
		if (!CanSeeThrone())
		{
			return;
		}
		VisitState visit = new VisitState(_player.crew.AllCrew.ToList()[0], Game.ctx.clock.Now, _player.PID);
		ThroneSettings.ThroneType throneTypeForId = Settings.GetThroneTypeForId(Data.chosenThrone);
		string signature = ((throneTypeForId != null) ? Loc.Get(throneTypeForId.locdefaultsig) : Loc.Get("throne.generic.defaultsig"));
		foreach (Trophy trophy in trophies)
		{
			bool num = (trophy.reqs.Count > 0 || ignoreReqCount) && trophy.reqs.AllPass(visit);
			bool flag = trophy.validThrones.Contains(GetThroneStyle());
			bool flag2 = trophy.id == STARTER_TROPHY_1 || trophy.id == STARTER_TROPHY_2 || trophy.id == STARTER_TROPHY_3 || trophy.id == STARTER_TROPHY_4;
			if (num && !IsInThrone(trophy.id) && (flag || flag2))
			{
				AddToThroneRoom(trophy.id, signature);
			}
		}
	}

	public void UpdateSeen()
	{
		_tdata.seen = _tdata.trophies.Select((IdToSignature x) => x.id).ToList();
	}

	public bool HasSeen(Label id)
	{
		return _tdata.seen.Contains(id);
	}

	public static bool CanSeeThrone()
	{
		return Game.serv.store.IsPackInstalled(PackID.CriminalRecord);
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		if (!CanSeeThrone())
		{
			return;
		}
		foreach (Trophy trophy in Game.serv.globals.settings.throne.trophies)
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("player", "add-trophy", trophy.id.String, CheatAddTrophy));
		}
	}

	private string CheatAddTrophy(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<trophy-id>");
		}
		Trophy trophyForId = Settings.GetTrophyForId(new Label(args[2]));
		if (trophyForId != null)
		{
			if (!trophyForId.validThrones.Contains(GetThroneStyle()) && GetThroneStyle().IsSet)
			{
				DebugConsoleEntry.InvalidParam("This item cannot be placed in your throne style!", args, 2);
			}
			AddToThroneRoom(trophyForId.id, Loc.Get("throne.cheat-sig"));
			return $"Added trophy {trophyForId.id} to your office";
		}
		return DebugConsoleEntry.InvalidParam("Invalid trophy id", args, 2);
	}

	protected override void ReleaseConsoleEntries()
	{
		if (CanSeeThrone())
		{
			Game.ctx.console.Remove(this);
			base.ReleaseConsoleEntries();
		}
	}
}
