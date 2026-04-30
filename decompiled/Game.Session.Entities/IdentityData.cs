using Game.Core;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class IdentityData : BaseData
{
	public EntityID id = 0uL;

	public bool enabled = true;

	public Xorshift rng;

	public string modid;

	public bool IsModEntity => modid != null;
}
