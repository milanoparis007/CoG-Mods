using System.Diagnostics;

namespace Game.Core;

[DebuggerDisplay("{DebugString}")]
public struct SimTimeSpan
{
	public int deltadays;

	public float YearsFloat => (float)deltadays / 365f;

	public int YearsInt => deltadays / 365;

	private string DebugString => ToString();

	public SimTimeSpan(int deltadays)
	{
		this.deltadays = deltadays;
	}

	public static SimTimeSpan FromYears(int years, int daysInYear)
	{
		return new SimTimeSpan(years * 365 + daysInYear);
	}

	public static SimTimeSpan FromYears(float years, float daysInYear)
	{
		return new SimTimeSpan((int)(years * 365f + daysInYear));
	}

	public static SimTimeSpan FromTurns(int turns)
	{
		return new SimTimeSpan(Game.ctx.clock.TurnsToDays(turns));
	}

	public static SimTimeSpan FromDays(int days)
	{
		return new SimTimeSpan(days);
	}

	public override string ToString()
	{
		return $"SimTimeSpan {deltadays} days / {YearsFloat} years";
	}
}
