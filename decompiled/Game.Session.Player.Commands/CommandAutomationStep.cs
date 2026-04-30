using Game.Core;
using Game.Services;

namespace Game.Session.Player.Commands;

public sealed class CommandAutomationStep : NormalCommand
{
	public EntityID buildingId;

	public override string Message => "";

	public CommandAutomationStep()
	{
	}

	public CommandAutomationStep(PlayerID pid, EntityID peepId, EntityID building)
		: base(pid, CommandType.AutomationAction, peepId)
	{
		buildingId = building;
	}

	protected override CrewCost CalculateCost()
	{
		return GetCost();
	}

	protected override void OnStarted()
	{
		base.OnStarted();
		CrewAssignment crewForPeep = GetPlayer().crew.GetCrewForPeep(peepId);
		crewForPeep.GetPeep().components.agent.AddXP(XPSource.FromDelivery);
		GetPlayer().automation.PerformCurrentStep(crewForPeep, buildingId);
	}

	public static CrewCost GetCost()
	{
		return Game.serv.globals.settings.people.social.costs.convoCost;
	}

	public static bool CanExecuteOneMore(CrewAssignment crew)
	{
		return crew.GetPeep().components.agent.CanPay(GetCost());
	}
}
