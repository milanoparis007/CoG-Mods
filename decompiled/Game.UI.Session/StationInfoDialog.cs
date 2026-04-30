using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session;

public class StationInfoDialog : EntityInfoDialog
{
	private const string CARD_TEMPLATE = "Templates/Officer Info Card";

	private const string CLOSE_BUTTON = "Panel/Close";

	private const string STATION_NAME = "Panel/Station Name";

	private const string LOC_LINE = "Panel/Loc";

	private const string STATUS_LINE = "Panel/Status";

	private const string OFFICERS_LINE = "Panel/Officers";

	private const string CONTENT = "Scroll View List/Viewport/Content";

	private GameObject _cardTemplate;

	private const string CARD_PORTRAIT = "Portrait/Portrait";

	private const string CARD_TEXT = "Text";

	private const string INFO_BTN = "Person Info";

	private const string GOTO_BTN = "Goto";

	public override UIReference UIReference => UIElements.StationInfoDialog;

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override void Show(Entity entity, CrewAssignment crewNearby)
	{
		base.Show(entity, crewNearby);
	}

	internal override void Initialize()
	{
		base.Initialize();
		_cardTemplate = _go.GetChild("Templates/Officer Info Card");
		_go.GetButton("Panel/Close").onClick.SetListener(delegate
		{
			Game.ctx.selection.ClearActive();
		});
	}

	internal override void Release()
	{
		_cardTemplate = null;
		base.Release();
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		InitializeHeader();
		InitializeCards();
	}

	private void InitializeHeader()
	{
		PlayerInfo playerInfo = base.Entity.data.police.copPlayerId.FindPlayer();
		string text = playerInfo.social.FindPlayerGroupNameColorized();
		string precinctName = playerInfo.ai.precinct.GetPrecinctName();
		_go.SetText("Panel/Station Name", Loc.Get("ui.stationinfo.header", "groupname", text, "precinct", precinctName));
		Node node = base.Entity.components.board.GetNode();
		_go.SetText("Panel/Loc", Loc.Get("ui.stationinfo.loc", "cornerinfo", node.GetCornerNameShort()));
		bool flag = playerInfo.ai.precinct.HasDonationFrom(PlayerID.HumanPlayer) == DonationState.PaidOff;
		string key = (flag ? "ui.stationinfo.status.coop" : "ui.stationinfo.status.noop");
		_go.SetText("Panel/Status", Loc.IconLine(flag, Loc.Get(key)));
		_go.SetText("Panel/Officers", Loc.Get("ui.stationinfo.officers"));
	}

	private void InitializeCards()
	{
		List<EntityID> officers = base.Entity.data.police.officers;
		GameObject child = _go.GetChild("Scroll View List/Viewport/Content");
		child.EnsureChildCount(officers, _cardTemplate);
		child.InitializeChildren(officers, InitializeOfficerCard);
	}

	private void InitializeOfficerCard(int _, GameObject card, EntityID officerId)
	{
		PlayerInfo playerInfo = base.Entity.data.police.copPlayerId.FindPlayer();
		PrecinctAdvisor precinct = playerInfo.ai.precinct;
		bool isInVehicle = playerInfo.crew.GetCrewForPeep(officerId).IsInVehicle;
		bool influenced = false;
		InitializeCard(card, officerId, influenced, isInVehicle);
	}

	private static void InitializeCard(GameObject card, EntityID peepId, bool influenced, bool deployed)
	{
		Entity peep = peepId.FindEntity();
		string text = (influenced ? Loc.Get("ui.stationinfo.officers.card.influenced") : "");
		string text2 = Loc.Get("ui.stationinfo.officers.card.name", "name", peep.data.person.FullName, "influenced", text);
		card.SetText("Text", text2);
		card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(peep));
		Button button = card.GetButton("Goto");
		button.gameObject.SetActive(deployed);
		if (deployed)
		{
			Entity vehicle = peep.components.agent.FindCrewAssignment().GetVehicle();
			button.onClick.SetListener(delegate
			{
				HUDUtil.GoTo(vehicle.data.mobile.worldpos);
			});
		}
		card.GetButton("Person Info").onClick.SetListener(delegate
		{
			PersonInfoDialog personInfo = Game.ctx.hud.personInfo;
			if (personInfo.IsShowing)
			{
				personInfo.Controller.SwitchToPerson(peep);
			}
			else
			{
				personInfo.Show(peep);
			}
		});
	}
}
