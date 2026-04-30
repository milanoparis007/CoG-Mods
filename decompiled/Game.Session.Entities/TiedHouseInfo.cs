using Game.Core;

namespace Game.Session.Entities;

public sealed class TiedHouseInfo
{
	public PlayerID pid;

	public SimTime start;

	public SimTime end;

	public TiedHouseInfo()
	{
	}

	public TiedHouseInfo(PlayerID pid, int days)
	{
		this.pid = pid;
		start = Game.ctx.clock.Now;
		end = start.IncrementDays(days);
	}
}
