using Game.Core;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public struct ModuleInitData
{
	public IModuleConfig config;

	public IModuleData data;

	public IRandom rng;

	public SimTime enable;

	public bool IsLoaded => data != null;

	public bool IsCreated => data == null;

	public ModuleInitData(IModuleConfig config, IRandom rng, SimTime enable)
	{
		this.config = config;
		data = null;
		this.rng = rng;
		this.enable = enable;
	}

	public ModuleInitData(IModuleConfig config, IModuleData data)
	{
		this.config = config;
		this.data = data;
		rng = null;
		enable = default(SimTime);
	}
}
