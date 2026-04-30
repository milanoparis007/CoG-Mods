namespace Game.Session.Sim.Modules;

public interface IModuleDataTyped<M, C, D> : IModuleData where M : IModuleTyped<M, C, D> where C : IModuleConfigTyped<M, C, D> where D : IModuleDataTyped<M, C, D>
{
}
