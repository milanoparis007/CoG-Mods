using System;
using Game.Core;

namespace Game.Session.Player.AI;

public class AdvisorRequest
{
	public enum Priority
	{
		PoliceCollectVehicle = 55,
		PoliceOrFedVisit = 50,
		DefendOutpost = 31,
		GoToMeetingPoint = 30,
		AttackAggroCoordinated = 25,
		SupportOutpost = 20,
		AttackAggroBuilding = 18,
		InstallBackroom = 15,
		StartOutpost = 12,
		TakeoverBuilding = 11,
		AttackAggroTarget = 5,
		HarassBusiness = 2,
		BuySellItem = 1,
		Default = 0,
		ScopeOutBuilding = -10,
		ExploreNode = -15
	}

	public PlayerID pid;

	public Label script;

	public EntityID assignedTo;

	public Priority priority;

	public Deictics variables;

	public AIAdvisor advisor;

	public AdvisorRequest()
	{
		throw new Exception("AdvisorRequest should not be saved in the save file!");
	}

	public AdvisorRequest(AIAdvisor advisor, Label script, Priority priority = Priority.Default, Deictics variables = null)
		: this(advisor, script, default(EntityID), priority, variables)
	{
	}

	public AdvisorRequest(AIAdvisor advisor, Label script, EntityID assignedTo, Priority priority = Priority.Default, Deictics variables = null)
	{
		this.advisor = advisor;
		pid = advisor.PID;
		this.script = script;
		this.assignedTo = assignedTo;
		this.priority = priority;
		this.variables = variables;
	}

	public override string ToString()
	{
		return $"{pid}: {assignedTo} => {script}";
	}
}
