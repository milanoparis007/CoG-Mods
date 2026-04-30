using System.Collections;
using Game.Core;
using Game.Services;
using Game.UI.Session.Tutorial;

namespace Game.Session.Tutorial;

public class LessonTopHUD : BaseLesson
{
	public override int LessonNumber => 3;

	public override int StepsCount => 8;

	public override bool InhibitsQuestRequests => true;

	public override IEnumerator Run()
	{
		Game.ctx.selection.ClearActive();
		base.Tutorial.SetHighlight("HUD Bar", "Info");
		ShowStepBlurb(1, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Money");
		ShowStepBlurb(2, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Cars");
		base.Tutorial.AddHighlight("HUD Bar", "Trucks");
		ShowStepBlurb(3, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Peeps");
		base.Tutorial.AddHighlight("HUD Bar", "Corners");
		ShowStepBlurb(4, FrancineDialog.Anchor.CCenter);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Corner/Maps");
		yield return null;
		base.Tutorial.AddHighlight("HUD Bar", "Corner/Resources");
		yield return null;
		base.Tutorial.AddHighlight("HUD Bar", "Corner/Reports");
		yield return null;
		ShowStepBlurb(5, FrancineDialog.Anchor.CLeft);
		while (!GotNext())
		{
			yield return null;
		}
		base.Tutorial.SetHighlight("HUD Bar", "Clock/Date");
		ShowStepBlurb(6, FrancineDialog.Anchor.CRight, FrancineDialog.ButtonType.Continue, new string[4]
		{
			"date",
			Loc.FormatDateLong(Game.ctx.clock.Now),
			"days",
			Loc.FormatNumber(Game.ctx.clock.DaysPerTurn)
		});
		while (!GotNext())
		{
			yield return null;
		}
		SimTime now = Game.ctx.clock.Now;
		base.Tutorial.SetHighlight("HUD Bar", "Clock/Next Turn Button");
		ShowStepBlurb(7, FrancineDialog.Anchor.CRight, FrancineDialog.ButtonType.Waiting);
		while (Game.ctx.clock.Now.days == now.days)
		{
			yield return null;
		}
		base.Tutorial.ClearHighlights();
		ShowStepBlurb(8, FrancineDialog.Anchor.CCenter, FrancineDialog.ButtonType.Understood, new string[2]
		{
			"date",
			Loc.FormatDateLong(Game.ctx.clock.Now)
		});
		while (!GotNext())
		{
			yield return null;
		}
		Game.ctx.hud.tickers.RemoveAll();
	}
}
