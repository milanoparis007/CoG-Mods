using Game.Services;
using Game.Session.Entities;

namespace Game.UI.Session.Crew;

public class CrewInfoGenJustPeep : CrewInfoGen
{
	public CrewInfoGenJustPeep(CrewCardInfo data)
		: base(data)
	{
	}

	public override ButtonConfig GetFirstImage()
	{
		return GetPeepSpriteHelper();
	}

	public override HealthBarConfig GetFirstImageHealth()
	{
		return GetPeepHealthHelper();
	}

	public override string GetTextLineTop()
	{
		return GetCrewName();
	}

	public override string GetTextLineBottom()
	{
		return DescribeCrewPeep();
	}

	public override string GetMouseoverHeader()
	{
		return base.GetMouseoverHeader() + MaybeExplainJailStatus();
	}

	private string MaybeExplainJailStatus()
	{
		Entity peep = data.crew.GetPeep();
		if (((peep != null) ? Game.ctx.simman.cops.FindArrestOrNull(peep.Id) : null) != null)
		{
			return "\n\n" + Loc.Get("ui.crewinfo.mo-arrested", "name", peep.data.person.ShortName);
		}
		return "";
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
		return false;
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
