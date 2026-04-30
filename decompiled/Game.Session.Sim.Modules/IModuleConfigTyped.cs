namespace Game.Session.Sim.Modules;

public interface IModuleConfigTyped<M, C, D> : IModuleConfig where M : IModuleTyped<M, C, D> where C : IModuleConfigTyped<M, C, D> where D : IModuleDataTyped<M, C, D>
{
}
