using System.Text;
using Game.Services;
using Game.Session;
using Game.Session.Assets;
using Game.Session.Entities;
using Game.Session.Overlays;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public class RelationshipPick : BasePick
{
	private const string BUTTON = "Button/";

	private const string PORTRAIT = "Button/Portrait/";

	private const string BG_IMAGE = "Button/BG Image/";

	private const string HIGHLIGHT = "Button/BG Border/";

	private const string RELATIONSHIP = "Relationship/";

	private const string TICKET_PIP = "Ticket Pip/";

	private const string TICKET_TEXT = "Ticket Pip/Text/";

	private Entity _peep;

	private Entity _source;

	private VFXManager.WorldArrowHandle _handle;

	private Sprite _peepSprite;

	private int _ticketsAvailable;

	private Fixnum _relationshipLevel;

	private const float PICK_Y_HEIGHT = 1f;

	public override PickType Type => PickType.RelArrowPick;

	public override void SetTarget(PickTarget target)
	{
		base.SetTarget(target);
		_peep = base.Target.FindEntity();
		_peepSprite = HUDUtil.GetCrewSprite(_peep);
		VFXManager.WorldArrowHandle? worldArrowHandle = Game.ctx.overlays.arrows.FindHandleForContext(_peep.Id);
		Color color = BuildingPickUtil.GenerateNPCColor(_peep);
		go.GetImage("Button/BG Image/").color = color;
		if (worldArrowHandle.HasValue)
		{
			_handle = worldArrowHandle.Value;
			_source = BuildingUtil.FindOwnerForAnyBuilding(_handle.from) ?? _handle.from.FindEntity();
		}
	}

	public override void RefreshContents()
	{
		go.GetImage("Button/Portrait/").sprite = _peepSprite;
		SetHighlight(state: false);
		_ticketsAvailable = TicketsAvailable();
		bool flag = ValidTickets();
		go.SetActive("Ticket Pip/", flag);
		if (flag)
		{
			go.SetText("Ticket Pip/Text/", Loc.Get("ui.ticket"));
		}
		_relationshipLevel = RelationshipLevel();
		go.SetActive("Relationship/", _relationshipLevel.IsNotZero && !_peep.data.agent.IsInHumanCrew);
		if (_relationshipLevel.IsNotZero)
		{
			PersonInfoUtil.UpdateRelationshipBar(go.GetChild("Relationship/"), _peep);
		}
	}

	private bool ValidTickets()
	{
		bool num = _ticketsAvailable > 0;
		bool flag = !_peep.data.agent.IsInHumanCrew && !_peep.components.agent.GetPlayer().IsJustGang;
		bool flag2 = !_peep.data.person.HasResAssigned;
		return num && flag && flag2;
	}

	public override void OnClick()
	{
		if (Game.ctx.overlays.CurrentRelMode == OverlayManager.RelMode.Default)
		{
			Game.ctx.hud.personInfo.Controller.SwitchToPerson(_peep);
		}
		Game.ctx.events.EnqueueOnce(SessionEventType.UIRelSelectMade, Game.ctx.players.Human.PID, _peep.Id);
	}

	public override string MakeMouseoverMessage()
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		string fullName = _source.data.person.FullName;
		string fullName2 = _peep.data.person.FullName;
		stringBuilder.AppendLine(Loc.Get("ui.pick.relationship.connection", "fromname", fullName, "toname", fullName2));
		if (_relationshipLevel.IsNotZero)
		{
			stringBuilder.AppendLine(Loc.Get("ui.pick.relationship.level", "num", _relationshipLevel));
		}
		if (ValidTickets())
		{
			stringBuilder.AppendLine(Loc.GetPluralized("ui.pick.tickets", _ticketsAvailable, "num", _ticketsAvailable));
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	public override void Reset()
	{
		_peep = null;
		_source = null;
		_peepSprite = null;
		base.Reset();
	}

	public override Vector3 MakeSceneVector()
	{
		return _handle.MakeArrowPoints().to.AsVector3XZ.SetY(1f);
	}

	public void SetHighlight(bool state)
	{
		go.GetChild("Button/BG Border/").SetActive(state);
	}

	private int TicketsAvailable()
	{
		if (_peep.Id == Game.ctx.players.Human.social.PlayerPeepId)
		{
			return 0;
		}
		return Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(_peep.Id)?.GetTicketsAvailable() ?? 0;
	}

	private Fixnum RelationshipLevel()
	{
		if (_peep.Id == Game.ctx.players.Human.social.PlayerPeepId)
		{
			return Fixnum.ZERO;
		}
		return Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(_peep.Id)?.Evaluate().current ?? Fixnum.ZERO;
	}
}
