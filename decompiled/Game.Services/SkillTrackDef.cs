using System.Collections.Generic;
using System.Linq;
using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class SkillTrackDef : List<List<Label>>
{
	public List<Label> GenerateFlatList(IRandom rng)
	{
		return this.Select((List<Label> sublist) => rng.ShuffleCopy(sublist)).SelectMany((List<Label> e) => e).ToList();
	}
}
