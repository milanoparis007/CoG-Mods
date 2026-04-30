using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Player.AI;
using Game.Session.Player.KB;
using SomaSim.Util;

namespace Game.Session.Player;

[DebuggerDisplay("{DebugString}")]
public sealed class PlayerInfo
{
	private AllPlayersManager _mgr;

	private PlayerID _pid;

	private PlayerData _data;

	private List<PlayerSubmanager> _allsubs;

	private List<ITurnHandlingPlayerSubmanager> _tickedsubs;

	public PlayerKB kb;

	public PlayerCrew crew;

	public PlayerSocial social;

	public PlayerFinances finances;

	public PlayerSkills skills;

	public PlayerTerritory territory;

	public PlayerOutposts outposts;

	public PlayerMeetings meetings;

	public PlayerGambling gambling;

	public PlayerScheme schemes;

	public PlayerThrone throne;

	public CommandExecutor commands;

	public AutomationExecutor automation;

	public PlayerAI ai;

	public PlayerID PID => _pid;

	public PlayerType PlayerType => _data.type;

	public bool IsHuman => _data.type == PlayerType.HumanPlayer;

	public bool IsGangOrGoon
	{
		get
		{
			if (_data.type != PlayerType.GangPlayer)
			{
				return _data.type == PlayerType.GoonPlayer;
			}
			return true;
		}
	}

	public bool IsCopOrFed
	{
		get
		{
			if (_data.type != PlayerType.CopPlayer)
			{
				return _data.type == PlayerType.AgentPlayer;
			}
			return true;
		}
	}

	public bool IsAnyAIPlayer
	{
		get
		{
			if (!IsCopOrFed)
			{
				return IsGangOrGoon;
			}
			return true;
		}
	}

	public bool IsJustGang => _data.type == PlayerType.GangPlayer;

	public bool IsJustGoon => _data.type == PlayerType.GoonPlayer;

	public bool IsJustCop => _data.type == PlayerType.CopPlayer;

	public bool IsJustFed => _data.type == PlayerType.AgentPlayer;

	public bool IsCriminal
	{
		get
		{
			if (!IsJustGang && !IsJustGoon)
			{
				return IsHuman;
			}
			return true;
		}
	}

	public bool OfInterest
	{
		get
		{
			if (!IsHuman && !GetNpcDef().territory.IsSet)
			{
				return GetNpcDef().business.IsSet;
			}
			return true;
		}
	}

	public bool HasBusinessAdvisor
	{
		get
		{
			if (!IsHuman)
			{
				return GetNpcDef().business.IsSet;
			}
			return false;
		}
	}

	public bool IsInvincibleCheatEnabled { get; set; }

	public bool DebugHumanAsAI { get; set; }

	private string DebugString => $"PlayerInfo for {_pid}";

	public NPCDefinition GetNpcDef()
	{
		return Game.serv.globals.settings.npc.FindNPCDefinition(_data.type);
	}

	public void Initialize(AllPlayersManager manager, PlayerID pid)
	{
		_mgr = manager;
		_pid = pid;
		TypeUtils.MakeMemberInstances<PlayerSubmanager>(this);
		_allsubs = TypeUtils.GetMemberInstances<PlayerSubmanager>(this).ToList();
		_allsubs.ForEach(delegate(PlayerSubmanager sub)
		{
			sub.Initialize(_mgr, _pid);
		});
		_tickedsubs = TypeUtils.GetMemberInstances<ITurnHandlingPlayerSubmanager>(this).ToList();
	}

	public void SetDataSource(PlayerData data, bool loaded)
	{
		_data = data;
		_allsubs.ForEach(delegate(PlayerSubmanager sub)
		{
			sub.SetDataSource(_data, loaded);
		});
	}

	public void OnGlobalTurnSetAdvanced()
	{
		foreach (ITurnHandlingPlayerSubmanager tickedsub in _tickedsubs)
		{
			tickedsub.OnGlobalTurnSetAdvanced();
		}
	}

	public void OnPlayerTurnStarted()
	{
		foreach (ITurnHandlingPlayerSubmanager tickedsub in _tickedsubs)
		{
			tickedsub.OnPlayerTurnStarted();
		}
	}

	public void OnPlayerTurnEnded()
	{
		foreach (ITurnHandlingPlayerSubmanager tickedsub in _tickedsubs)
		{
			tickedsub.OnPlayerTurnEnded();
		}
	}

	public PlayerTurnStatus GetPlayerTurnStatus()
	{
		PlayerTurnStatus playerTurnStatus = PlayerTurnStatus.TurnFinished;
		foreach (ITurnHandlingPlayerSubmanager tickedsub in _tickedsubs)
		{
			PlayerTurnStatus playerTurnStatus2 = tickedsub.GetPlayerTurnStatus();
			if (playerTurnStatus2 > playerTurnStatus)
			{
				playerTurnStatus = playerTurnStatus2;
			}
		}
		return playerTurnStatus;
	}

	public void Release()
	{
		_allsubs.Reverse();
		_allsubs.ForEach(delegate(PlayerSubmanager sub)
		{
			sub.Release();
		});
		TypeUtils.RemoveMemberInstances<PlayerSubmanager>(this);
		_allsubs = null;
		_tickedsubs = null;
		_data = null;
		_mgr = null;
		_pid = PlayerID.INVALID;
	}
}
