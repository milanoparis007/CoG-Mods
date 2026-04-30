using System.Collections;
using System.Collections.Generic;
using SomaSim.SION;

namespace Game.Session.Quests;

public class GoalTrackerData
{
	public Dictionary<string, BaseGoal> goals { get; private set; }

	public GoalTrackerData()
	{
		goals = new Dictionary<string, BaseGoal>();
	}

	public Hashtable Save(Serializer ser)
	{
		Hashtable hashtable = new Hashtable();
		foreach (KeyValuePair<string, BaseGoal> goal in goals)
		{
			object value = ser.Serialize(goal.Value, specifyValueTypes: true);
			hashtable[goal.Key] = value;
		}
		return hashtable;
	}

	public void Populate(Hashtable source)
	{
		if (source == null)
		{
			return;
		}
		Serializer instance = Game.serv.serializer.instance;
		foreach (object key2 in source.Keys)
		{
			object source2 = source[key2];
			BaseGoal baseGoal = instance.Deserialize(source2) as BaseGoal;
			if (key2 is string key && baseGoal != null)
			{
				goals[key] = baseGoal;
			}
			else
			{
				Logger.Warning("Malformed serialized goal: " + key2);
			}
		}
	}
}
