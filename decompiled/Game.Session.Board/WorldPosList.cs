using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Board;

public sealed class WorldPosList : List<WorldPos>, IObjectPoolElement
{
	public void Reset()
	{
		Clear();
	}
}
