using Game.Session.Entities;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public class SpecialCheckResEventActive : IConvoButtonRequirement, IRequirement
{
	public enum Type
	{
		Unknown,
		ThisTurn,
		ComingSoon,
		Registered,
		Skipped,
		Denied
	}

	public Type type;

	public int minrel;

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		ResEventData resEventData = visit.building?.components.residence.GetResEventOrNull();
		if (resEventData == null)
		{
			return false;
		}
		Type type = ((!((visit.GetPlayer().social.GetRelationshipFromSourceToPlayer(visit)?.Evaluate().current ?? Fixnum.ZERO) >= minrel)) ? Type.Denied : (resEventData.IsThisTurn ? (resEventData.IsAttendanceRegistered ? Type.ThisTurn : Type.Skipped) : ((!resEventData.IsComingSoon) ? Type.Skipped : (resEventData.IsAttendanceRegistered ? Type.Registered : Type.ComingSoon))));
		return type == this.type;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		return new ReqExplanation(DoesPass(visit, bstate), null);
	}
}
