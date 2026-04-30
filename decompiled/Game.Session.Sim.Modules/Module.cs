using Game.Core;
using Game.Session.Entities;

namespace Game.Session.Sim.Modules;

public abstract class Module<M, C, D> : IModuleTyped<M, C, D>, IModule where M : IModuleTyped<M, C, D>, new() where C : class, IModuleConfigTyped<M, C, D> where D : class, IModuleDataTyped<M, C, D>, new()
{
	public D data;

	public C config;

	public IModuleData ModuleData => data;

	public IModuleConfig ModuleConfig => config;

	public virtual void Initialize(ModuleInitData init)
	{
		config = init.config as C;
		if (init.IsLoaded)
		{
			data = init.data as D;
		}
		if (init.IsCreated || data == null)
		{
			data = new D();
			data.InitializeOnCreate(init);
		}
	}

	public virtual void Release(bool shutdown)
	{
		data = null;
		config = null;
	}

	public abstract ModuleResult DoUpdate(ModuleQuery q, SimTime time, bool initial, bool enabled);

	public abstract bool IsEnabled(SimTime time);

	protected bool WasJustEnabled(SimTime time)
	{
		return ModulesUtil.WasJustEnabled(time, data.EnableTime);
	}

	protected void MaybeInformAboutConstruction(Entity building, SimTime time, bool initial)
	{
		if (!initial && WasJustEnabled(time))
		{
			building.components.modules.MaybeShowConstructionFinishedFeedback(data.Id);
		}
	}
}
