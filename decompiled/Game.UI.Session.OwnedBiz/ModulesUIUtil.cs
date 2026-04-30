using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.OwnedBiz;

public static class ModulesUIUtil
{
	private static readonly string LARGE_BANNERS_RESOURCES_FOLDER = "UI Images/Large Banners/";

	public static Sprite FindLargeBannerOrNull(string name)
	{
		if (name == null)
		{
			return null;
		}
		return Resources.Load<Sprite>(LARGE_BANNERS_RESOURCES_FOLDER + name);
	}

	public static Sprite FindModuleIcon(ModuleCommon common)
	{
		Label name = ((common == null) ? ModuleCommon.Display.ICON_ADD : (common.display?.icon ?? Label.NULL));
		return FindSpriteOrWarn(Game.ctx.hud.uisprites.moduleIcons, name, ModuleCommon.Display.ICON_MISSING);
	}

	public static Sprite FindIconBackModuleOrGambling(Entity building)
	{
		ModuleCommon moduleCommon = (building?.components.modules.FindBackroomModule() ?? building?.components.modules.gambling)?.ModuleConfig?.Common;
		if (moduleCommon == null)
		{
			return null;
		}
		return FindModuleIcon(moduleCommon);
	}

	public static string FindModuleName(ModuleCommon common)
	{
		string text = common?.display?.locname;
		if (text == null)
		{
			return "";
		}
		return Loc.Get(text);
	}

	public static string FindModuleDesc(ModuleCommon common)
	{
		string text = common?.display?.locdesc;
		if (text == null)
		{
			return "";
		}
		return Loc.Get(text);
	}

	public static Sprite FindBusinessEmptySprite(Entity building)
	{
		return FindLargeBannerOrNull(BuildingUtil.FindBizForBuilding(building)?.config.biz?.emptyImage);
	}

	public static Sprite FindModuleReplacementSprite(ModuleCommon common)
	{
		return FindLargeBannerOrNull(common?.display?.replacementSprite);
	}

	public static Sprite FindModuleOperationSprite(ModuleCommon common)
	{
		return FindLargeBannerOrNull(common?.display?.operationSprite);
	}

	public static Sprite FindModuleAccentSprite(ModuleCommon common)
	{
		return FindLargeBannerOrNull(common?.display?.accentSprite);
	}

	public static Sprite FindVehicleIcon(Entity vehicle)
	{
		return Game.ctx.hud.uisprites.moduleIcons.Find(vehicle.config.mobile.uiSprite);
	}

	public static Sprite FindSpriteOrWarn(SpriteAtlasCache cache, Label name, Label @default)
	{
		Sprite sprite = cache.Find(name.String);
		if (sprite == null)
		{
			sprite = cache.Find(@default.String);
		}
		return sprite;
	}

	public static (string text, int daysleft) DescribeEnable(IModule mod, SimTime time, bool shortForm)
	{
		if (mod.IsEnabled(time))
		{
			return (text: "", daysleft: 0);
		}
		int num = mod.ModuleData.EnableTime.days - time.days;
		int num2 = Game.ctx.clock.DaysToTurnsRoundedUp(num);
		return (text: TextUtil.ColorWrap(shortForm ? Loc.Get("module.ui-util.describe.short") : Loc.Get("module.ui-util.describe.full", "daysLeft", num, "turnsLeft", num2), ColorConstants.TEXT_HEX_CONSTRUCTION), daysleft: num);
	}

	public static (bool damaged, string text) DescribeDamaged(Entity building)
	{
		bool num = building.components.building.HasDamage();
		string item = ((!num) ? null : TextUtil.ColorWrap(Loc.Get("module.ui-util.describe.damaged"), ColorConstants.TEXT_HEX_RED));
		return (damaged: num, text: item);
	}

	public static void RefreshModuleBackground(GameObject go, Entity building, ModuleCommon common)
	{
		Sprite sprite = null;
		Sprite sprite2 = null;
		Sprite sprite3 = FindModuleReplacementSprite(common);
		if (sprite3 == null)
		{
			sprite3 = FindBusinessEmptySprite(building);
			sprite = FindModuleOperationSprite(common);
			sprite2 = FindModuleAccentSprite(common);
		}
		go.SetImageOrHide("Backroom", sprite3);
		go.SetImageOrHide("Operation", sprite);
		go.SetImageOrHide("Accent", sprite2);
		go.SetActive("Border", sprite3 != null);
	}
}
