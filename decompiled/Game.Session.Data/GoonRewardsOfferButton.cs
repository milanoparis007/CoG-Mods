using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class GoonRewardsOfferButton : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		if ((button?.state.data ?? model.state?.data) is ConvoDataGoonRewards convoDataGoonRewards)
		{
			return Loc.Get(Game.serv.globals.settings.npc.goons.FindSpecialGoonConfig(convoDataGoonRewards.goontype).loclootoffer, replacements);
		}
		return null;
	}
}
