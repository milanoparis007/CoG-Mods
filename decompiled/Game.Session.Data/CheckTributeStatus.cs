using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckTributeStatus : AbstractVisitRequirement
{
	public enum StatusType
	{
		NotAsked,
		Asked,
		Paying,
		NotPaying,
		Refused
	}

	public StatusType @is;

	public override bool DoesPass(VisitState visit)
	{
		PlayerOutposts outposts = GetPlayer(visit).outposts;
		BizComponent biz = visit.biz.components.biz;
		bool flag = outposts.IsBizPayingTribute(visit.biz);
		bool flag2 = outposts.IsBizRejectingTribute(visit.biz);
		switch (@is)
		{
		case StatusType.Paying:
			return flag;
		case StatusType.NotPaying:
			return !flag;
		case StatusType.Refused:
			return flag2;
		case StatusType.Asked:
			if (!flag && !flag2)
			{
				return biz.HasHumanMentionedTribute();
			}
			return false;
		case StatusType.NotAsked:
			if (!flag && !flag2)
			{
				return !biz.HasHumanMentionedTribute();
			}
			return false;
		default:
			return false;
		}
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = ((@is == StatusType.Paying) ? Loc.Get("ui.requirements.tribute.paying") : ((@is == StatusType.Refused) ? Loc.Get("ui.requirements.tribute.refused") : Loc.Get("ui.requirements.tribute.none")));
		return new ReqExplanation(DoesPass(visit), message);
	}
}
