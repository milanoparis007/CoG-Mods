using System;

namespace Game.Core;

[Flags]
public enum NeighborFlags
{
	None = 0,
	North = 1,
	East = 2,
	South = 4,
	West = 8,
	EastWest = 0xA,
	NorthSouth = 5,
	NorthEast = 3,
	SouthEast = 6,
	SouthWest = 0xC,
	NorthWest = 9,
	NorthEastWest = 0xB,
	NorthEastSouth = 7,
	EastSouthWest = 0xE,
	NorthSouthWest = 0xD,
	NorthEastSouthWest = 0xF
}
