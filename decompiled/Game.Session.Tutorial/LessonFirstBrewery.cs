using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.UI.Session;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonFirstBrewery : BaseLesson
{
	public override int LessonNumber => 10;

	public override int StepsCount => 14;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Entity safehouse = GetPlayerSafehouse();
		List<ResourceAndQty> resources = new List<ResourceAndQty>
		{
			new ResourceAndQty((Label)"lumber", 4),
			new ResourceAndQty((Label)"malt", 10),
			new ResourceAndQty((Label)"crocks", 30)
		};
		EnsurePlayerPeepHas(new Money(1200));
		EnsureInventoryHas(safehouse, resources);
		Game.ctx.selection.ClearActive();
		TweenCameraTo(safehouse, HUDUtil.ZoomInLevel.PullBack);
		yield return new WaitForSecondsRealtime(1f);
		ShowStepBlurb(1, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Node safehouseCorner = base.HumanPlayer.territory.GetHeadquartersNode();
		ShowStepBlurb(2, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (GetPlayerPeepCorner() != safehouseCorner)
		{
			yield return null;
		}
		ShowStepBlurb(3, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!IsShowing(ViewType.ViewInventory))
		{
			yield return null;
		}
		string[] replacements = new string[2]
		{
			"amt",
			Loc.Price(new Price(900))
		};
		ShowStepBlurb(4, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		while (base.HumanPlayer.finances.GetMoney(safehouse).cash < 900)
		{
			yield return null;
		}
		ShowStepBlurb(5, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting, replacements);
		while (!IsShowing(ViewType.ViewAddModule))
		{
			yield return null;
		}
		base.Tutorial.AddHighlight("Owned Biz", "View Add Module/Add Button");
		ShowStepBlurb(6, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (!Game.serv.ui.ContainsPopup<OwnedBizAddModulePopup>())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(7, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(8, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (!safehouse.components.modules.HasBackroomModules())
		{
			yield return null;
		}
		ShowStepBlurb(9, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		GameObject child = Game.serv.ui.GetSessionUICanvas().gameObject.GetChild("Owned Biz");
		GameObject describe = child.GetChild("View Describe Module");
		GameObject child2 = describe.GetChild("Detail View/Viewport/Content").transform.GetChild(0).gameObject.GetChild("Contents");
		Transform last = child2.transform.GetChild(6);
		base.Tutorial.SetHighlight(last.gameObject);
		ShowStepBlurb(10, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		base.Tutorial.AddHighlight(last.transform.GetChild(0).gameObject);
		base.Tutorial.AddHighlight(last.transform.GetChild(1).gameObject);
		ShowStepBlurb(11, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		base.Tutorial.AddHighlight(describe.GetChild("Manager"));
		base.Tutorial.AddHighlight(describe.GetChild("Upgrade"));
		base.Tutorial.AddHighlight(describe.GetChild("Expansions"));
		ShowStepBlurb(12, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		Game.ctx.hud.ownedBiz.Hide();
		ShowStepBlurbGendered(13, base.Tutorial.Context.Francine, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Agreed);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(14, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.quests.StartQuest(TutorialManager.QUEST_TUTORIAL_CROCKS, EntityID.INVALID, fromRequest: false);
		static bool IsShowing(ViewType type)
		{
			if (Game.ctx.hud.ownedBiz.IsShowing)
			{
				return Game.ctx.hud.ownedBiz.IsShowingSubview(type);
			}
			return false;
		}
	}
}
