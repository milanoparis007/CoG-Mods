using Game.Session.Data;
using Game.Session.Sim.Modules;

namespace Game.UI.Session.OwnedBiz;

public sealed class OwnedBizModel : HUDModel<OwnedBizModel, OwnedBizDialog, OwnedBizController>
{
	internal class LoadingState
	{
		public InventoryModule building;

		public InventoryModule vehicle;

		public bool IsLoading => vehicle != null;

		public bool IsNotLoading => vehicle == null;

		internal void ToggleLoading(OwnedBizModel model, bool loading)
		{
			if (loading)
			{
				StartLoading(model);
			}
			else
			{
				StopLoading();
			}
		}

		internal void RefreshOnCrewChange(OwnedBizModel model)
		{
			if (IsLoading)
			{
				StopLoading();
				StartLoading(model);
			}
		}

		internal void StartLoading(OwnedBizModel model)
		{
			building = model.currentSlot.module as InventoryModule;
			vehicle = ModulesUtil.GetInventory(model.visit.crew);
		}

		internal void StopLoading()
		{
			vehicle = (building = null);
		}
	}

	public VisitState visit;

	internal LoadingState invstate = new LoadingState();

	internal ModuleToggleContext currentSlot;

	public override void Reset()
	{
		base.Reset();
		currentSlot = null;
		visit = null;
		invstate = new LoadingState();
	}
}
