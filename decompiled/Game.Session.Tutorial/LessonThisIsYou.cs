using System.Collections;
using Game.UI.Session;
using Game.UI.Session.Picks;
using Game.UI.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonThisIsYou : BaseLesson
{
	public override int LessonNumber => 2;

	public override int StepsCount => 10;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Game.ctx.selection.ClearActive();
		TweenCameraTo(GetPlayerPeepVehicle());
		yield return new WaitForSecondsRealtime(1f);
		BasePick orNull = Game.ctx.hud.picks.GetContainer(PickType.CrewPick).GetOrNull(GetPlayerPeepVehicle());
		if (orNull != null)
		{
			GameObject child = orNull.go.GetChild("Button");
			base.Tutorial.SetHighlight(child);
			ShowStepBlurb(1, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
			while (!IsPlayerVehSelected())
			{
				yield return null;
			}
			base.Tutorial.ClearHighlights();
			TweenCameraTo(GetPlayerPeepVehicle(), HUDUtil.ZoomInLevel.PullBack);
			ShowStepBlurb(2, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Toggle");
			ShowStepBlurb(3, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Info/Panel/First");
			ShowStepBlurb(4, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Info/Panel/Second");
			ShowStepBlurb(5, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Info/Panel/Rows/Bottom");
			ShowStepBlurb(6, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Info/Panel/Rows/Bottom");
			ShowStepBlurb(7, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Extras/Contents");
			ShowStepBlurb(8, FrancineDialog.Anchor.BRight);
			while (!GotNext())
			{
				yield return null;
			}
			EnsurePlayerSelected();
			base.Tutorial.SetHighlight("CARD for CrewMuscle", "Extras/Person");
			ShowStepBlurb(9, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Understood);
			while (!GotNext())
			{
				yield return null;
			}
			Game.ctx.selection.ClearActive();
			ShowStepBlurb(10, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
			while (Game.ctx.selection.CurrentActive != GetPlayerPeepVehicle())
			{
				yield return null;
			}
			yield return new WaitForSecondsRealtime(0.5f);
			base.Tutorial.HideFrancineDialog();
			yield return new WaitForSecondsRealtime(0.5f);
		}
		void EnsurePlayerSelected()
		{
			if (Game.ctx.selection.CurrentActive != GetPlayerPeepVehicle())
			{
				Game.ctx.selection.SetActive(GetPlayerPeepVehicle());
			}
		}
	}
}
