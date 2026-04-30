using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Picks;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Crew;

public abstract class CrewInfoGen
{
	public struct ButtonConfig
	{
		public enum AddType
		{
			Plain,
			Crew,
			Vehicle,
			Building
		}

		public static readonly ButtonConfig NONE;

		public bool show;

		public bool showBars;

		public Sprite sprite;

		public AddType addType;

		public bool ShowAdd => sprite == null;

		public ButtonConfig(Sprite sprite, AddType addType, bool showBars = false)
		{
			show = true;
			this.showBars = showBars;
			this.sprite = sprite;
			this.addType = addType;
		}

		public (Sprite sprite, bool showAdd, string addBgIcon) GetButtonAssets()
		{
			Color color = new Color(0.3f, 0.3f, 0.3f);
			string item = ((sprite != null) ? null : ((addType == AddType.Building) ? TextUtil.ColorWrap(Loc.Get("ui.crewmgmt.icon-building"), color) : ((addType == AddType.Vehicle) ? TextUtil.ColorWrap(Loc.Get("ui.crewmgmt.icon-vehicle"), color) : ((addType == AddType.Crew) ? TextUtil.ColorWrap(Loc.Get("ui.crewmgmt.icon-crew"), color) : null))));
			return (sprite: sprite, showAdd: sprite == null, addBgIcon: item);
		}
	}

	public struct HealthBarConfig
	{
		public bool show;

		public float p;

		public Color color;

		public Entity source;

		public static readonly HealthBarConfig NONE;

		public HealthBarConfig(bool show, float p, Color color, Entity source)
		{
			this.show = show;
			this.p = p;
			this.color = color;
			this.source = source;
		}
	}

	public static readonly string PLUS_SIGN = "Image_Plus";

	public readonly CrewCardInfo data;

	private const string PORTRAIT_SPRITE = "Portrait";

	private const string PORTRAIT_BARS = "Bars";

	private const string PORTRAIT_TEXT = "Text";

	private const string PORTRAIT_PLUS = "Plus";

	public virtual bool UseTwoImagePanel => false;

	public static Sprite GetPlusSignSprite()
	{
		return Game.ctx.hud.uisprites.uiatlas.Find(PLUS_SIGN);
	}

	public CrewInfoGen(CrewCardInfo data)
	{
		this.data = data;
	}

	internal static void UpdateCrewButton(GameObject portrait, ButtonConfig config)
	{
		var (sprite, value, text) = config.GetButtonAssets();
		portrait.SetActive(config.show);
		portrait.SetActive("Bars", config.showBars);
		portrait.SetImageOrHide("Portrait", sprite);
		portrait.SetActive("Plus", value);
		portrait.SetTextOrHide("Text", text);
	}

	public abstract string GetTextLineTop();

	public abstract string GetTextLineBottom();

	public virtual string GetMouseoverHeader()
	{
		return GetTextLineTop() + "\n" + GetTextLineBottom();
	}

	public virtual ButtonConfig GetFirstImage()
	{
		return ButtonConfig.NONE;
	}

	public virtual ButtonConfig GetSecondImage()
	{
		return ButtonConfig.NONE;
	}

	public virtual HealthBarConfig GetFirstImageHealth()
	{
		return HealthBarConfig.NONE;
	}

	public virtual HealthBarConfig GetSecondImageHealth()
	{
		return HealthBarConfig.NONE;
	}

	public virtual bool ShowTLAutomated()
	{
		return false;
	}

	public virtual bool ShowTRPeep()
	{
		return false;
	}

	public virtual bool ShowTRCorner()
	{
		return false;
	}

	public virtual bool ShowTRGoto()
	{
		return false;
	}

	public virtual bool ShowTRInspect()
	{
		return false;
	}

	public virtual bool ShowTRStopScheme()
	{
		return false;
	}

	public virtual bool ShowExtraAutomate()
	{
		return false;
	}

	public virtual bool ShowExtraPersonInfo()
	{
		return true;
	}

	public virtual bool ShowExtraInventory()
	{
		return true;
	}

	public virtual Color GetTRColor()
	{
		return GetCornerColor();
	}

	public virtual string GetExtraPersonInfo()
	{
		return GetPersonInfoDefault();
	}

	public virtual string GetExtraPersonInfoMO()
	{
		return GetPersonInfoDefaultMouseover();
	}

	public virtual string GetExtraInventory()
	{
		return GetInventoryDefault();
	}

	public virtual string GetExtraInventoryMO()
	{
		return GetInventoryDefaultMouseover();
	}

	public virtual void OnCardClick()
	{
	}

	public virtual void OnTRPeep()
	{
		OnPeepClick();
	}

	public virtual void OnTRCorner()
	{
		OnCornerClick();
	}

	public virtual void OnTRGoto()
	{
		OnGotoClick();
	}

	public virtual void OnTRInspect()
	{
		OnInspectClick();
	}

	public virtual void OnTRStopScheme()
	{
		OnStopSchemeClick();
	}

	protected static Sprite FetchCrewSpriteOrNull(Entity peep)
	{
		if (peep != null)
		{
			return HUDUtil.GetCrewSprite(peep);
		}
		return null;
	}

	protected static Sprite FetchVehicleSpriteOrNull(Entity veh)
	{
		if (veh != null)
		{
			return HUDUtil.GetVehicleSprite(veh);
		}
		return null;
	}

	public static ButtonConfig FetchSpriteButtonConfig(Entity entity)
	{
		if (entity?.components?.agent == null)
		{
			if (entity?.components?.mobile == null)
			{
				return new ButtonConfig(null, ButtonConfig.AddType.Plain);
			}
			return new ButtonConfig(FetchVehicleSpriteOrNull(entity), ButtonConfig.AddType.Vehicle);
		}
		return new ButtonConfig(FetchCrewSpriteOrNull(entity), ButtonConfig.AddType.Crew);
	}

	protected Entity GetBuildingManager()
	{
		if (!data.building.IsNotValid)
		{
			return ModulesUtil.GetManagerOrNull(data.building.FindEntity()).manager;
		}
		return null;
	}

	protected ButtonConfig GetPeepSpriteHelper()
	{
		Entity entity = (data.crew.peepId.IsValid ? data.crew.peepId.FindEntity() : (data.building.IsValid ? GetBuildingManager() : null));
		bool showBars = entity != null && Game.ctx.simman.cops.IsArrestedOrImprisoned(entity.Id);
		return new ButtonConfig(FetchCrewSpriteOrNull(entity), ButtonConfig.AddType.Crew, showBars);
	}

	protected ButtonConfig GetVehicleSpriteHelper()
	{
		return new ButtonConfig(FetchVehicleSpriteOrNull(data.emptyVehicle.FindEntity() ?? data.crew.GetVehicle()), ButtonConfig.AddType.Vehicle);
	}

	protected ButtonConfig GetBuildingBackModuleIcon()
	{
		return new ButtonConfig(ModulesUIUtil.FindIconBackModuleOrGambling(data.building.FindEntity()), ButtonConfig.AddType.Building);
	}

	protected HealthBarConfig GetPeepHealthHelper()
	{
		Entity entity = data.crew.peepId.FindEntity();
		float num = entity?.components.agent.CurrentHealthAsFraction ?? 1f;
		bool show = entity != null && num < 1f;
		Color32 color = entity?.components.agent.FindHealthInfo().color ?? ((Color32)Color.clear);
		return new HealthBarConfig(show, num, color, entity);
	}

	protected HealthBarConfig GetVehicleHealthHelper()
	{
		Entity entity = data.emptyVehicle.FindEntity() ?? data.crew.GetVehicle();
		float num = entity?.components.mobile.CurrentHealthAsFraction ?? 1f;
		VehicleSettings.HealthInfo healthInfo = entity?.components.mobile.FindHealthInfo();
		bool show = entity != null && num < 1f && healthInfo.showbar;
		Color32 color = healthInfo?.color ?? ((Color32)Color.clear);
		return new HealthBarConfig(show, num, color, entity);
	}

	protected string GetCrewName()
	{
		string crewPeepName = Game.ctx.players.Human.crew.GetCrewPeepName(data.crew, human: false);
		string key = (data.crew.IsDead ? "ui.crewinfo.namedead" : "ui.crewinfo.name");
		RoleDef roleDef = data.crew.GetPeep().data.agent.xp?.GetCrewRole();
		return ((roleDef != null) ? (Loc.Get(roleDef.locicon) + " ") : "") + Loc.Get(key, "name", crewPeepName);
	}

	protected string GetBizName()
	{
		return Loc.Get("ui.crewinfo.desc-manager", "bizname", BuildingUtil.FindBuildingName(data.building));
	}

	protected string GetBackModuleName()
	{
		bool flag = data.building.FindEntity().components.modules.gambling != null;
		string backModuleName = BuildingUtil.GetBackModuleName(data.building);
		if (backModuleName != null)
		{
			return Loc.Get("ui.crewinfo.module", "name", backModuleName);
		}
		if (!flag)
		{
			return Loc.Get("ui.crewinfo.nomodule");
		}
		if (!data.crew.IsNotAssigned)
		{
			return BuildingUtil.FindBuildingName(data.building);
		}
		return Loc.Get("ui.crewinfo.nomanager.gambling");
	}

	protected string GetEmptyVehicleName()
	{
		return data.emptyVehicle.FindEntity().config.mobile.GetName();
	}

	protected string DescribeCrewPeep()
	{
		CrewAssignment crew = data.crew;
		if (crew.IsDead)
		{
			return Loc.Get("ui.crewinfo.descdead");
		}
		var (flag, result) = PersonInfoUtil.FindActionString(crew.GetPeep(), crew.GetVehicle());
		if (crew.IsInVehicle)
		{
			if (!flag)
			{
				return MakePointsLine();
			}
			return result;
		}
		CopTracker cops = Game.ctx.simman.cops;
		if (cops.IsArrested(crew.peepId))
		{
			SimTime trialDate = cops.FindArrestOrNull(crew.peepId).trialDate;
			return Loc.Get("ui.crewinfo.desc-arrested", "date", Loc.FormatDateShort(trialDate));
		}
		if (cops.IsImprisoned(crew.peepId))
		{
			SimTime endDate = cops.FindImprisonedOrNull(crew.peepId).endDate;
			return Loc.Get("ui.crewinfo.desc-imprisoned", "date", Loc.FormatDateLong(endDate));
		}
		return Loc.Get("ui.crewinfo.desc-idle");
		string MakePointsLine()
		{
			Entity peep = crew.GetPeep();
			string text = Loc.FormatNumber(peep.components.agent.ActionsRemaining);
			string text2 = Loc.FormatNumber(peep.components.agent.MovesRemaining);
			return Loc.Get("ui.crewinfo.descpoints", "actions", text, "movements", text2);
		}
	}

	protected string DescribeEmptyVehicle()
	{
		return Loc.Get("ui.crewinfo.desc-idle");
	}

	protected Node GetCornerNode()
	{
		object obj = data.building.FindEntity()?.components.board.GetNode();
		if (obj == null)
		{
			Entity peep = data.crew.GetPeep();
			if (peep == null)
			{
				return null;
			}
			obj = peep.components.agent.GetNode();
		}
		return (Node)obj;
	}

	protected Entity GetGotoTarget()
	{
		return data.emptyVehicle.FindEntity() ?? data.building.FindEntity() ?? data.crew.GetPeep();
	}

	protected Color GetCornerColor()
	{
		Node cornerNode = GetCornerNode();
		if (cornerNode != null)
		{
			return BuildingPickUtil.GenerateCornerButtonColor(cornerNode);
		}
		return Color.clear;
	}

	protected void OnPeepClick()
	{
		Game.ctx.hud.personInfo.Show(data.crew.GetPeep());
	}

	protected void OnCornerClick()
	{
		Game.ctx.selection.ClearActive();
		Game.ctx.hud.cornerInfo.Show(data.crew, GetCornerNode());
	}

	protected void OnGotoClick()
	{
		PersonInfoUtil.TweenCameraToEntity(GetGotoTarget());
	}

	protected void OnInspectClick()
	{
		if (Game.serv.ui.ContainsPopup<CrewPeepInspectPopup>() && !(Game.serv.ui.TopPopupUnsafe is CrewPeepInspectPopup))
		{
			Game.serv.ui.RemovePopup<CrewPeepInspectPopup>();
		}
		Game.serv.ui.AddPopup(new CrewPeepInspectPopup(data.crew.peepId));
	}

	protected void OnStopSchemeClick()
	{
		SchemeData schemeForCrew = Game.ctx.players.Human.schemes.GetSchemeForCrew(data.crew.peepId);
		Game.serv.ui.AddPopup(new SchemePopup(schemeForCrew));
	}

	protected string GetPersonInfoDefault()
	{
		var (entity, text, text2) = GetTraitsAndExp(icons: true);
		if (entity == null)
		{
			if (!data.building.IsValid)
			{
				return "";
			}
			return Loc.Get("ui.crewinfo.nomanager.desc");
		}
		return text + "\n" + text2;
	}

	protected string GetPersonInfoDefaultMouseover()
	{
		var (entity, text, text2) = GetTraitsAndExp(icons: false);
		if (entity == null)
		{
			if (!data.building.IsValid)
			{
				return "";
			}
			return Loc.Get("ui.crewinfo.nomanager.desc");
		}
		return text + "\n\n" + text2;
	}

	private (Entity person, string traits, string exp) GetTraitsAndExp(bool icons)
	{
		Entity entity = GetBuildingManager() ?? data.crew.GetPeep();
		if (entity == null)
		{
			return (person: null, traits: null, exp: null);
		}
		string item = (icons ? PersonInfoUtil.GenerateTraitsIcons(entity) : PersonInfoUtil.GenerateTraitsList(entity, showDesc: true, showDescLong: false));
		string item2 = (icons ? PersonInfoUtil.GenerateLevelupIcons(entity) : PersonInfoUtil.GenerateLevelupDescription(entity, detailed: false));
		return (person: entity, traits: item, exp: item2);
	}

	private Entity GetEntityWithInventory()
	{
		return data.building.FindEntity() ?? data.emptyVehicle.FindEntity() ?? data.crew.GetVehicle();
	}

	protected string GetInventoryDefault()
	{
		return ModulesUtil.DescribeInventoryBrief(GetEntityWithInventory());
	}

	protected string GetInventoryDefaultMouseover()
	{
		Entity entityWithInventory = GetEntityWithInventory();
		if (entityWithInventory == null)
		{
			return Loc.Get("ui.contents.mo-empty");
		}
		return ModulesUtil.DescribeInventory(entityWithInventory);
	}
}
