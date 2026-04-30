using System.Collections;
using Game.Session.Entities;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonWASD : BaseLesson
{
	public override int LessonNumber => 1;

	public override int StepsCount => 5;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Game.ctx.selection.ClearActive();
		TweenCameraTo(GetPlayerPeepVehicle());
		yield return new WaitForSecondsRealtime(1f);
		Entity playerPeep = base.HumanPlayer.social.GetPlayerPeep();
		Entity entity = base.HumanPlayer.social.FindFrancine();
		string[] replacements = new string[4]
		{
			"fr",
			entity.data.person.FullName,
			"name",
			playerPeep.data.person.first
		};
		ShowStepBlurbGendered(1, playerPeep, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(2, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(3, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(4, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(5, FrancineDialog.Anchor.BRight);
		while (!GotNext())
		{
			yield return null;
		}
	}
}
