using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public sealed class PlayerTradeSummaryList : List<PlayerTradeSummary>
{
	public PlayerTradeSummary GetOrNull(PlayerID pid)
	{
		using (Enumerator enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				PlayerTradeSummary current = enumerator.Current;
				if (current.pid == pid)
				{
					return current;
				}
			}
		}
		return null;
	}

	public PlayerTradeSummary GetOrMake(PlayerID pid)
	{
		PlayerTradeSummary orNull = GetOrNull(pid);
		if (orNull != null)
		{
			return orNull;
		}
		PlayerTradeSummary playerTradeSummary = new PlayerTradeSummary
		{
			pid = pid
		};
		Add(playerTradeSummary);
		return playerTradeSummary;
	}
}
