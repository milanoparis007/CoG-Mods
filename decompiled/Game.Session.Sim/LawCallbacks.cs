using Game.Core;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Sim;

public sealed class LawCallbacks
{
	public static void EnactCallbackGrapeJuiceIllegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.GRAPE_JUICE);
	}

	public static void RevokeCallbackGrapeJuiceIllegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.GRAPE_JUICE);
	}

	public static void EnactCallbackAppleJuiceIllegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.APPLE_JUICE);
	}

	public static void RevokeCallbackAppleJuiceIllegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.APPLE_JUICE);
	}

	public static void EnactCallbackMaltIllegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.MALT);
	}

	public static void RevokeCallbackMaltIllegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.MALT);
	}

	public static void EnactCallbackNeutralAlcoholLegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.NEUTRAL_ALCOHOL);
	}

	public static void RevokeCallbackNeutralAlcoholLegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.NEUTRAL_ALCOHOL);
	}

	public static void EnactCallbackBrickWineLegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.BRICK_WINE);
	}

	public static void RevokeCallbackBrickWineLegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.BRICK_WINE);
	}

	public static void EnactCallbackCiderLegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.CIDER);
	}

	public static void RevokeCallbackCiderLegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.CIDER);
	}

	public static void EnactCallbackHomeBrewLegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.HOME_BREW);
	}

	public static void RevokeCallbackHomeBrewLegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.HOME_BREW);
	}

	public static void EnactCallbackMoonshineLegal()
	{
		Game.ctx.resManager.ForceResourceLegal(ResourceConstants.MOONSHINE);
	}

	public static void RevokeCallbackMoonshineLegal()
	{
		Game.ctx.resManager.ForceResourceIllegal(ResourceConstants.MOONSHINE);
	}

	public static void EnactCallbackBizOwnerStimulus()
	{
		PlayerInfo human = Game.ctx.players.Human;
		Entity container = human.territory.Safehouse.FindEntity();
		int num = 2000;
		human.finances.DoChangeMoney(container, new Price(num), MoneyReason.Other);
	}
}
