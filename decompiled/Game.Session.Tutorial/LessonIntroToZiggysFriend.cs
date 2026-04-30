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

public class LessonIntroToZiggysFriend : BaseLesson
{
	public override int LessonNumber => 8;

	public override int StepsCount => 10;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Entity ziggy = base.Tutorial.Context.Ziggy;
		Node ziggyCorner = base.Tutorial.Context.ZiggyBuilding.components.board.GetNode();
		string[] replacements = new string[2]
		{
			"name",
			ziggy.data.person.first
		};
		ShowStepBlurbGendered(1, ziggy, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting, replacements);
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
		base.Tutorial.SetHighlight("Conversation", "Owner/Info/Relationship/Bar");
		ShowStepBlurb(2, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Owner/Info/Relationship/Tickets");
		ShowStepBlurb(3, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.convoDialog.TrimHistoryForTutorial();
		ShowStepBlurb(4, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting, replacements);
		while (GetConvoDialogHistoryCount() <= 1)
		{
			yield return null;
		}
		ShowStepBlurb(5, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting, replacements);
		while (GetConvoDialogHistoryCount() <= 3)
		{
			yield return null;
		}
		ShowStepBlurb(6, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		RelationshipList playerRels = Game.ctx.simman.rels.GetListOrNull(base.HumanPlayer.social.PlayerPeepId);
		int playerRelCount = playerRels.data.Count;
		while (playerRels.data.Count == playerRelCount)
		{
			yield return null;
		}
		Relationship relationship = playerRels.data.LastOrDefaultFast();
		Entity friend = relationship.to.FindEntity();
		Entity friendBuilding = BuildingUtil.FindBuildingForBizOwner(friend);
		replacements = replacements.Append("friendname", friend.data.person.FullName);
		TweenCameraTo(friendBuilding, HUDUtil.ZoomInLevel.PullBack, resetPitch: true);
		yield return new WaitForSecondsRealtime(1f);
		Game.ctx.selection.ClearActive();
		HighlightBuildingPickFor(friendBuilding);
		ShowStepBlurbGendered(7, friend, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(8, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		Fixnum lastTurnBeerInCar = GetBeerQtyInCar();
		Fixnum lastTurnBeerFriend = GetBeerQtyIn(friendBuilding);
		ShowStepBlurbGendered(9, friend, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(10, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (true)
		{
			Fixnum beerQtyInCar = GetBeerQtyInCar();
			Fixnum beerQtyIn = GetBeerQtyIn(friendBuilding);
			if (beerQtyInCar != lastTurnBeerInCar && beerQtyIn != lastTurnBeerFriend)
			{
				break;
			}
			lastTurnBeerFriend = beerQtyIn;
			lastTurnBeerInCar = beerQtyInCar;
			yield return new WaitForSecondsRealtime(0.5f);
		}
		base.Tutorial.HideFrancineDialog();
	}

	public override void OnSkip()
	{
		Entity ziggy = base.Tutorial.Context.Ziggy;
		Entity ziggyFriend = base.Tutorial.Context.ZiggyFriend;
		EntityID playerPeepId = Game.ctx.players.Human.social.PlayerPeepId;
		Game.ctx.players.Human.territory.ScopeOutAndMeetOwner(BuildingUtil.FindBuildingForBizOwner(ziggyFriend), procgen: false, playerPeepId, setControlled: false);
		Relationship relationshipFromSourceToPlayer = Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(ziggyFriend.Id);
		Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(ziggy.Id).convos++;
		relationshipFromSourceToPlayer.convos++;
		relationshipFromSourceToPlayer.AddBuff(BuffConstants.TICKET_INTRO, playerPeepId);
	}
}
