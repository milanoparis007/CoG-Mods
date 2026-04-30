using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class AutomationData
{
	public int gensym;

	public List<AutomationSequence> sequences = new List<AutomationSequence>();

	public int IndexOf(EntityID vehicle)
	{
		int i = 0;
		for (int count = sequences.Count; i < count; i++)
		{
			if (sequences[i].vehicle == vehicle)
			{
				return i;
			}
		}
		return -1;
	}

	public int IndexOf(AutomationID id)
	{
		int i = 0;
		for (int count = sequences.Count; i < count; i++)
		{
			if (AutomationID.Equals(sequences[i].id, id))
			{
				return i;
			}
		}
		return -1;
	}

	public AutomationSequence GetOrNull(AutomationID id)
	{
		return sequences.GetOrDefaultFast(IndexOf(id));
	}

	public AutomationSequence GetOrNull(EntityID vehicle)
	{
		return sequences.GetOrDefaultFast(IndexOf(vehicle));
	}
}
