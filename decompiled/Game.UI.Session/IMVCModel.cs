namespace Game.UI.Session;

public interface IMVCModel<M, V, C> : IMVCBaseModel where M : IMVCModel<M, V, C> where V : IMVCView<M, V, C> where C : IMVCController<M, V, C>
{
}
