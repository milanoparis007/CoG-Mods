using System;

namespace Game.Core;

[Flags]
public enum TransitFlags
{
	Empty = 0,
	Terminal = 1,
	Rail = 4,
	Road = 0x10
}
