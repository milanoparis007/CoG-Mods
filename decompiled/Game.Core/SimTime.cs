using System;
using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct SimTime : IEquatable<SimTime>
{
	public static readonly SimTime MIN_DATE = new SimTime(int.MinValue);

	public static readonly SimTime MAX_DATE = new SimTime(int.MaxValue);

	public const int DAYS_PER_YEAR = 365;

	public int days;

	public float YearsFloat => (float)days / 365f;

	public int YearsInt => days / 365;

	public int DayOfYear => days % 365;

	public bool NeverHappens => days == MAX_DATE.days;

	public bool IsMinDate => days == MIN_DATE.days;

	private string DebugString => ToString();

	public SimTime(int day)
	{
		days = day;
	}

	public SimTime(int year, int dayOfYear)
	{
		days = year * 365 + dayOfYear;
	}

	public SimTime Increment(SimTimeSpan delta)
	{
		return new SimTime(days + delta.deltadays);
	}

	public SimTime IncrementDays(int deltaDays)
	{
		return new SimTime(days + deltaDays);
	}

	public SimTime IncrementYears(float deltaYears)
	{
		return new SimTime(days + (int)(deltaYears * 365f));
	}

	public SimTime IncrementTurns(int turns)
	{
		return IncrementDays(Game.ctx.clock.TurnsToDays(turns));
	}

	public SimTimeSpan Subtract(SimTime other)
	{
		return new SimTimeSpan(days - other.days);
	}

	public static SimTimeSpan operator -(SimTime a, SimTime b)
	{
		return a.Subtract(b);
	}

	public static bool operator >(SimTime a, SimTime b)
	{
		return a.days > b.days;
	}

	public static bool operator <(SimTime a, SimTime b)
	{
		return a.days < b.days;
	}

	public static bool operator >=(SimTime a, SimTime b)
	{
		return a.days >= b.days;
	}

	public static bool operator <=(SimTime a, SimTime b)
	{
		return a.days <= b.days;
	}

	public static bool Equals(SimTime a, SimTime b)
	{
		return a.days == b.days;
	}

	public bool Equals(SimTime other)
	{
		return Equals(this, other);
	}

	public override bool Equals(object obj)
	{
		if (obj is SimTime a)
		{
			return Equals(a, this);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return days;
	}

	public override string ToString()
	{
		if (!NeverHappens)
		{
			if (!IsMinDate)
			{
				return $"SimTime {ToDate().ToShortDateString()} / {days} days / {YearsFloat} yrs";
			}
			return "SimTime MIN_TIME";
		}
		return "SimTime NEVER";
	}

	public static int CompareAscending(SimTime a, SimTime b)
	{
		return a.days - b.days;
	}

	public static int CompareDescending(SimTime a, SimTime b)
	{
		return b.days - a.days;
	}

	public DateTime ToDate()
	{
		try
		{
			TimeSpan value = new TimeSpan(DayOfYear, 0, 0, 0);
			return new DateTime(YearsInt, 1, 1).Add(value);
		}
		catch (Exception)
		{
			return new DateTime(1920, 1, 1);
		}
	}
}
