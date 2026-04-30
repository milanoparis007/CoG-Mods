using System.Collections;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonTerritory : BaseLesson
{
	public override int LessonNumber => 12;

	public override int StepsCount => 16;

	public override bool InhibitsQuestRequests => false;

	public override IEnumerator Run()
	{
		EnsurePlayerPeepHas(new Money(300));
		Entity ziggy = base.Tutorial.Context.Ziggy;
		Entity ziggyBuilding = base.Tutorial.Context.ZiggyBuilding;
		Node ziggyCorner = ziggyBuilding.components.board.GetNode();
		string[] replacements = new string[2]
		{
			"name",
			ziggy.data.person.first
		};
		Relationship relationshipFromSourceToPlayer = base.HumanPlayer.social.GetRelationshipFromSourceToPlayer(ziggy.Id);
		if (relationshipFromSourceToPlayer.GetTicketsAvailable() == 0)
		{
			relationshipFromSourceToPlayer.GrantFreebieTickets(1);
		}
		Game.ctx.selection.ClearActive();
		ShowStepBlurb(1, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		TweenCameraTo(GetPlayerSafehouseCorner(), HUDUtil.ZoomInLevel.ShowNames, resetPitch: true);
		yield return new WaitForSecondsRealtime(1f);
		ShowStepBlurb(2, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		TweenCameraTo(GetPlayerSafehouseCorner(), HUDUtil.ZoomInLevel.PullBack);
		Game.ctx.hud.cornerInfo.Show(CrewAssignment.EMPTY, GetPlayerSafehouseCorner());
		yield return new WaitForSecondsRealtime(0.5f);
		base.Tutorial.SetHighlight("Corner Info", "Respect/Scroll View/Viewport/Content");
		ShowStepBlurb(3, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Corner Info", "Businesses/Scroll View/Viewport/Content");
		ShowStepBlurb(4, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		Game.ctx.hud.cornerInfo.Hide();
		Game.ctx.selection.ClearActive();
		ShowStepBlurb(5, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		TweenCameraTo(ziggyBuilding, HUDUtil.ZoomInLevel.PullBack);
		yield return new WaitForSecondsRealtime(0.5f);
		ShowStepBlurbGendered(6, ziggy, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		do
		{
			yield return new WaitForSecondsRealtime(0.5f);
		}
		while (!Game.ctx.hud.convoDialog.IsShowing || Game.ctx.hud.convoDialog.Model.visit.npc != ziggy);
		ShowStepBlurb(7, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		while (base.HumanPlayer.outposts.GetOutpostEntriesUnsafe().Count == 0)
		{
			yield return new WaitForSecondsRealtime(0.5f);
		}
		base.Tutorial.HideFrancineDialog();
		Game.ctx.selection.ClearActive();
		while (Game.serv.ui.ContainsPopup<PhotoPopup>())
		{
			yield return null;
		}
		TweenCameraTo(ziggyCorner, HUDUtil.ZoomInLevel.ShowNames);
		ShowStepBlurb(8, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(9, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		TweenCameraTo(ziggyCorner, HUDUtil.ZoomInLevel.PullBackFurther);
		ShowStepBlurb(10, FrancineDialog.Anchor.BCenter);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurbGendered(11, ziggy, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		do
		{
			yield return new WaitForSecondsRealtime(0.5f);
		}
		while (base.HumanPlayer.outposts.GetOutpostEntriesUnsafe().FirstOrDefaultFast()?.pump?.IsPumping != true);
		Game.ctx.selection.ClearActive();
		ShowStepBlurb(12, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(13, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (base.HumanPlayer.territory.OwnedNodeCount < 3)
		{
			yield return null;
		}
		while (Game.serv.ui.ContainsPopup<PhotoPopup>())
		{
			yield return null;
		}
		ShowStepBlurb(14, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		TweenCameraTo(ziggyBuilding);
		ShowStepBlurb(15, FrancineDialog.Anchor.CRight);
		while (!GotNext())
		{
			yield return null;
		}
		ForceStartQuestWithReward(TutorialManager.QUEST_TUTORIAL_TERRITORY);
		base.Tutorial.SetHighlight("Quest Bar", "Panel/Container");
		TweenCameraTo(ziggyBuilding, HUDUtil.ZoomInLevel.PullBackFurther);
		ShowStepBlurb(16, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		yield return new WaitForSecondsRealtime(1f);
	}
}
