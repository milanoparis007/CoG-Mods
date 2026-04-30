using Game.Core;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public abstract class GangAggroBaseMod : AbstractModifier
{
	public enum Truce
	{
		DontCare,
		Active,
		NotActive
	}

	public Truce truce;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		if (DoesPass(query))
		{
			PlayerInfo player = query.pid.FindPlayer();
			PlayerInfo enemy = FindEnemyOrNull(query);
			Fixnum fixnum = FindExtra(player, enemy);
			return source + fixnum;
		}
		return source;
	}

	protected abstract Fixnum FindExtra(PlayerInfo player, PlayerInfo enemy);

	protected PlayerInfo FindEnemyOrNull(ModQuery query)
	{
		return query.FindTarget()?.data.agent?.pid.FindPlayer();
	}

	private bool DoesPass(ModQuery query)
	{
		PlayerInfo playerInfo = query.pid.FindPlayer();
		if (playerInfo == null)
		{
			return false;
		}
		PlayerID pid = FindEnemyOrNull(query)?.PID ?? PlayerID.INVALID;
		var (flag, hasTruce) = playerInfo.ai.combat.GetAggroAndTruce(pid);
		if (flag)
		{
			return TruceMatches(hasTruce);
		}
		return false;
	}

	private bool TruceMatches(bool hasTruce)
	{
		return truce switch
		{
			Truce.Active => hasTruce, 
			Truce.NotActive => !hasTruce, 
			_ => true, 
		};
	}
}
