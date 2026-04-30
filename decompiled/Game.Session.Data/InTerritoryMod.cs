using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class InTerritoryMod : BaseDeltaMultiplierModifier
{
	public enum Type
	{
		None,
		Human,
		NonHuman
	}

	public Type owner;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Node | ModQueryElement.Player;

	public override bool DoesPass(ModQuery query)
	{
		PlayerID playerID = query.FindNode()?.owner?.pid ?? PlayerID.INVALID;
		return owner switch
		{
			Type.None => playerID.IsNotAnyPlayer, 
			Type.Human => playerID.IsHumanPlayer, 
			Type.NonHuman => playerID.IsAIPlayer, 
			_ => false, 
		};
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get((owner == Type.Human) ? "mod.in-player-territory.human" : "mod.in-player-territory.nonhuman", "delta", AbstractModifier.FormatDelta(delta));
	}
}
