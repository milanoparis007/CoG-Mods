using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public sealed class PlayerSingleFlagWithHistory
{
	public PlayerID pid = PlayerID.INVALID;

	public PlayerID lastpid = PlayerID.INVALID;

	public SimTime updated = SimTime.MIN_DATE;

	public bool IsSet => pid.IsValid;

	public bool IsNotSet => pid.IsNotValid;

	private string DebugString => $"[PFLAG {pid} (from {lastpid} @ {updated})]";

	public PlayerID Get()
	{
		return pid;
	}

	public bool Is(PlayerID pid)
	{
		return pid == this.pid;
	}

	public void Set(PlayerID value, SimTime time)
	{
		lastpid = pid;
		pid = value;
		updated = time;
	}

	public void Clear(SimTime time)
	{
		Set(PlayerID.INVALID, time);
	}

	public bool WasEverModified()
	{
		return updated.days != SimTime.MIN_DATE.days;
	}

	public override string ToString()
	{
		return DebugString;
	}
}
