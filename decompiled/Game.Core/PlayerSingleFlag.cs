using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public sealed class PlayerSingleFlag
{
	public PlayerID pid = PlayerID.INVALID;

	public bool IsSet => pid.IsValid;

	public bool IsNotSet => pid.IsNotValid;

	private string DebugString => $"[PFLAG {pid}]";

	public PlayerID Get()
	{
		return pid;
	}

	public void Set(PlayerID value)
	{
		pid = value;
	}

	public void Clear()
	{
		pid = PlayerID.INVALID;
	}

	public bool Is(PlayerID pid)
	{
		return pid == this.pid;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
