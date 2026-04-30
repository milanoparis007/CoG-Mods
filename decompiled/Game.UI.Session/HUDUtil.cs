using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public static class HUDUtil
{
	public enum ZoomInLevel
	{
		Full,
		PullBack,
		PullBackFurther,
		ShowNames,
		None
	}

	private static readonly string PORTRAIT_NONE = "CrewNone";

	public static void GoTo(WorldPos pos, bool zoomIn = false, bool showFx = false)
	{
		GoTo(pos, (!zoomIn) ? ZoomInLevel.None : ZoomInLevel.Full, showFx);
	}

	public static void GoTo(WorldPos pos, ZoomInLevel zoomIn, bool showFx = false)
	{
		Game.serv.camera.SetPosition(pos, CameraTween.SLOW_FOCUS_TWEEN);
		if (zoomIn != ZoomInLevel.None)
		{
			float minZZoom = Game.serv.camera.Settings.minZZoom;
			float maxZZoom = Game.serv.camera.Settings.maxZZoom;
			float zoom = zoomIn switch
			{
				ZoomInLevel.ShowNames => MathUtil.Interpolate(0.75f, minZZoom, maxZZoom), 
				ZoomInLevel.PullBackFurther => minZZoom * 3f, 
				ZoomInLevel.PullBack => minZZoom * 2f, 
				ZoomInLevel.Full => minZZoom, 
				_ => minZZoom, 
			};
			Game.serv.camera.SetZoom(zoom, CameraTween.SLOW_ZOOM_TWEEN);
		}
		if (showFx)
		{
			Game.ctx.vfx.PlayOneShotPFX(PFXType.ZoomToFX, pos, PlayerID.HumanPlayer);
		}
	}

	public static void MoveToScreenPos(GameObject go, Vector2 screenpos)
	{
		go.GetComponent<RectTransform>().anchoredPosition = screenpos;
	}

	public static void MoveToWorldPos(GameObject go, WorldPos worldpos)
	{
		Vector2 anchoredPosition = Game.serv.camera.WorldToScreenPos(worldpos);
		go.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
	}

	public static Sprite GetCrewSprite(Entity person = null)
	{
		if (person == null)
		{
			return Game.ctx.hud.uisprites.portraits.Find(PORTRAIT_NONE);
		}
		return Game.ctx.hud.portraits.GetSpriteFor(person);
	}

	public static Sprite GetVehicleSprite(Entity vehicle = null)
	{
		string name = vehicle?.config.mobile.uiSprite;
		return Game.ctx.hud.uisprites.portraits.Find(name);
	}
}
