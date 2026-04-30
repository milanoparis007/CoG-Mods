using System;

namespace Game.Session.Data;

[Flags]
public enum ModQueryElement
{
	Nothing = 0,
	Node = 1,
	Player = 2,
	Target = 4,
	CrewPeep = 8
}
