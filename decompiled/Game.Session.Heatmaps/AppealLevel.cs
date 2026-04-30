using System;

namespace Game.Session.Heatmaps;

[Flags]
public enum AppealLevel
{
	None = 0,
	Low = 1,
	Mid = 2,
	High = 4,
	_Other = 8,
	LowMid = 3,
	MidHigh = 6,
	LowMidHigh = 7,
	Any = 0xF
}
