using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Overlays;
using SomaSim.Util;

namespace Game.UI.Session.HUD;

public static class OverlaysBarItems
{
	public const string ID_MAIN_SHELF = "main";

	public const string ID_SUBSHELF_RCI = "rci";

	public const string ID_SUBSHELF_ETH = "eth";

	public static readonly Dictionary<string, List<HUDDialogItemDefBase>> BUTTONS = new Dictionary<string, List<HUDDialogItemDefBase>>
	{
		{
			"main",
			new List<HUDDialogItemDefBase>
			{
				OverlaysButtonDef.MakeShelfButton("main", "OB Resources", "", Loc.Get("ui.overlays.recent"), Loc.Get("ui.overlays.recent.mo"), delegate
				{
					Game.ctx.overlays.ShowOverlayForRecentResources();
				}, delegate
				{
					Game.ctx.overlays.HideOverlayForResources();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Controlled", "", Loc.Get("ui.overlays.owned"), Loc.Get("ui.overlays.owned.mo"), delegate
				{
					Game.ctx.overlays.ShowAllControlledOverlay();
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Fronts", "", Loc.Get("ui.overlays.fronts"), Loc.Get("ui.overlays.fronts.mo"), delegate
				{
					Game.ctx.overlays.arrows.DrawFrontArrows();
				}, delegate
				{
					Game.ctx.overlays.arrows.HideFrontArrows();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Orders", "", Loc.Get("ui.overlays.deliveries"), Loc.Get("ui.overlays.deliveries.mo"), delegate
				{
					Game.ctx.overlays.arrows.ShowOrderArrows();
				}, delegate
				{
					Game.ctx.overlays.arrows.HideOrderArrows();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Cars", "", Loc.Get("ui.overlays.vehicles"), Loc.Get("ui.overlays.vehicles.mo"), delegate
				{
					Game.ctx.overlays.ShowCarShopsOverlay();
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Politics", "", Loc.Get("ui.overlays.wards"), Loc.Get("ui.overlays.wards.mo"), delegate
				{
					Game.ctx.overlays.ShowWardsOverlay();
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.hasShadowGovVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Precincts", "", Loc.Get("ui.overlays.precincts"), Loc.Get("ui.overlays.precincts.mo"), delegate
				{
					Game.ctx.overlays.ShowPrecinctsOverlay();
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Heat", "", Loc.Get("ui.overlays.heat"), Loc.Get("ui.overlays.heat.mo"), delegate
				{
					Game.ctx.overlays.ShowHeatOverlay();
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB Respect", "", Loc.Get("ui.overlays.respect"), Loc.Get("ui.overlays.respect.mo"), delegate
				{
					Game.ctx.overlays.ShowRespectOverlay();
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("main", "OB RCI", "", Loc.Get("ui.overlays.zoning"), Loc.Get("ui.overlays.zoning.mo"), "rci"),
				OverlaysButtonDef.MakeShelfButton("main", "OB Eth", "", Loc.Get("ui.overlays.population"), Loc.Get("ui.overlays.population.mo"), "eth")
			}
		},
		{
			"rci",
			new List<HUDDialogItemDefBase>
			{
				OverlaysButtonDef.MakeShelfButton("rci", "OB R", "", Loc.Get("ui.overlays.zoning.residential"), Loc.Get("ui.overlays.zoning.residential.mo"), delegate
				{
					OverlayManager.ShowOverlayForZone(ZoneType.Res, ColorConstants.OVERLAY_RES);
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("rci", "OB C", "", Loc.Get("ui.overlays.zoning.commercial"), Loc.Get("ui.overlays.zoning.commercial.mo"), delegate
				{
					OverlayManager.ShowOverlayForZone(ZoneType.Com, ColorConstants.OVERLAY_COM);
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc),
				OverlaysButtonDef.MakeShelfButton("rci", "OB I", "", Loc.Get("ui.overlays.zoning.industrial"), Loc.Get("ui.overlays.zoning.industrial.mo"), delegate
				{
					OverlayManager.ShowOverlayForZone(ZoneType.Ind, ColorConstants.OVERLAY_IND);
				}, delegate
				{
					Game.ctx.overlays.HideAnyOverlay();
				}, HUDDialogItemDefBase.defaultVisFunc)
			}
		},
		{
			"eth",
			new List<HUDDialogItemDefBase>()
		}
	};

	public static List<HUDDialogItemDefBase> GetDefs(string id)
	{
		return BUTTONS.FindOrNull(id);
	}

	internal static void InitializeEth()
	{
		EthnicitySettings settings = Game.serv.globals.settings.ethnicities;
		IOrderedEnumerable<EthnicityDef> source = from eth in Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted()
			select settings.FindEthnicityDef(eth) into def
			where !def.hidden
			orderby def.loc.GetEthnicity()
			select def;
		GetDefs("eth").ClearAndAddRange(source.Select(MakeEthButton));
	}

	private static OverlaysButtonDef MakeEthButton(EthnicityDef eth)
	{
		string ethnicity = eth.loc.GetEthnicity();
		return OverlaysButtonDef.MakeShelfButton("eth", "", Loc.Get(eth.loc.icon), ethnicity, Loc.Get("ui.overlays.population.button", "eth", ethnicity), delegate
		{
			OverlayManager.ShowOverlayForEthnicity(eth.id, ColorConstants.OVERLAY_ETH);
		}, delegate
		{
			Game.ctx.overlays.HideAnyOverlay();
		}, HUDDialogItemDefBase.defaultVisFunc);
	}
}
