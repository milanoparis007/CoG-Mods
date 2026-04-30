using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Sim;

public class VictoryGoal
{
	public Func<bool> visFunc;

	public VictoryResult result;

	public string locname;

	public string locdesc;

	public List<Label> grantTrophies;

	public string goaldesc;

	public List<VictorySubgoal> subgoals;

	public VictoryGoal(string locname, string locdesc, params VictorySubgoal[] subgoals)
		: this(locname, locdesc, null, VictoryTracker.visDefault, null, subgoals)
	{
	}

	public VictoryGoal(string locname, string locdesc, string goaldesc, params VictorySubgoal[] subgoals)
	{
		this.locname = locname;
		this.locdesc = locdesc;
		this.goaldesc = goaldesc;
		visFunc = VictoryTracker.visDefault;
		this.subgoals = subgoals.ToList();
		grantTrophies = null;
	}

	public VictoryGoal(string locname, string locdesc, string goaldesc, Func<bool> visFunc, params VictorySubgoal[] subgoals)
	{
		this.locname = locname;
		this.locdesc = locdesc;
		this.goaldesc = goaldesc;
		this.visFunc = visFunc;
		this.subgoals = subgoals.ToList();
		grantTrophies = null;
	}

	public VictoryGoal(string locname, string locdesc, string goaldesc, Func<bool> visFunc, List<Label> grantTrophies, params VictorySubgoal[] subgoals)
	{
		this.locname = locname;
		this.locdesc = locdesc;
		this.goaldesc = goaldesc;
		this.visFunc = visFunc;
		this.subgoals = subgoals.ToList();
		this.grantTrophies = grantTrophies;
	}

	public void RecomputeState()
	{
		foreach (VictorySubgoal subgoal in subgoals)
		{
			if (!subgoal.state.forced)
			{
				subgoal.RecomputeState();
			}
		}
		result = (subgoals.All((VictorySubgoal sub) => sub.state.result == VictoryResult.Pass) ? VictoryResult.Pass : (subgoals.Any((VictorySubgoal sub) => sub.state.result == VictoryResult.Fail) ? VictoryResult.Fail : VictoryResult.Unknown));
		if (result == VictoryResult.Pass && grantTrophies != null)
		{
			Game.ctx.players.Human.throne.TryGrantTrophies(grantTrophies, ignoreReqCount: true);
		}
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc, "goal", goaldesc);
	}

	public string GetIcon()
	{
		if (result != VictoryResult.Pass)
		{
			if (result != VictoryResult.Fail)
			{
				return Loc.Dot();
			}
			return Loc.Cross();
		}
		return Loc.Check();
	}

	public string Explain()
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		stringBuilder.AppendLine(GetDesc(), 2);
		foreach (VictorySubgoal subgoal in subgoals)
		{
			var (text, text2) = subgoal.ExplainSubgoal();
			stringBuilder.AppendLine(Loc.Get("victory.explain-header", "header", text, "explanation", text2));
		}
		return stringBuilder.ToStringAndReset();
	}
}
