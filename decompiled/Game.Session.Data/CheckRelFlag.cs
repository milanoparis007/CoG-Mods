using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckRelFlag : AbstractVisitRequirement
{
	public enum Type
	{
		Set,
		NotSet
	}

	public Label id;

	public Type @is;

	public string lockey;

	public override bool DoesPass(VisitState visit)
	{
		bool expected = @is == Type.Set;
		return GetPlayer(visit).social.GetRelationshipFromSourceToPlayer(visit)?.TestFlag(id, expected) ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), (lockey != null) ? Loc.Get(lockey) : null);
	}
}
