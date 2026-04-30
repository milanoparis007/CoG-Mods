using System;
using System.Collections;
using System.Text;
using Game.Core;
using Game.Services;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Quests;

public class GoalTracker : AbstractSessionManager, ISaveLoadProvider
{
	private GoalTrackerData _data;

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
		_data = new GoalTrackerData();
	}

	public override void OnReleased()
	{
		_data = null;
		base.OnReleased();
	}

	public override void OnPreInteractive()
	{
		base.OnPreInteractive();
		if (Game.ctx.HasSaveFile)
		{
			ReinitializeLoadedGoals();
		}
	}

	private void ReinitializeLoadedGoals()
	{
		if (_data.goals == null)
		{
			return;
		}
		foreach (BaseGoal value in _data.goals.Values)
		{
			try
			{
				value.OnAdded();
			}
			catch (Exception ex)
			{
				Logger.Error("Failed to reinitialize mission goal " + value.LocDescKey, ex.Message);
			}
		}
	}

	public BaseGoal FindGoalOrNull(string goalid)
	{
		return _data.goals.FindOrNull(goalid);
	}

	public bool HasRegisteredGoal(string goalid)
	{
		return _data.goals.ContainsKey(goalid);
	}

	public BaseGoal CloneAndRegisterGoal(BaseGoal original, string guuid, EntityID target)
	{
		BaseGoal baseGoal = Game.serv.serializer.instance.Clone(original);
		_data.goals.Add(guuid, baseGoal);
		baseGoal.OnInitialize(guuid, target);
		baseGoal.OnAdded();
		return baseGoal;
	}

	public bool UnregisterClonedGoal(string goalid)
	{
		BaseGoal baseGoal = _data.goals[goalid];
		if (baseGoal != null)
		{
			baseGoal.OnRemoved();
			return _data.goals.Remove(goalid);
		}
		Logger.Warning("Missing goal during unregister: " + goalid);
		return false;
	}

	public float ProduceGoalCompletionFraction(string goalid)
	{
		BaseGoal baseGoal = FindGoalOrNull(goalid);
		if (baseGoal == null)
		{
			Logger.Warning("Invalid goal id: " + goalid);
			return 0f;
		}
		return baseGoal.ProduceGoalCompletionFraction();
	}

	public void ProduceFutureGoalDescription(BaseGoal original, EntityID target, StringBuilder sb)
	{
		BaseGoal baseGoal = Game.serv.serializer.instance.Clone(original);
		baseGoal.OnInitialize("", target);
		baseGoal.OnAdded();
		ProduceGoalDescription(baseGoal, sb);
		baseGoal.OnRemoved();
	}

	public void ProduceActiveGoalDescription(string goalid, StringBuilder sb)
	{
		BaseGoal baseGoal = FindGoalOrNull(goalid);
		if (baseGoal == null)
		{
			Logger.Warning("Missing goal " + goalid);
		}
		else
		{
			ProduceGoalDescription(baseGoal, sb);
		}
	}

	public string GetGoalHeader()
	{
		return Loc.Get("goal.item.title");
	}

	private void ProduceGoalDescription(BaseGoal goal, StringBuilder sb)
	{
		string key = (goal.state.completed ? "goal.item.done" : "goal.item.todo");
		sb.AppendLine(Loc.Get(key, "line", goal.GetLocDesc()));
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", _data.Save(s));
	}

	public IEnumerator Load(Hashtable data)
	{
		_data.Populate(data["data"] as Hashtable);
		yield break;
	}
}
