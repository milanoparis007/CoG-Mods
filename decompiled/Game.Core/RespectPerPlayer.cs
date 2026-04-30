using SomaSim.Util;

namespace Game.Core;

public sealed class RespectPerPlayer : PlayerKeyedList<Respect>
{
	public bool IsAnyPositive()
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].AnyValue)
			{
				return true;
			}
		}
		return false;
	}

	public (PlayerID pid, Fixnum highest) GetHighestCurrentRespect()
	{
		PlayerID item = PlayerID.INVALID;
		Fixnum fixnum = Fixnum.MIN_VALUE;
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].current > fixnum)
			{
				fixnum = data[i].current;
				item = data[i].pid;
			}
		}
		return (pid: item, highest: fixnum);
	}

	public void ResetPerTurnRespect()
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			data[i].ResetPerTurnValue();
		}
	}

	public void IncrementAOERespect(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromAOE += delta;
	}

	public void IncrementBizRespect(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromRelationships += delta;
	}

	public void IncrementNeighborRespect(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromNeighbors += delta;
	}

	public void IncrementEthnicity(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromEthnicity += delta;
	}

	public void IncrementSafehouse(PlayerID pid, Fixnum delta)
	{
		GetOrAdd(pid).fromSafehouse += delta;
	}

	public void ClearSafehouse(PlayerID pid)
	{
		GetOrAdd(pid).fromSafehouse = 0;
	}
}
