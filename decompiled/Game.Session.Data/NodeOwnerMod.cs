using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class NodeOwnerMod : BaseDeltaMultiplierModifier
{
	public enum OwnerType
	{
		None,
		Human,
		Gang,
		Goon,
		GangOrGoon,
		ThisPlayer,
		AnotherPlayer,
		AnyPlayer
	}

	public OwnerType @is;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Node | ModQueryElement.Player;

	public override bool DoesPass(ModQuery query)
	{
		PlayerID playerID = query.FindNode()?.owner.Get() ?? PlayerID.INVALID;
		bool isAnyPlayer = playerID.IsAnyPlayer;
		switch (@is)
		{
		case OwnerType.None:
			return !isAnyPlayer;
		case OwnerType.ThisPlayer:
			if (isAnyPlayer)
			{
				return playerID == query.pid;
			}
			return false;
		case OwnerType.AnotherPlayer:
			if (isAnyPlayer)
			{
				return playerID != query.pid;
			}
			return false;
		case OwnerType.AnyPlayer:
			return isAnyPlayer;
		case OwnerType.Human:
			if (isAnyPlayer)
			{
				return playerID.IsHumanPlayer;
			}
			return false;
		case OwnerType.Gang:
			if (isAnyPlayer)
			{
				return Game.ctx.players.WithID(playerID).IsJustGang;
			}
			return false;
		case OwnerType.Goon:
			if (isAnyPlayer)
			{
				return Game.ctx.players.WithID(playerID).IsJustGoon;
			}
			return false;
		case OwnerType.GangOrGoon:
			if (isAnyPlayer)
			{
				return Game.ctx.players.WithID(playerID).IsGangOrGoon;
			}
			return false;
		default:
			Logger.Warning("Unknown owner type", @is);
			return false;
		}
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.node-ownership", "delta", AbstractModifier.FormatDelta(delta));
	}
}
