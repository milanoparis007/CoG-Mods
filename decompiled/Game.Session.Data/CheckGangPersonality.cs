using Game.Core;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckGangPersonality : AbstractVisitRequirement
{
	public enum StatusType
	{
		EqualTo,
		Not
	}

	public StatusType @is;

	public Label personality;

	public override bool DoesPass(VisitState visit)
	{
		PlayerInfo playerInfo = visit.npc.components.agent?.GetPlayer();
		if (playerInfo == null || !playerInfo.IsGangOrGoon)
		{
			return false;
		}
		Label other = playerInfo.ai.Data.personality;
		return @is switch
		{
			StatusType.Not => !personality.Equals(other), 
			StatusType.EqualTo => personality.Equals(other), 
			_ => false, 
		};
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), "");
	}
}
