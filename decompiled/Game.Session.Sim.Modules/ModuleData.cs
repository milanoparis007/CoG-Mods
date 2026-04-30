using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Sim.Modules;

public abstract class ModuleData<M, C, D> : IModuleDataTyped<M, C, D>, IModuleData where M : IModuleTyped<M, C, D> where C : IModuleConfigTyped<M, C, D> where D : IModuleDataTyped<M, C, D>
{
	public Label id;

	public SimTime enable;

	public List<Label> expansions;

	public Label Id => id;

	public SimTime EnableTime
	{
		get
		{
			return enable;
		}
		set
		{
			enable = value;
		}
	}

	public List<Label> Expansions
	{
		get
		{
			return expansions;
		}
		set
		{
			expansions = value;
		}
	}

	public virtual void InitializeOnCreate(ModuleInitData data)
	{
		id = data.config.Id;
		enable = data.enable;
		expansions = null;
	}
}
