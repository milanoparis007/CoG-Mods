using Game.Core;

namespace Game.Session.Data;

public class CheckCrewPeep : AbstractVisitRequirement
{
	public enum PeepType
	{
		Player,
		NotPlayer
	}

	public PeepType @is;

	public override bool DoesPass(VisitState visit)
	{
		EntityID playerPeepId = GetPlayer(visit).social.PlayerPeepId;
		if (@is != PeepType.Player)
		{
			return visit.peep.Id != playerPeepId;
		}
		return visit.peep.Id == playerPeepId;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
