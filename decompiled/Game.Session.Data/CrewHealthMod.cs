using Game.Core;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CrewHealthMod : BaseModifier
{
	public enum Type
	{
		Total,
		Single
	}

	public enum Target
	{
		Player,
		Target
	}

	public Fixnum delta = 0;

	public Fixnum multiplier = 1;

	public Type type;

	public Target of;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player | ModQueryElement.Target;

	public override string Lockey => "mod.crew-strength";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		PlayerInfo player = ((of == Target.Player) ? query.FindPlayer() : query.FindTarget()?.data.agent?.pid.FindPlayer());
		Fixnum fixnum = CalculateHealth(player, query.targetId);
		return source + (fixnum + delta) * multiplier;
	}

	private Fixnum CalculateHealth(PlayerInfo player, EntityID peepId)
	{
		if (player == null)
		{
			return 0;
		}
		EntityID? onlySpecificPeep = ((type == Type.Single) ? new EntityID?(peepId) : ((EntityID?)null));
		return Game.ctx.simman.combat.FindPlayerCrewHealth(player, onlySpecificPeep);
	}
}
