using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class GangAggroLevelMod : GangAggroBaseMod
{
	public Fixnum perpoint = 1;

	public Fixnum offset = 0;

	protected override Fixnum FindExtra(PlayerInfo inquirer, PlayerInfo enemy)
	{
		Fixnum fixnum = inquirer.social.EvaluateRelationshipFromPlayerTo(enemy.PID);
		if (fixnum >= 0)
		{
			return 0;
		}
		return fixnum * perpoint + offset;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.ai-aggro-level", "delta", delta);
	}
}
