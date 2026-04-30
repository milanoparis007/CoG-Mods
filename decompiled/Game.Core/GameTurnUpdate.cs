using System;
using SomaSim.Util;

namespace Game.Core;

public sealed class GameTurnUpdate
{
	public SimTime now;

	public SimTime previous;

	public int turn;

	public SimTime turnOneDate;

	public SimTime firstTurnThisYearDate;

	public PlayerID pid;

	public int daysPerTurn;

	public bool DidCrossMonthBoundaries()
	{
		return CountCrossedMonthBoundaries() > 0;
	}

	public int CountCrossedMonthBoundaries()
	{
		return CountCrossedMonthBoundaries(previous, now);
	}

	public static int CountCrossedMonthBoundaries(SimTime previous, SimTime now)
	{
		DateTime dateTime = previous.ToDate();
		DateTime dateTime2 = now.ToDate();
		int num = dateTime2.Month + dateTime2.Year * 12;
		int num2 = dateTime.Month + dateTime.Year * 12;
		return MathUtil.ClampMin(num - num2, 0);
	}
}
