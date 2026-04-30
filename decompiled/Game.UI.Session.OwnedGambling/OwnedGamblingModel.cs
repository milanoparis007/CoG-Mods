using Game.Session.Data;
using Game.Session.Sim.Modules;
using Game.UI.Session.OwnedBiz;

namespace Game.UI.Session.OwnedGambling;

public sealed class OwnedGamblingModel : HUDModel<OwnedGamblingModel, OwnedGamblingDialog, OwnedGamblingController>
{
	public VisitState visit;

	internal ModuleToggleContext currentSlot;

	public GamblingModule Module => visit.building.components.modules.gambling;

	public override void Reset()
	{
		base.Reset();
		visit = null;
	}
}
