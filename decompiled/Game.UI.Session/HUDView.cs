namespace Game.UI.Session;

public abstract class HUDView<M, V, C> : BaseHUDDialog, IMVCView<M, V, C>, IMVCBaseView where M : HUDModel<M, V, C>, new() where V : HUDView<M, V, C>, new() where C : HUDController<M, V, C>, new()
{
	public C Controller { get; private set; }

	public M Model => Controller.Model;

	internal override void Initialize()
	{
		base.Initialize();
		Controller = new C();
		Controller.Initialize((V)this);
	}

	internal override void Release()
	{
		Controller.Release();
		Controller = null;
		base.Release();
	}
}
