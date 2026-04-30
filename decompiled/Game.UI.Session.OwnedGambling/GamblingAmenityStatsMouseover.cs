using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.OwnedGambling;

public class GamblingAmenityStatsMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		AmenityMouseoverCtx componentInObjectOrParents = context.GetComponentInObjectOrParents<AmenityMouseoverCtx>();
		return Game.ctx.players.Human.gambling.DescribeAmenity(componentInObjectOrParents.lastTurn, componentInObjectOrParents.def, componentInObjectOrParents.model.Module, componentInObjectOrParents.model.visit).stats;
	}
}
