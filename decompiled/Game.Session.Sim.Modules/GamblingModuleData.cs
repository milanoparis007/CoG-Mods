using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class GamblingModuleData : ModuleData<GamblingModule, GamblingModuleConfig, GamblingModuleData>
{
	public Xorshift rng = new Xorshift();

	public List<AmenityData> amenities = new List<AmenityData>();

	public override void InitializeOnCreate(ModuleInitData data)
	{
		base.InitializeOnCreate(data);
		rng.Init(data.rng.Generate());
	}
}
