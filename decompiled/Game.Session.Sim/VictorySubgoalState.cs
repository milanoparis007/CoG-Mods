using SomaSim.Util;

namespace Game.Session.Sim;

public struct VictorySubgoalState
{
	public Fixnum current;

	public Fixnum goal;

	public VictoryResult result;

	public bool forced;

	public VictorySubgoalState(Fixnum current, Fixnum goal, VictoryResult result)
	{
		this.current = current;
		this.goal = goal;
		this.result = result;
		forced = false;
	}

	public VictorySubgoalState(Fixnum current, Fixnum goal, bool pass)
	{
		this.current = current;
		this.goal = goal;
		result = (pass ? VictoryResult.Pass : VictoryResult.Unknown);
		forced = false;
	}

	public bool? DoesPass()
	{
		if (result != VictoryResult.Pass)
		{
			if (result != VictoryResult.Fail)
			{
				return null;
			}
			return false;
		}
		return true;
	}

	public string GetKey()
	{
		if (result != VictoryResult.Pass)
		{
			if (result != VictoryResult.Fail)
			{
				return "victory.line.todo";
			}
			return "victory.line.fail";
		}
		return "victory.line.pass";
	}
}
