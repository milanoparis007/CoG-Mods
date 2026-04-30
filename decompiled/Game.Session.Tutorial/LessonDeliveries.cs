using System.Collections;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using Game.UI.Session.Deliveries;
using Game.UI.Session.Picks;
using Game.UI.Session.Tutorial;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public class LessonDeliveries : BaseLesson
{
	public override int LessonNumber => 15;

	public override int StepsCount => 15;

	public override bool InhibitsQuestRequests => false;

	public override IEnumerator Run()
	{
		yield return new WaitForSecondsRealtime(0.25f);
		Entity playerSafehouse = GetPlayerSafehouse();
		ResourceAndQty resources = new ResourceAndQty((Label)"home-brew", 40);
		AddToInventory(playerSafehouse, resources);
		while (Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		while (Game.serv.ui.TopPopupUnsafe != null)
		{
			yield return null;
		}
		yield return new WaitForSecondsRealtime(1f);
		DeliveriesDialog delHUD = Game.ctx.hud.deliveries;
		AutomationExecutor delData = Game.ctx.players.Human.automation;
		base.Tutorial.SetHighlight("Crew Dialog", "CARD for CrewJobs", null, 2);
		ShowStepBlurb(1, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (delData.GetAllSequences().ToList().Count < 1)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Crew Dialog", "CARD for CrewJobs/Info/Panel/First");
		ShowStepBlurb(2, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Panel/Info/First");
		ShowStepBlurb(3, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing || !delHUD.HasCrew())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Deliveries Add(Clone)", null, null, 50);
		ShowStepBlurb(4, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing || delHUD.GetAutomationSequence().steps.Count < 1)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Action");
		ShowStepBlurb(5, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentAction() != AutoAction.PickUp)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Item");
		ShowStepBlurb(6, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentItem() == Label.NULL || delHUD.CurrentItem() != base.Tutorial.TutorialResource.resid)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Buttons/OK");
		ShowStepBlurb(7, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing || delHUD.IsEditing())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Deliveries Add(Clone)");
		ShowStepBlurb(8, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		while (!delHUD.IsShowing || (!delHUD.IsEditing() && delHUD.GetAutomationSequence().steps.Count < 2))
		{
			yield return null;
		}
		ShowStepBlurb(9, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Action");
		while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentAction() != AutoAction.Sell)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Item");
		Entity firstbuilding = delHUD.CurrentDest();
		while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentItem() == Label.NULL || delHUD.CurrentItem() != base.Tutorial.TutorialResource.resid)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Buttons/OK Add");
		while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.GetStepIndex() == 1)
		{
			yield return null;
		}
		ShowStepBlurb(10, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
		base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Dest");
		Entity ziggyFriendBuilding = BuildingUtil.FindBuildingForBizOwner(base.Tutorial.Context.ZiggyFriend);
		if (ziggyFriendBuilding == firstbuilding)
		{
			ziggyFriendBuilding = base.Tutorial.Context.ZiggyBuilding;
		}
		BasePick orNull = Game.ctx.hud.picks.GetContainer(PickType.ResourcePick).GetOrNull(ziggyFriendBuilding);
		if (orNull != null)
		{
			base.Tutorial.AddHighlight(orNull.go.GetChild("Button"));
			TweenCameraTo(ziggyFriendBuilding, HUDUtil.ZoomInLevel.PullBackFurther);
			while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentDest() != ziggyFriendBuilding)
			{
				yield return null;
			}
			base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Buttons/OK Add");
			while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.GetStepIndex() == 2)
			{
				yield return null;
			}
			ShowStepBlurb(11, FrancineDialog.Anchor.CLeft, FrancineDialog.ButtonType.Waiting);
			base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Action");
			while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentAction() != AutoAction.DropOff)
			{
				yield return null;
			}
			base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Item");
			while (!delHUD.IsShowing || !delHUD.IsEditing() || delHUD.CurrentItem() != Label.NULL)
			{
				yield return null;
			}
			base.Tutorial.SetHighlight("Deliveries Dialog", "Edit Panel/Buttons/OK");
			while (!delHUD.IsShowing || delHUD.IsEditing())
			{
				yield return null;
			}
			base.Tutorial.SetHighlight("Deliveries Dialog", "Info/Buttons/Start");
			ShowStepBlurb(12, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
			while (!delHUD.GetAutomationSequence().IsAutoActive)
			{
				yield return null;
			}
			base.Tutorial.ClearHighlights();
			ShowStepBlurb(13, FrancineDialog.Anchor.BCenter);
			while (!GotNext())
			{
				yield return null;
			}
			ShowStepBlurb(14, FrancineDialog.Anchor.CCenter);
			while (!GotNext())
			{
				yield return null;
			}
			ShowStepBlurb(15, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Agreed);
			while (!GotNext())
			{
				yield return null;
			}
			TimerUtil.RunAfterTime(delegate
			{
				Game.ctx.tutorial.TryStartManualLesson(LessonNumber + 1);
			}, 0.25f);
		}
	}

	public override void OnSkip()
	{
		Game.ctx.tutorial.TryStartManualLesson(LessonNumber + 1);
	}
}
