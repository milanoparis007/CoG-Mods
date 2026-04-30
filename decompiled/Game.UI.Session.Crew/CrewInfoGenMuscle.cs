namespace Game.UI.Session.Crew;

public class CrewInfoGenMuscle : CrewInfoGen
{
	public override bool UseTwoImagePanel => true;

	public CrewInfoGenMuscle(CrewCardInfo data)
		: base(data)
	{
	}

	public override ButtonConfig GetFirstImage()
	{
		return GetPeepSpriteHelper();
	}

	public override ButtonConfig GetSecondImage()
	{
		return GetVehicleSpriteHelper();
	}

	public override HealthBarConfig GetFirstImageHealth()
	{
		return GetPeepHealthHelper();
	}

	public override HealthBarConfig GetSecondImageHealth()
	{
		return GetVehicleHealthHelper();
	}

	public override string GetTextLineTop()
	{
		return GetCrewName();
	}

	public override string GetTextLineBottom()
	{
		return DescribeCrewPeep();
	}

	public override bool ShowTRPeep()
	{
		return false;
	}

	public override bool ShowTRCorner()
	{
		return true;
	}

	public override bool ShowTRGoto()
	{
		return true;
	}

	public override bool ShowTRInspect()
	{
		return true;
	}

	public override bool ShowTRStopScheme()
	{
		return false;
	}
}
