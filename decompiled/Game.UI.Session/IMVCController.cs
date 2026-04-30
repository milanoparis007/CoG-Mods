namespace Game.UI.Session;

public interface IMVCController<M, V, C> : IMVCBaseController where M : IMVCModel<M, V, C> where V : IMVCView<M, V, C> where C : IMVCController<M, V, C>
{
}
