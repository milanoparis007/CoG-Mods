using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public sealed class TerritorySettings
{
	public class TODColorSegment
	{
		public FloatRange hourRange;

		public Color colorMultiplier;

		public float nightValue;
	}

	public class TODColors
	{
		public float timeASRWidth;

		public Color emissiveMultiplier;

		public List<TODColorSegment> segments;
	}

	public TODColors timeOfDayColors;

	public FloatRange territoryDisplayZoomRange;

	public FloatRange showWeatherZoomRange;

	public FloatRange showCloudsZoomRange;

	public FloatRange showBuildingPicksZoomRange;

	public FloatRange showSummaryPicksZoomRange;
}
