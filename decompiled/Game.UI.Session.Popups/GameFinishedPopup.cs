using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public class GameFinishedPopup : GameOverPopup
{
	public override UIReference UIReference => UIElements.GameOverPopup;

	protected override void InitializeOnPush()
	{
		base.InitializeOnPush();
		_go.SetButtonListener("Panel/Footer/Continue", base.Continue);
		_go.SetChildText("Panel/Footer/Continue", Loc.Get("button.endgame-continue"));
	}

	protected override void RefreshContents()
	{
		Entity playerPeep = Game.ctx.players.Human.social.GetPlayerPeep();
		Sprite crewSprite = HUDUtil.GetCrewSprite(playerPeep);
		_go.SetImageOrHide("Panel/Portrait/Portrait", crewSprite);
		Game.ctx.simman.victory.ExplainGameOverPoints();
		Game.ctx.simman.victory.ExplainGameOverRanking();
		string[] array = new string[4]
		{
			"name",
			playerPeep.data.person.ShortName,
			"groupname",
			Game.ctx.players.Human.social.PlayerGroupName
		};
		GameObject go = _go;
		object[] replacements = array;
		go.SetText("Panel/Name", Loc.Get("victory.gameover.title", replacements));
		_go.SetText("Panel/Text", Loc.Get("victory.gameover.continue"));
	}

	protected override void ReleaseOnPop()
	{
		base.ReleaseOnPop();
		_go.ClearButtonListeners("Panel/Footer/Continue");
	}
}
