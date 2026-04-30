using System;

namespace Game.Session.Heatmaps;

[Flags]
public enum GroundFlags
{
	None = 0,
	NearBuilding = 1,
	VeryNearBuilding = 2,
	NearRoadOrRail = 4,
	VeryNearRoadOrRail = 8,
	_Other = 0x10,
	NotNone = 0xF,
	Any = 0x1F
}
