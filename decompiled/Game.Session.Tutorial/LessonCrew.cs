using System.Collections;
using System.Linq;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Setup;
using Game.UI.Session;
using Game.UI.Session.Picks;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonCrew : BaseLesson
{
	public override int LessonNumber => 14;

	public override int StepsCount => 11;

	public override bool InhibitsQuestRequests => false;

	public override IEnumerator Run()
	{
		while (Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		while (Game.serv.ui.TopPopupUnsafe != null)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1f);
		Game.ctx.selection.ClearActive();
		ForceStartQuestWithReward(TutorialManager.QUEST_TUTORIAL_CREW);
		ShowStepBlurb(1, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(2, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		TweenCameraTo(GetPlayerSafehouse(), HUDUtil.ZoomInLevel.PullBackFurther);
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
		ShowStepBlurb(6, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (base.HumanPlayer.crew.LivingCrewCount < 2)
		{
			yield return null;
		}
		base.Tutorial.HideFrancineDialog();
		Game.ctx.hud.picks.GetContainer(PickType.BuildingPick).RefreshShowingPicks();
		while (Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		while (Game.serv.ui.TopPopupUnsafe != null)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1f);
		base.Tutorial.SetHighlight("Crew Dialog", "Scroll View List/Viewport/Content");
		ShowStepBlurb(7, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		base.HumanPlayer.crew.CreateAndTrackVehicleAtSafehouse(EntityConstants.VEHICLE_TRUCK);
		TweenCameraTo(GetPlayerSafehouseCorner());
		ShowStepBlurb(8, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(9, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (true)
		{
			int num = base.HumanPlayer.crew.AllUnassignedVehicles.Count();
			if (base.HumanPlayer.crew.AllVehicles.Count() - num >= 2)
			{
				break;
			}
			yield return new WaitForSecondsRealtime(1f);
		}
		base.Tutorial.HideFrancineDialog();
		while (Game.serv.ui.TopPopupUnsafe != null)
		{
			yield return null;
		}
		ShowStepBlurb(10, FrancineDialog.Anchor.BCenter);
		while (!GotNext() && !Game.ctx.hud.deliveries.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Quest Bar", "Panel/Quest Scroll View/Viewport/Content");
		ShowStepBlurb(11, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
	}

	public override void OnSkip()
	{
		Game.ctx.tutorial.TryStartManualLesson(LessonNumber + 1);
		PlayerInfo human = Game.ctx.players.Human;
		if (Game.ctx.players.Human.crew.TotalCrewCount < 2)
		{
			Entity peep = CreatePlayers.CheatGenerateCrewForPlayer(Game.ctx.players.Human);
			Game.ctx.players.Human.crew.HireNewCrewInVehicle(human.territory.GetHeadquartersNode(), peep, null, isBoss: false);
		}
	}
}
