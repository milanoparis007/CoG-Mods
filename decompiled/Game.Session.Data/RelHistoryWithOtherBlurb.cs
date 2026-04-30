using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class RelHistoryWithOtherBlurb : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton _, string[] replacements)
	{
		Entity npc = model.visit.npc;
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(npc.Id);
		if (listOrNull == null)
		{
			return Loc.Get("convo.social.history.none.npc-npc.blurb");
		}
		Relationship relationship = null;
		foreach (Relationship datum in listOrNull.data)
		{
			if (datum.IsRelToAIWithSocialHistory())
			{
				relationship = datum;
				break;
			}
		}
		SocialActionInfo? socialActionInfo = relationship?.GetRandomHistoryOrNull(npc);
		if (!socialActionInfo.HasValue)
		{
			return Loc.Get("convo.social.history.none.attackers-target.blurb");
		}
		SocialActionInfo value = socialActionInfo.Value;
		string playerGroupName = relationship.to.FindEntity().data.agent.pid.FindPlayer().social.PlayerGroupName;
		return Loc.Get(value.locblurb, "gangname", playerGroupName);
	}
}
