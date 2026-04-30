using Game.Core;

namespace Game.Session.Setup;

public class WaterBead
{
	public WaterDecoType decoType;

	public WorldPos pos;

	public float rot;

	public bool IsResidential
	{
		get
		{
			if (decoType != WaterDecoType.ResStart && decoType != WaterDecoType.ResMid)
			{
				return decoType == WaterDecoType.ResEnd;
			}
			return true;
		}
	}

	public bool IsIndustrial
	{
		get
		{
			if (decoType != WaterDecoType.IndStart && decoType != WaterDecoType.IndMid)
			{
				return decoType == WaterDecoType.IndEnd;
			}
			return true;
		}
	}
}
