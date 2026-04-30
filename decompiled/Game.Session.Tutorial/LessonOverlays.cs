using System.Collections;
using Game.Core;
using Game.UI.Session.Popups;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonOverlays : BaseLesson
{
	public override int LessonNumber => 11;

	public override int StepsCount => 17;

	public override bool InhibitsQuestRequests => false;

	public override IEnumerator Run()
	{
		ShowStepBlurb(1, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Corner/Resources");
		ShowStepBlurb(2, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.resourcesBar.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(3, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.AddHighlight("HUD Bar", "Corner/Resources");
		base.Tutorial.AddHighlight("Resources Bar", "Shelf/Favorites Bar/Add Faves");
		base.Tutorial.AddHighlight("Resources Bar", "Shelf/Favorites Bar/Toggle Faves");
		ShowStepBlurb(4, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.hud.resourcesBar.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Corner/Maps");
		ShowStepBlurb(5, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.overlaysBar.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(6, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.overlaysBar.Show();
		ShowStepBlurb(7, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.overlaysBar.Show();
		ShowStepBlurb(8, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.overlaysBar.Show();
		ShowStepBlurb(9, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.overlaysBar.Show();
		ShowStepBlurb(10, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.overlaysBar.Show();
		ShowStepBlurb(11, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.overlaysBar.Hide();
		yield return new WaitForSecondsRealtime(0.5f);
		base.Tutorial.SetHighlight("HUD Bar", "Corner/Reports");
		ShowStepBlurb(12, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.reportsBar.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(13, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.reportsBar.Hide();
		yield return new WaitForSecondsRealtime(0.5f);
		base.Tutorial.SetHighlight("HUD Bar", "Corner/Encyclopedia");
		ShowStepBlurb(14, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Waiting);
		while (!Game.serv.ui.ContainsPopup<EncyclopediaPopup>())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(15, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.serv.ui.GetActivePopup<EncyclopediaPopup>()?.Close();
		ShowStepBlurb(16, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(17, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.HideFrancineDialog();
		do
		{
			yield return new WaitForSecondsRealtime(1f);
		}
		while (Game.ctx.quests.IsQuestActiveByID(TutorialManager.QUEST_TUTORIAL_CROCKS, EntityID.INVALID));
	}
}
