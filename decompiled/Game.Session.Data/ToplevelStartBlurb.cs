using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class ToplevelStartBlurb : ConvoBlurb
{
	public string rootkey;

	public override string GetBlurb(ConversationModel model, ConvoButton _, string[] replacements)
	{
		int num = model.visit.FindVisitCount();
		int valueOrDefault = (Game.ctx.hud.convoDialog?.Controller?.Model?.shared.statecount).GetValueOrDefault();
		string msg = Loc.GetPluralized(ConvoBlurbUtils.FindRelationshipBlurbKey(model, rootkey), num, replacements);
		if (num == 1 && valueOrDefault == 1)
		{
			TryAddBizInfo(ref msg, model);
		}
		return msg;
	}

	private void TryAddBizInfo(ref string msg, ConversationModel model)
	{
		if (model.IsBusinessVisitOK && model.visit.AtValidBiz)
		{
			string text = BuildingUtil.DescribeBuildingAtScopeOut(model.visit.building);
			if (!string.IsNullOrWhiteSpace(text))
			{
				string fullName = model.visit.npc.data.person.FullName;
				msg = Loc.Get("convo.toplevel-start-blurb", "scopeOut", text, "ownerName", fullName, "msg", msg);
			}
		}
	}
}
