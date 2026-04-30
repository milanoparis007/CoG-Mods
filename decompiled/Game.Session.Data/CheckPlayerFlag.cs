using Game.Core;

namespace Game.Session.Data;

public class CheckPlayerFlag : AbstractVisitRequirement
{
	public enum Type
	{
		Set,
		NotSet
	}

	public Label id;

	public Type @is;

	public override bool DoesPass(VisitState visit)
	{
		bool expected = @is == Type.Set;
		return GetPlayer(visit).skills.TestFlag(id, expected);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
