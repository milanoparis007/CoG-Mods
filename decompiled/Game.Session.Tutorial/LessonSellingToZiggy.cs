using System.Collections;
using Game.Core;
using Game.Session.Entities;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.Tutorial;
using SomaSim.Util;

namespace Game.Session.Tutorial;

public class LessonSellingToZiggy : BaseLesson
{
	public override int LessonNumber => 7;

	public override int StepsCount => 11;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Entity ziggy = base.Tutorial.Context.Ziggy;
		string[] replacements = new string[2]
		{
			"name",
			ziggy.data.person.FirstName
		};
		ShowStepBlurbGendered(1, ziggy, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting, replacements);
		while (!Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("Conversation", "Scroll View List/Viewport/Content/View Container");
		while (GetConvoDialogHistoryCount() <= 1)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurbGendered(2, ziggy, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(3, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.hud.convoDialog.IsShowing)
		{
			yield return null;
		}
		Node safehouseCorner = base.HumanPlayer.territory.GetHeadquartersNode();
		ShowStepBlurb(4, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(5, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (GetPlayerPeepCorner() != safehouseCorner)
		{
			yield return null;
		}
		ShowStepBlurb(6, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurb(7, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting);
		while (!Game.ctx.hud.ownedBiz.IsShowing || !Game.ctx.hud.ownedBiz.IsShowingSubview(ViewType.ViewInventory))
		{
			yield return null;
		}
		ShowStepBlurb(8, FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType.Waiting);
		while (GetBeerQtyInCar() < 20)
		{
			yield return null;
		}
		ShowStepBlurb(9, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.hud.ownedBiz.IsShowing)
		{
			yield return null;
		}
		Fixnum broughtToZiggy = GetBeerQtyInCar();
		ShowStepBlurbGendered(10, ziggy, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Continue, replacements);
		while (!GotNext())
		{
			yield return null;
		}
		ShowStepBlurbGendered(11, ziggy, FrancineDialog.Anchor.BRight, FrancineDialog.ButtonType.Waiting, replacements);
		while (GetBeerQtyInCar() == broughtToZiggy)
		{
			yield return null;
		}
		base.Tutorial.HideFrancineDialog();
	}
}
