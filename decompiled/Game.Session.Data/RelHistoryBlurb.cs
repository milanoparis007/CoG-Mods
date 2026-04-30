using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class RelHistoryBlurb : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton _, string[] replacements)
	{
		EntityID playerPeepId = Game.ctx.players.Human.social.PlayerPeepId;
		Entity npc = model.visit.npc;
		SocialActionInfo? socialActionInfo = Game.ctx.simman.rels.GetOrNull(npc.Id, playerPeepId)?.GetRandomHistoryOrNull(npc);
		if (!socialActionInfo.HasValue)
		{
			return Loc.Get("convo.social.history.none.pc-npc.blurb");
		}
		SocialActionInfo value = socialActionInfo.Value;
		if (value.questCtx.IsSet)
		{
			return ConvoBlurbUtils.LocWithQuestID(value.locblurb, value.questCtx);
		}
		if (!value.entityCtx.IsValid)
		{
			return Loc.Get(value.locblurb);
		}
		return ConvoBlurbUtils.LocWithRelationship(npc, value.entityCtx.FindEntity(), value.locblurb, null);
	}
}
