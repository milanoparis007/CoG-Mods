using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Crew;

public class CrewInfoGenJob : CrewInfoGen
{
	public override bool UseTwoImagePanel => true;

	private AutomationExecutor AutoExec => Game.ctx.players.Human.automation;

	private bool IsExisting => data.automation.IsValid;

	private bool IsNewJob => data.automation.IsNotValid;

	public CrewInfoGenJob(CrewCardInfo data)
		: base(data)
	{
	}

	public override ButtonConfig GetFirstImage()
	{
		if (!IsNewJob)
		{
			return GetPeepSpriteHelper();
		}
		return ButtonConfig.NONE;
	}

	public override ButtonConfig GetSecondImage()
	{
		if (!IsNewJob)
		{
			return GetVehicleSpriteHelper();
		}
		return ButtonConfig.NONE;
	}

	public override HealthBarConfig GetFirstImageHealth()
	{
		if (!IsNewJob)
		{
			return GetPeepHealthHelper();
		}
		return HealthBarConfig.NONE;
	}

	public override HealthBarConfig GetSecondImageHealth()
	{
		if (!IsNewJob)
		{
			return GetVehicleHealthHelper();
		}
		return HealthBarConfig.NONE;
	}

	private AutomationSequence GetAuto()
	{
		return AutoExec.GetAutoOrNull(data.automation);
	}

	private CrewAssignment FindCrew()
	{
		return GetAuto()?.FindCrew() ?? CrewAssignment.EMPTY;
	}

	public override string GetTextLineTop()
	{
		if (!IsNewJob)
		{
			if (!FindCrew().IsNotValid)
			{
				return GetCrewName();
			}
			return Loc.Get("ui.crewinfo.delivery-noassign");
		}
		return Loc.Get("ui.crewinfo.delivery-setup");
	}

	public override string GetTextLineBottom()
	{
		object obj;
		if (!IsNewJob)
		{
			obj = GetAuto()?.name;
			if (obj == null)
			{
				return "";
			}
		}
		else
		{
			obj = "";
		}
		return (string)obj;
	}

	public override bool ShowTLAutomated()
	{
		return data.crew.IsInVehicle;
	}

	public bool IsPaused()
	{
		return GetAuto()?.IsAutoNotActive ?? false;
	}

	public override bool ShowTRPeep()
	{
		return false;
	}

	public override bool ShowTRCorner()
	{
		return data.crew.IsInVehicle;
	}

	public override bool ShowTRGoto()
	{
		return data.crew.IsInVehicle;
	}

	public override bool ShowTRInspect()
	{
		return data.crew.IsInVehicle;
	}

	public override bool ShowTRStopScheme()
	{
		return false;
	}

	public override void OnCardClick()
	{
		if (!IsExisting)
		{
			AutoExec.MakeNewAutomationSequence();
		}
	}
}
