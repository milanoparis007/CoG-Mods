namespace Game.UI.Session;

public interface IMVCView<M, V, C> : IMVCBaseView where M : IMVCModel<M, V, C> where V : IMVCView<M, V, C> where C : IMVCController<M, V, C>
{
}
