using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.Politics;

public class SponsorButtonMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		SponsorCtx componentInObjectOrParents = context.GetComponentInObjectOrParents<SponsorCtx>();
		Fixnum amt = (componentInObjectOrParents.isNomination ? Game.ctx.simman.politics.GetNominatePrice(componentInObjectOrParents.candidate) : Game.ctx.simman.politics.GetSponsorPrice(componentInObjectOrParents.candidate));
		(string, string, string, string, string) tuple = (componentInObjectOrParents.isNomination ? Game.ctx.simman.politics.GetNominatePriceExplanation(componentInObjectOrParents.candidate) : Game.ctx.simman.politics.GetSponsorPriceExplanation(componentInObjectOrParents.candidate));
		string text = (componentInObjectOrParents.isNomination ? Loc.Get("ui.politics.nominate-pol.mo", "price", Loc.Money(amt)) : Loc.Get("ui.politics.sponsor-pol.mo", "price", Loc.Money(amt)));
		Relationship relationshipFromPlayerTo = Game.ctx.players.Human.social.GetRelationshipFromPlayerTo(componentInObjectOrParents.candidate);
		if (relationshipFromPlayerTo == null || !relationshipFromPlayerTo.HasBuff(BuffConstants.RELBUFF_NY_POL_STARTER))
		{
			return text + tuple.Item1 + tuple.Item2 + tuple.Item3 + tuple.Item4 + tuple.Item5;
		}
		return text;
	}
}
