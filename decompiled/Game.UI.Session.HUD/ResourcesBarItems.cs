using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.UI.Session.HUD;

public static class ResourcesBarItems
{
	public const string ID_MAIN_SHELF_RES = "main";

	public static readonly List<HUDDialogItemDefBase> BUTTONS = new List<HUDDialogItemDefBase>();

	internal static void InitializeResourcesButtonDefs()
	{
		_ = Game.serv.globals.settings.resources;
		List<Resource> list = (from resId in Game.ctx.players.Human.skills.GetUnlockedResourcesUnsafe()
			select Resource.Find(resId)).WhereNotNull().ToList();
		list.Sort((Resource a, Resource b) => string.Compare(a.GetName(), b.GetName(), ignoreCase: true));
		IOrderedEnumerable<IGrouping<ResourceCategory, Resource>> orderedEnumerable = from resource in list
			group resource by resource.GetCategory() into @group
			orderby @group.Key?.sortorder ?? 0
			select @group;
		List<HUDDialogItemDefBase> bUTTONS = BUTTONS;
		bUTTONS.Clear();
		foreach (IGrouping<ResourceCategory, Resource> item in orderedEnumerable)
		{
			ResourceCategory key = item.Key;
			if (key == null)
			{
				continue;
			}
			OverlaysButtonSetDef overlaysButtonSetDef = OverlaysButtonSetDef.MakeButtonSet("main", key.GetIcon(), key.GetName(), key.GetName());
			bUTTONS.Add(overlaysButtonSetDef);
			foreach (Resource item2 in item)
			{
				overlaysButtonSetDef.buttons.Add(MakeResButton(item2));
			}
		}
	}

	[Conditional("UNITY_EDITOR")]
	private static void LogMissingResources()
	{
		_ = Game.serv.globals.settings.resources;
		foreach (Label item in Game.ctx.players.Human.skills.GetUnlockedResourcesUnsafe())
		{
			Game.ctx.simman.FindResource(item);
		}
	}

	private static OverlaysButtonDef MakeResButton(Resource res)
	{
		return OverlaysButtonDef.MakeShelfButton("main", "", res.GetIcon(), res.GetName(), Explain(res), delegate
		{
			Game.ctx.overlays.ToggleResourceInOverlay(new List<Resource> { res });
		}, delegate
		{
			Game.ctx.overlays.RefreshResources();
		}, res.resid);
	}

	private static string Explain(Resource res)
	{
		string text = res.GetIconAndName();
		if (res.GetIsIllegal())
		{
			text += Loc.Get("ui.resource.is-illegal");
		}
		return text;
	}
}
