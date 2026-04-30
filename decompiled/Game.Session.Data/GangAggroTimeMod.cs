using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class GangAggroTimeMod : GangAggroBaseMod
{
	public Fixnum permonth = 1;

	public Fixnum offset = 0;

	protected override Fixnum FindExtra(PlayerInfo inquirer, PlayerInfo enemy)
	{
		int item = inquirer.ai.combat.GetAggroLengthDays(enemy.PID).days;
		return permonth * (Fixnum)((float)item / 30f) + offset;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.ai-aggro-time", "delta", delta);
	}
}
