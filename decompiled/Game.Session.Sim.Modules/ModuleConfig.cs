using Game.Core;

namespace Game.Session.Sim.Modules;

public abstract class ModuleConfig<M, C, D> : IModuleConfigTyped<M, C, D>, IModuleConfig where M : IModuleTyped<M, C, D>, new() where C : IModuleConfigTyped<M, C, D> where D : IModuleDataTyped<M, C, D>
{
	public Label id;

	public ModuleCommon common;

	public Label Id => id;

	public ModuleCommon Common
	{
		get
		{
			if (!(id == Game.ctx.resManager.ETHNIC_BOOZE_PRODUCTION))
			{
				return common;
			}
			return Game.ctx.resManager.replaceCommon ?? common;
		}
	}

	public virtual IModule MakeModule()
	{
		return new M();
	}
}
