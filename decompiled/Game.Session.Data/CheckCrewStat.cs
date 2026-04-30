using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public sealed class CheckCrewStat : AbstractVisitRequirement
{
	public enum Type
	{
		Individual,
		Highwater
	}

	public CrewStats id;

	public Type type;

	public Test @is;

	public int value;

	public override bool DoesPass(VisitState visit)
	{
		if (type == Type.Individual)
		{
			return ValueUtil.TestCurrentValue(visit.crew.GetPeep().data.agent?.crewHistoryStats[id] ?? 0, @is, value);
		}
		if (type == Type.Highwater)
		{
			foreach (CrewAssignment item in visit.GetPlayer().crew.AllCrew)
			{
				if (ValueUtil.TestCurrentValue(item.GetPeep().data.agent?.crewHistoryStats[id] ?? 0, @is, value))
				{
					return true;
				}
			}
			return false;
		}
		return false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.crewinfo.stat", "stat", Loc.GetCrewStatWithValueColon(id, value.ToString()), "current", visit.crew.GetPeep().data.agent?.crewHistoryStats[id] ?? 0));
	}
}
