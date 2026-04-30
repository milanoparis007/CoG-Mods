using Game.Services.Store;

namespace Game.Session.Data;

public sealed class CheckPacks : AbstractVisitRequirement
{
	public PackID id;

	public override bool DoesPass(VisitState visit)
	{
		return Game.serv.store.IsPackInstalled(id);
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
