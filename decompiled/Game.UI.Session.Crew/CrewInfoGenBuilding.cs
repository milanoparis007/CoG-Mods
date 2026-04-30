using Game.Services;
using Game.Session.Entities;

namespace Game.UI.Session.Crew;

public class CrewInfoGenBuilding : CrewInfoGen
{
	public override bool UseTwoImagePanel => true;

	public CrewInfoGenBuilding(CrewCardInfo data)
		: base(data)
	{
	}

	public override ButtonConfig GetFirstImage()
	{
		return GetBuildingBackModuleIcon();
	}

	public override ButtonConfig GetSecondImage()
	{
		return GetPeepSpriteHelper();
	}

	public override string GetTextLineTop()
	{
		return DescribeManagerOrBuilding();
	}

	public override string GetTextLineBottom()
	{
		return GetBackModuleName();
	}

	public override string GetMouseoverHeader()
	{
		return DescribeBuildingInMouseover();
	}

	public override string GetExtraInventory()
	{
		return CrewDialogModuleUtil.DescribeBackModule(data.building.FindEntity()) ?? GetInventoryDefault();
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
		return GetBuildingManager() != null;
	}

	public override bool ShowTRStopScheme()
	{
		return false;
	}

	protected string DescribeManagerOrBuilding()
	{
		Entity buildingManager = GetBuildingManager();
		if (buildingManager != null)
		{
			RoleDef roleDef = buildingManager.data.agent.xp?.GetCrewRole();
			return ((roleDef != null) ? (Loc.Get(roleDef.locicon) + " ") : "") + Loc.Get("ui.crewinfo.manager", "name", buildingManager.data.person.ShortName);
		}
		return GetBizName();
	}

	protected string DescribeBuildingInMouseover()
	{
		string bizName = GetBizName();
		bool flag = data.building.FindEntity().components.modules.gambling != null;
		string text = (data.building.FindEntity()?.components.modules?.FindBackroomModule())?.ModuleConfig.Common.display.locname;
		bool flag2 = text != null;
		string text2 = (flag2 ? Loc.Get(text) : Loc.Get("ui.crewinfo.building.mo.none"));
		Entity buildingManager = GetBuildingManager();
		bool flag3 = buildingManager != null;
		string text3 = (flag3 ? buildingManager.data.person.ShortName : Loc.Get("ui.crewinfo.building.mo.none"));
		string text4 = Loc.Get("ui.crewinfo.building.mo", "bizname", bizName, "opname", flag ? BuildingUtil.FindBuildingName(data.building) : text2, "peepname", text3);
		if (!flag2 && !flag)
		{
			text4 += Loc.Get("ui.crewinfo.building.mo.nooperation");
		}
		if (!flag3)
		{
			text4 += Loc.Get("ui.crewinfo.building.mo.nomanager");
		}
		return text4;
	}
}
