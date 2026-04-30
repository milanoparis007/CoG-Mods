using System.Collections;
using Game.Services.Filesystem;
using Game.UI.Session.Picks;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonMovingOn : BaseLesson
{
	public override int LessonNumber => 16;

	public override int StepsCount => 7;

	public override bool InhibitsQuestRequests => false;

	public override IEnumerator Run()
	{
		yield return new WaitForSecondsRealtime(0.5f);
		while (Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		while (Game.serv.ui.TopPopupUnsafe != null)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1f);
		ShowStepBlurbGendered(1, GetPlayerPeepCrew().GetPeep(), FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.picks.GetContainer(PickType.BuildingPick).RefreshShowingPicks();
		ForceStartQuestWithReward(TutorialManager.QUEST_TUTORIAL_BUILDING);
		ShowStepBlurb(2, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(3, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(4, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(5, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(6, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(7, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Agreed);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.events.SendImmediate(SessionEventType.AchieveEndTutorial);
		GamePreferences game = Game.serv.saveload.prefs.game;
		if (game.tutorial == GamePreferences.TutorialType.TutorialAndHints)
		{
			game.tutorial = GamePreferences.TutorialType.HintsOnly;
			Game.serv.saveload.SavePrefs();
		}
	}
}
