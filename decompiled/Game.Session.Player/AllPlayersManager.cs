using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Entities;
using Game.UI.Session;
using SomaSim.SION;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Player;

public sealed class AllPlayersManager : AbstractSessionManager, IGlobalTurnSetHandler, ISessionManager, IPlayerTurnHandler, ISaveLoadProvider
{
	public AllPlayersManagerPersistedData data;

	public List<PlayerInfo> all;

	public AllPlayersStats stats;

	private float _lastAdvanceTime;

	private const float _doubleTapTime = 0.5f;

	public PlayerInfo Human => all[PlayerID.HumanPlayer.ArrayIndex];

	public PlayerInfo WithID(PlayerID pid)
	{
		return all[pid.ArrayIndex];
	}

	public void ForEachPlayer(Action<PlayerID, PlayerInfo> fn)
	{
		for (short num = 0; num < data.totalPlayers; num++)
		{
			PlayerID arg = new PlayerID(num);
			fn(arg, all[arg.ArrayIndex]);
		}
	}

	public override void OnInitializeDone()
	{
		AllPlayersManagerPersistedData allPlayersManagerPersistedData = new AllPlayersManagerPersistedData(Game.ctx.session.mapconfig);
		UpdatePersistedData(allPlayersManagerPersistedData);
		stats = new AllPlayersStats();
		all = ListGenerators.ListOfNewInstances<PlayerInfo>(data.totalPlayers);
		ForEachPlayer(delegate(PlayerID pid, PlayerInfo player)
		{
			player.Initialize(this, pid);
		});
		ForEachPlayer(delegate(PlayerID pid, PlayerInfo player)
		{
			player.SetDataSource(GetPlayerData(pid), loaded: false);
		});
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Add(OnPersonDeath);
		InitializeConsoleEntries();
	}

	private void UpdatePersistedData(AllPlayersManagerPersistedData data)
	{
		this.data = data;
	}

	public override void OnReleased()
	{
		ReleaseConsoleEntries();
		Game.ctx.simman.peoplegen.OnBeforePersonDeath.Remove(OnPersonDeath);
		ForEachPlayer(delegate(PlayerID pid, PlayerInfo player)
		{
			player.Release();
		});
		all.Clear();
		all = null;
		stats = null;
		UpdatePersistedData(null);
		base.OnReleased();
	}

	public PlayerData GetPlayerData(PlayerID pid)
	{
		return data.players[pid.ArrayIndex];
	}

	public PlayerData GetHumanPlayerData()
	{
		return data.players[PlayerID.HumanPlayer.ArrayIndex];
	}

	private PlayerInfo GetCurrentPlayer()
	{
		return Game.ctx.clock.CurrentPlayer.FindPlayer();
	}

	public void OnGlobalTurnSetAdvanced()
	{
		stats.OnGlobalTurnSetAdvanced();
		for (int i = 0; i < all.Count; i++)
		{
			all[i].OnGlobalTurnSetAdvanced();
		}
	}

	public void OnPlayerTurnStarted()
	{
		GetCurrentPlayer().OnPlayerTurnStarted();
	}

	public bool IsPlayerTurnDone()
	{
		return GetCurrentPlayer().GetPlayerTurnStatus() == PlayerTurnStatus.TurnFinished;
	}

	public void OnPlayerTurnEnded()
	{
		GetCurrentPlayer().OnPlayerTurnEnded();
	}

	internal void FinishActivePlayerTurn()
	{
		if (Game.ctx.clock.CurrentPlayer.IsHumanPlayer)
		{
			Game.serv.audio.PlayUISFX(SFXType.ClickButtonNextTurn);
			Game.ctx.selection.ClearActive();
			GetCurrentPlayer().ai.MarkPlayerTurnAsDone();
		}
	}

	private bool TrackDoubleTap()
	{
		float realtimeSinceStartup = Time.realtimeSinceStartup;
		bool result = realtimeSinceStartup - _lastAdvanceTime < 0.5f;
		_lastAdvanceTime = realtimeSinceStartup;
		return result;
	}

	internal void AdvanceToNextCrew(bool zoom)
	{
		if (Game.ctx.clock.CurrentPlayer.IsHumanPlayer)
		{
			bool zoom2 = zoom || TrackDoubleTap();
			GetCurrentPlayer().crew.SelectNextCrewMember(zoom2);
		}
	}

	internal void AdvanceToSpecificCrew(int number, bool zoom)
	{
		if (Game.ctx.clock.CurrentPlayer.IsHumanPlayer)
		{
			bool zoom2 = zoom || TrackDoubleTap();
			int index = MathUtil.ClampMin(number - 1, 0);
			GetCurrentPlayer().crew.SelectSpecificCrewMember(index, zoom2);
		}
	}

	private void OnPersonDeath(Entity peep)
	{
		foreach (PlayerInfo item in all)
		{
			item.social.ResetRelationshipSymmetrical(peep.Id);
		}
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey<AllPlayersManagerPersistedData>(data, "data", UpdatePersistedData);
		ForEachPlayer(delegate(PlayerID pid, PlayerInfo sub)
		{
			sub.SetDataSource(GetPlayerData(pid), loaded: true);
		});
		yield break;
	}

	private void InitializeConsoleEntries()
	{
		Game.ctx.console.Add(this, new DebugConsoleEntry("player", "set-invincible", CheatSetInvincible));
		Game.ctx.console.Add(this, new DebugConsoleEntry("player", "reset-invincible", CheatResetInvincible));
		Game.ctx.console.Add(this, new DebugConsoleEntry("player", "set-human-ai", SetHumanAI));
		Game.ctx.console.Add(this, new DebugConsoleEntry("find", "entity", FindEntityByID));
	}

	private void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
	}

	private string CheatSetInvincible(string[] args)
	{
		Human.IsInvincibleCheatEnabled = true;
		return "Human player is invincible.";
	}

	private string CheatResetInvincible(string[] _)
	{
		Human.IsInvincibleCheatEnabled = false;
		return "Human player is no longer invincible.";
	}

	private string SetHumanAI(string[] args)
	{
		if (args.Length != 3 || !int.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "{0 or 1}");
		}
		bool flag = result != 0;
		Human.DebugHumanAsAI = flag;
		bool flag2 = Game.serv.globals.settings.npc.FindNPCDefinition(PlayerType.HumanPlayer) != null;
		return $"Set human as fake ai to {flag}. " + (flag2 ? "" : "But the AI config for type HumanPlayer is missing in config files! This will not work.");
	}

	private string FindEntityByID(string[] args)
	{
		if (args.Length == 3 && int.TryParse(args[2], out var result))
		{
			return FindEntityAndZoom(result);
		}
		return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<entity index> \nEntity index is the last number in the ID, e.g. for entity 'E2_123' it would be just 123");
	}

	private static string FindEntityAndZoom(int index)
	{
		Entity entity = Game.ctx.entityman.FindByIndex(index);
		if (entity == null)
		{
			return "Failed to find entity with index " + index;
		}
		WorldPos? worldPos = BoardUtil.FindBoardPositionFor(entity);
		if (!worldPos.HasValue)
		{
			return $"Entity {entity} is not on the game board";
		}
		HUDUtil.GoTo(worldPos.Value, zoomIn: true, showFx: true);
		return $"Zooming to entity {entity}";
	}
}
