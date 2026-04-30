using System.Collections.Generic;

namespace Game.Core;

public sealed class PlayerIDComparer : IComparer<PlayerID>
{
	public int Compare(PlayerID x, PlayerID y)
	{
		return x.CompareTo(y);
	}
}
