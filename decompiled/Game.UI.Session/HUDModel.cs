namespace Game.UI.Session;

public abstract class HUDModel<M, V, C> : IMVCModel<M, V, C>, IMVCBaseModel where M : HUDModel<M, V, C>, new() where V : HUDView<M, V, C>, new() where C : HUDController<M, V, C>, new()
{
	public virtual void Reset()
	{
	}
}
