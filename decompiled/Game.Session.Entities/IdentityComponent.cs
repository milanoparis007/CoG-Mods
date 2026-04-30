using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class IdentityComponent : BaseComponent
{
	public string debugname;

	public uint hash;

	public IdentityConfig Config => _baseConfig as IdentityConfig;

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
		debugname = Config.template.String;
		hash = HashUtil.Hash((uint)_entity.Id.index);
		if (!loaded)
		{
			GenerateNewIdentity();
		}
	}

	private void GenerateNewIdentity()
	{
		IdentityData ident = base.entity.data.ident;
		ident.rng = new Xorshift(hash);
		ident.modid = Config.modid;
	}

	internal uint GetIdentityHash()
	{
		return hash;
	}

	internal float GetIdentityHashAsFloat()
	{
		return (float)hash * 2.3283064E-10f;
	}

	internal IRandom GetIdentityRNGUnchanging(uint seed = 0u)
	{
		return new Xorshift(hash ^ seed);
	}
}
