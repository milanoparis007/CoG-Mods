using System.Collections;
using Game.Session.Entities;
using Game.UI.Session;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonSafehouse : BaseLesson
{
	public override int LessonNumber => 4;

	public override int StepsCount => 19;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Entity francine = base.HumanPlayer.social.FindFrancine();
		string[] replacements = new string[2]
		{
			"fr",
			francine.data.person.FirstName
		};
		TweenCameraTo(GetPlayerSafehouse(), HUDUtil.ZoomInLevel.PullBack, resetPitch: true, 275f);
		yield return new WaitForSecondsRealtime(1f);
		HighlightBuildingPickFor(GetPlayerSafehouse());
		ShowStepBlurb(1, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!IsPlayerSafehouseSelected())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurbGendered(2, francine, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Owned Biz", "Background/Modules/Buttons", null, 1);
		ShowStepBlurb(3, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!IsShowing(ViewType.ViewDescribeModule))
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(4, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Owned Biz", "Background/Modules/Buttons", null, 2);
		ShowStepBlurb(5, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!IsShowing(ViewType.ViewInventory))
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(6, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Owned Biz", "Background/Modules/Buttons", null, 3);
		ShowStepBlurb(7, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!IsShowing(ViewType.ViewAddModule))
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(8, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Owned Biz", "Background/Modules/Buttons", null, 4);
		ShowStepBlurb(9, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.cornerInfo.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(10, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Corner Info", "Businesses/Scroll View/Viewport/Content");
		ShowStepBlurb(11, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Corner Info", "Respect/Scroll View/Viewport/Content");
		ShowStepBlurb(12, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Corner Info", "Heat");
		ShowStepBlurb(13, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Corner Info", "Cops");
		ShowStepBlurb(14, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Corner Info", "Close");
		ShowStepBlurb(15, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.hud.cornerInfo.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		base.Tutorial.HideFrancineDialog();
		Game.ctx.selection.SetActive(GetPlayerSafehouse());
		yield return new WaitForSecondsRealtime(1f);
		base.Tutorial.SetHighlight("Owned Biz", "Background/Modules/Buttons", null, 0);
		ShowStepBlurb(16, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		ShowStepBlurbGendered(17, francine, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Owner/Close Button");
		ShowStepBlurb(18, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(19, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Agreed);
		while (!GotNext())
		{
			yield return null;
		}
		static bool IsShowing(ViewType type)
		{
			return Game.ctx.hud.ownedBiz.IsShowingSubview(type);
		}
	}
}
