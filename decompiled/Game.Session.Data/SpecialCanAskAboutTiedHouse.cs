using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public class SpecialCanAskAboutTiedHouse : IConvoButtonRequirement, IRequirement
{
	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		PlayerInfo playerInfo = visit.npc.data.agent.pid.FindPlayer();
		SocialActionInfo? socialActionInfo = playerInfo?.ai?.business?.FindTiedHouseConvoMemory(visit.pid);
		if (!socialActionInfo.HasValue)
		{
			return false;
		}
		return BuildingUtil.FindBizForOwner(socialActionInfo.Value.entityCtx).components.biz.IsTiedTo(playerInfo.PID);
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}
