using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class RelativeCrewStrengthMod : BaseModifier
{
	public Fixnum delta = 0;

	public Fixnum multiplier = 1;

	public Fixnum bizdefault = 0;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override string Lockey => "mod.crew-strength";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		PlayerInfo sourcePlayer = query.FindPlayer();
		PlayerInfo targetPlayer = query.FindTarget()?.data.agent?.pid.FindPlayer();
		Fixnum fixnum = FindRatio(sourcePlayer, targetPlayer);
		return source + (fixnum + delta) * multiplier;
	}

	private Fixnum FindRatio(PlayerInfo sourcePlayer, PlayerInfo targetPlayer)
	{
		if (sourcePlayer == null || targetPlayer == null)
		{
			return bizdefault;
		}
		return FindRatioHelper(sourcePlayer, targetPlayer);
	}

	internal static Fixnum FindRatioHelper(PlayerInfo sourcePlayer, PlayerInfo targetPlayer)
	{
		Fixnum fixnum = CalculateAttackPoints(sourcePlayer);
		Fixnum fixnum2 = CalculateAttackPoints(targetPlayer);
		if (!fixnum2.IsZero)
		{
			return fixnum / fixnum2;
		}
		return Fixnum.MAX_VALUE;
	}

	private static Fixnum CalculateAttackPoints(PlayerInfo player)
	{
		return Game.ctx.simman.combat.FindPlayerCrewHitpoints(player);
	}
}
