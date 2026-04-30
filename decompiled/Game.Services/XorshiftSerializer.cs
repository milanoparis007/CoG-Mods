using SomaSim.SION;
using SomaSim.Util;

namespace Game.Services;

public static class XorshiftSerializer
{
	public static object Serialize(Xorshift rng, Serializer s)
	{
		return s.Serialize(rng.y);
	}

	public static Xorshift Deserialize(object value, Serializer s)
	{
		uint y = s.Deserialize<uint>(value);
		return new Xorshift
		{
			y = y
		};
	}
}
