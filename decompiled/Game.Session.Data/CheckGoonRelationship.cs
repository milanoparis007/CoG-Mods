using Game.Core;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CheckGoonRelationship : AbstractVisitRequirement
{
	public enum StatusType
	{
		OnGoodTerms,
		OnBadTerms
	}

	public StatusType @is;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc.components.agent?.GetPlayer();
		if (playerInfo?.ai?.goon == null)
		{
			return false;
		}
		Fixnum fixnum = playerInfo.ai.goon.GetPersonality().friendlyAt.Evaluate(visit.MakeOwnerModQuery());
		PlayerID targetId = visit.GetPlayer()?.PID ?? PlayerID.INVALID;
		Fixnum fixnum2 = playerInfo.social.EvaluateRelationshipFromPlayerTo(targetId);
		return @is switch
		{
			StatusType.OnGoodTerms => fixnum2 >= fixnum, 
			StatusType.OnBadTerms => fixnum2 < fixnum, 
			_ => false, 
		};
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc.components.agent?.GetPlayer();
		if (playerInfo?.ai?.goon == null)
		{
			return new ReqExplanation(passed: false, "");
		}
		Fixnum fixnum = playerInfo.ai.goon.GetPersonality().friendlyAt.Evaluate(visit.MakeOwnerModQuery());
		return new ReqExplanation(DoesPass(visit), (@is == StatusType.OnGoodTerms) ? Loc.Get("ui.requirements.relationship.goon.goodterms", "thresh", fixnum) : Loc.Get("ui.requirements.relationship.goon.badterms", "thresh", fixnum));
	}
}
