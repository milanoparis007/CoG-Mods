using System.Collections;
using Game.Session.Entities;
using Game.UI.Session;
using Game.UI.Session.Tutorial;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonSameNode : BaseLesson
{
	public override int LessonNumber => 5;

	public override int StepsCount => 13;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Entity building = Game.ctx.tutorial.Context.SameNodeBizToVisit;
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(building);
		string[] replacements = new string[4]
		{
			"name",
			buildingAndBusinessData.owner.data.person.FullName,
			"bizname",
			buildingAndBusinessData.biz.data.biz.bizname
		};
		TweenCameraTo(building, HUDUtil.ZoomInLevel.PullBack, resetPitch: true);
		yield return new WaitForSecondsRealtime(1f);
		HighlightBuildingPickFor(building);
		ShowStepBlurb(1, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.selection.CurrentActive != building)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(2, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Owner/Portrait");
		ShowStepBlurb(3, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Owner/Info/Relationship/Bar");
		ShowStepBlurb(4, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Owner/Info/Relationship/Tickets");
		ShowStepBlurb(5, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Scroll View List/Viewport/Content");
		ShowStepBlurb(6, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Scroll View List/Viewport/Content/View Container");
		ShowStepBlurb(7, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (GetConvoDialogHistoryCount() <= 1)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(8, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Owner/Info/Person Info Button");
		ShowStepBlurb(9, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.personInfo.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(10, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Person Info Dialog", "Panel Connections/Scroll View/Viewport/Content");
		ShowStepBlurb(11, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Person Info Dialog", "Owner/Close Button");
		ShowStepBlurb(12, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.hud.personInfo.IsShowing)
		{
			yield return null;
		}
		ShowStepBlurb(13, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Agreed);
		while (!GotNext())
		{
			yield return null;
		}
	}
}
