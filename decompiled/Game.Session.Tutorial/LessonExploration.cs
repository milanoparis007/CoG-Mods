using System.Collections;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.UI.Session;
using Game.UI.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonExploration : BaseLesson
{
	public override int LessonNumber => 9;

	public override int StepsCount => 10;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		string[] replacements = new string[0];
		Entity unscoped = FindUnscopedBuilding();
		if (unscoped != null)
		{
			Game.ctx.selection.ClearActive();
			TweenCameraTo(unscoped, HUDUtil.ZoomInLevel.PullBack, resetPitch: true);
			yield return new WaitForSecondsRealtime(1f);
			HighlightBuildingPickFor(unscoped);
			ShowStepBlurb(1, FrancineDialog.Anchor.BCenter);
			while (!GotNext())
			{
				yield return null;
			}
			GetPlayerPeep().components.agent.CheatAddActionsAndMoves(1, 1);
			Game.ctx.hud.tickers.RemoveAll();
			base.Tutorial.ClearHighlights();
			ShowStepBlurb(2, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
			while (!unscoped.components.building.IsScopedBy(PlayerID.HumanPlayer))
			{
				yield return null;
			}
			yield return new WaitForSecondsRealtime(1f);
			base.Tutorial.AddHighlight("Ticker Bar", "Container/Buttons", null, 0);
			Entity entity = BuildingUtil.FindBizForBuilding(unscoped);
			replacements = new string[2]
			{
				"bizname",
				entity.data.biz.bizname
			};
			ShowStepBlurb(3, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
			while (!GotNext())
			{
				yield return null;
			}
			base.Tutorial.ClearHighlights();
			Game.ctx.hud.tickers.RemoveAll();
		}
		Entity entity2 = FindUnscopedBuilding();
		if (entity2 != null)
		{
			TweenCameraTo(entity2, HUDUtil.ZoomInLevel.PullBackFurther, resetPitch: true);
			yield return new WaitForSecondsRealtime(1f);
			ShowStepBlurb(4, FrancineDialog.Anchor.BCenter);
			while (!GotNext())
			{
				yield return null;
			}
			Game.serv.camera.IncrementZoom(200f, CameraTween.VERY_SLOW_ZOOM_TWEEN);
			ShowStepBlurb(5, FrancineDialog.Anchor.BCenter);
			while (!GotNext())
			{
				yield return null;
			}
		}
		Node node = FindUnscopedCorner();
		if (node != null)
		{
			Game.ctx.selection.ClearActive();
			TweenCameraTo(node, HUDUtil.ZoomInLevel.PullBackFurther);
			yield return new WaitForSecondsRealtime(1f);
			ShowStepBlurb(6, FrancineDialog.Anchor.BCenter);
			while (!GotNext())
			{
				yield return null;
			}
			TweenCameraTo(node, HUDUtil.ZoomInLevel.PullBack);
			ShowStepBlurb(7, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
			while (!node.known.Get(PlayerID.HumanPlayer))
			{
				yield return null;
			}
			ShowStepBlurb(8, FrancineDialog.Anchor.BCenter);
			while (!GotNext())
			{
				yield return null;
			}
			base.Tutorial.HideFrancineDialog();
			yield return new WaitForSecondsRealtime(1f);
		}
		Entity ziggy = base.Tutorial.Context.Ziggy;
		replacements = replacements.Append("name", ziggy.data.person.first, "amt", Loc.Money(1200));
		Game.ctx.selection.ClearActive();
		TweenCameraTo(GetPlayerPeepVehicle(), HUDUtil.ZoomInLevel.PullBack);
		yield return new WaitForSecondsRealtime(1f);
		ShowStepBlurb(9, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.HideFrancineDialog();
		QuestUUID quuid = Game.ctx.quests.StartQuest(TutorialManager.QUEST_TUTORIAL_MONEY, EntityID.INVALID, fromRequest: false);
		while (Game.ctx.quests.IsQuestActive(quuid))
		{
			yield return new WaitForSecondsRealtime(1f);
		}
		Game.ctx.selection.ClearActive();
		ShowStepBlurb(10, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.HideFrancineDialog();
	}

	private Entity FindUnscopedBuilding()
	{
		Node playerPeepCorner = GetPlayerPeepCorner();
		Entity target = null;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(playerPeepCorner, 100, delegate(Node node)
		{
			target = target ?? FindUnscopedAt(node);
		}, null, null, (Node node) => target != null, onlyBizNodes: true);
		return target;
		static Entity FindUnscopedAt(Node node)
		{
			if (!node.known.Get(PlayerID.HumanPlayer))
			{
				return null;
			}
			using ListPool<Entity>.PooledBlockList pooledBlockList = ListPool<Entity>.Allocate();
			node.FindBuildingsToScopeOut(PlayerID.HumanPlayer, pooledBlockList);
			return pooledBlockList.FirstOrDefaultFast();
		}
	}

	private Node FindUnscopedCorner()
	{
		Node playerPeepCorner = GetPlayerPeepCorner();
		Node target = null;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(playerPeepCorner, 100, delegate(Node node)
		{
			target = ((!node.known.Get(PlayerID.HumanPlayer)) ? node : target);
		}, null, null, (Node node) => target != null, onlyBizNodes: true);
		return target;
	}
}
