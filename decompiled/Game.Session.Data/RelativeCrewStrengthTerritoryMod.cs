using Game.Core;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class RelativeCrewStrengthTerritoryMod : BaseModifier
{
	public Fixnum delta = 0;

	public Fixnum multiplier = 1;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Node | ModQueryElement.Player;

	public override string Lockey => "mod.crew-strength";

	public override Fixnum Evaluate(ModQuery query, Fixnum source)
	{
		PlayerInfo sourcePlayer = query.FindPlayer();
		PlayerInfo targetPlayer = (query.FindNode()?.owner.Get() ?? PlayerID.INVALID).FindPlayer();
		Fixnum fixnum = FindRatio(sourcePlayer, targetPlayer);
		return source + (fixnum + delta) * multiplier;
	}

	private Fixnum FindRatio(PlayerInfo sourcePlayer, PlayerInfo targetPlayer)
	{
		if (sourcePlayer == null || targetPlayer == null)
		{
			return Fixnum.ZERO;
		}
		return RelativeCrewStrengthMod.FindRatioHelper(sourcePlayer, targetPlayer);
	}
}
