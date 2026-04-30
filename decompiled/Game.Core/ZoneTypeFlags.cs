using System;

namespace Game.Core;

[Flags]
public enum ZoneTypeFlags
{
	None = 0,
	Res = 1,
	Com = 2,
	Ind = 4,
	_Other = 8,
	ResCom = 3,
	ResInd = 5,
	ComInd = 6,
	ResComInd = 7,
	Any = 0xF
}
