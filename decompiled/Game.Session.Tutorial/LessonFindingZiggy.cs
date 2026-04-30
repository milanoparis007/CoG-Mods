using System.Collections;
using Game.Core;
using Game.Session.Entities;
using Game.UI.Session;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonFindingZiggy : BaseLesson
{
	public override int LessonNumber => 6;

	public override int StepsCount => 11;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Entity neighbor = base.Tutorial.Context.NeighborToVisit;
		Game.ctx.selection.ClearActive();
		TweenCameraTo(neighbor, HUDUtil.ZoomInLevel.PullBack, resetPitch: true, 350f);
		yield return new WaitForSecondsRealtime(1f);
		HighlightBuildingPickFor(neighbor);
		ShowStepBlurb(1, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
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
		Game.ctx.selection.ClearActive();
		TweenCameraTo(neighbor, HUDUtil.ZoomInLevel.PullBackFurther, resetPitch: true);
		yield return new WaitForSecondsRealtime(1f);
		ShowStepBlurb(4, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.selection.CurrentActive != GetPlayerPeepVehicle())
		{
			yield return null;
		}
		Node playerCorner = GetPlayerPeepCorner();
		Node neighborCorner = neighbor.components.board.GetNode();
		ShowStepBlurb(5, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (GetPlayerPeepCorner() == playerCorner)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1f);
		ShowStepBlurb(6, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (GetPlayerPeepCorner() != neighborCorner)
		{
			yield return null;
		}
		ShowStepBlurb(7, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(8, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Entity ziggy = base.Tutorial.Context.Ziggy;
		Node ziggyCorner = base.Tutorial.Context.ZiggyBuilding.components.board.GetNode();
		Entity entity = BuildingUtil.FindBizForBuilding(base.Tutorial.Context.ZiggyBuilding);
		string[] replacements = new string[4]
		{
			"name",
			ziggy.data.person.FullName,
			"bizname",
			entity.data.biz.bizname
		};
		ShowStepBlurbGendered(9, ziggy, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurbGendered(10, ziggy, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(11, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		while (true)
		{
			bool num = GetPlayerPeepCorner() == ziggyCorner;
			bool flag = Game.ctx.hud.convoDialog.IsShowing && Game.ctx.hud.convoDialog.Model.visit.npc == ziggy;
			if (num && flag)
			{
				break;
			}
			yield return null;
		}
		base.Tutorial.HideFrancineDialog();
	}
}
