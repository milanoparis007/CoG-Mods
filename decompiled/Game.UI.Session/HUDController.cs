namespace Game.UI.Session;

public abstract class HUDController<M, V, C> : IMVCController<M, V, C>, IMVCBaseController where M : HUDModel<M, V, C>, new() where V : HUDView<M, V, C>, new() where C : HUDController<M, V, C>, new()
{
	public M Model { get; private set; }

	public V View { get; private set; }

	public virtual void Initialize(V view)
	{
		View = view;
		Model = new M();
	}

	public virtual void Release()
	{
		Model.Reset();
		Model = null;
		View = null;
	}
}
