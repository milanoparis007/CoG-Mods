namespace Game.UI.Session.Crew;

public class CrewInfoGenJustVehicle : CrewInfoGen
{
	public CrewInfoGenJustVehicle(CrewCardInfo data)
		: base(data)
	{
	}

	public override ButtonConfig GetFirstImage()
	{
		return GetVehicleSpriteHelper();
	}

	public override HealthBarConfig GetFirstImageHealth()
	{
		return GetVehicleHealthHelper();
	}

	public override string GetTextLineTop()
	{
		return GetEmptyVehicleName();
	}

	public override string GetTextLineBottom()
	{
		return DescribeEmptyVehicle();
	}

	public override bool ShowTRPeep()
	{
		return false;
	}

	public override bool ShowTRCorner()
	{
		return false;
	}

	public override bool ShowTRGoto()
	{
		return true;
	}

	public override bool ShowTRInspect()
	{
		return false;
	}

	public override bool ShowTRStopScheme()
	{
		return false;
	}
}
