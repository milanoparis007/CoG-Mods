using Game.Session.Data;
using Game.Session.Sim;
using Game.UI.Session.OwnedBiz;

namespace Game.UI.Session.Politics;

public sealed class PoliticsModel : HUDModel<PoliticsModel, PoliticsDialog, PoliticsController>
{
	public VisitState visit;

	internal ModuleToggleContext currentSlot;

	public Ward ThisWard => Game.ctx.simman.politics.GetWardForID(visit.GetBldgNode().precinctId);

	public override void Reset()
	{
		base.Reset();
		visit = null;
	}
}
