using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class QreqNextBlurb : ConvoBlurb
{
	public enum Type
	{
		PlayerReq,
		NPCReq,
		NPCReqEpilogue,
		NPCCollect,
		NPCCollectEpilogue
	}

	public Type type;

	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		if (type == Type.NPCCollect || type == Type.NPCCollectEpilogue)
		{
			return GetCollectBlurb(model, button, replacements);
		}
		return GetQReqBlurb(button, replacements);
	}

	private string GetCollectBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		ConvoData convoData = button?.state.data ?? model.state?.data;
		QuestDefinition questDefinition = ((convoData is ConvoDataQuestActive convoDataQuestActive) ? convoDataQuestActive.GetDef() : ((convoData is ConvoDataQuestRequest convoDataQuestRequest) ? convoDataQuestRequest.GetDef() : null));
		if (questDefinition == null)
		{
			return null;
		}
		return Loc.Get((type == Type.NPCCollect) ? questDefinition.beforechoice : ((type == Type.NPCCollectEpilogue) ? questDefinition.afterchoice : null), replacements);
	}

	private string GetQReqBlurb(ConvoButton button, string[] replacements)
	{
		ConvoDataQuestRequest data = button.GetData<ConvoDataQuestRequest>();
		if (data == null)
		{
			return null;
		}
		return Loc.Get((type == Type.NPCReq) ? data.blurbsleft[0] : ((type == Type.PlayerReq) ? data.blurbsleft[1] : ((type == Type.NPCReqEpilogue) ? data.GetDef().requestinfo.introending : null)), replacements);
	}
}
