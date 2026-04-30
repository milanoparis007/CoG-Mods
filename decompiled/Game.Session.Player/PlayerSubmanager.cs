using Game.Core;

namespace Game.Session.Player;

public abstract class PlayerSubmanager
{
	protected AllPlayersManager _manager;

	protected PlayerInfo _player;

	protected PlayerID _pid;

	protected PlayerData _data;

	public PlayerID PID => _pid;

	public PlayerInfo PlayerInfo => _player;

	public void Initialize(AllPlayersManager manager, PlayerID pid)
	{
		_manager = manager;
		_pid = pid;
		_player = manager.WithID(pid);
		OnPostInitialize();
	}

	public virtual void OnPostInitialize()
	{
		if (_pid.IsHumanPlayer)
		{
			InitializeConsoleEntries();
		}
	}

	public void SetDataSource(PlayerData data, bool loaded)
	{
		_data = data;
		OnPostSetDataSource(loaded);
	}

	public abstract void OnPostSetDataSource(bool loaded);

	public virtual void OnPreRelease()
	{
		if (_pid.IsHumanPlayer)
		{
			ReleaseConsoleEntries();
		}
	}

	public void Release()
	{
		OnPreRelease();
		_manager = null;
		_pid = PlayerID.INVALID;
		_player = null;
		_data = null;
	}

	protected virtual void InitializeConsoleEntries()
	{
		Game.ctx.console.HasEntriesFor(this);
	}

	protected virtual void ReleaseConsoleEntries()
	{
		Game.ctx.console.HasEntriesFor(this);
	}
}
