using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class ExpiredBlurb : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		if (!((button?.state.data ?? model.state?.data) is ConvoDataQuestActive convoDataQuestActive))
		{
			return null;
		}
		return Game.ctx.quests.ExplainExpirationOrNull(convoDataQuestActive.GetDef(), replacements);
	}
}
