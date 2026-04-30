using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class SchemePick : BasePick
{
	private const string BUTTON = "Button";

	private const string STATE_ICON = "State Icon";

	private const string BG_OVERLAY = "BG Overlay";

	private const string TEXT_ICON = "Text";

	private const string MODULE_ICON = "Module Icon";

	private const float PICK_Y_HEIGHT = 1f;

	private SchemeData scheme;

	public override PickType Type => PickType.SchemePick;

	protected override bool CanShowPick()
	{
		SchemeData schemeForBuilding = Game.ctx.players.Human.schemes.GetSchemeForBuilding(base.Target.eid.FindEntity());
		if (base.CanShowPick())
		{
			return schemeForBuilding != null;
		}
		return false;
	}

	public override void SetTarget(PickTarget target)
	{
		base.SetTarget(target);
		scheme = Game.ctx.players.Human.schemes.GetSchemeForBuilding(target.eid.FindEntity());
	}

	public override void RefreshContents()
	{
		string item = BuildingPickUtil.GenerateBuildingButtonIcon(base.Target.eid.FindEntity(), forceScoped: true).icon;
		go.SetText("Text", item);
	}

	public override string MakeMouseoverMessage()
	{
		return Loc.Get("ui.pick.scheme");
	}

	public override void OnClick()
	{
		Game.serv.ui.AddPopup(new SchemePopup(scheme));
	}

	public override Vector3 MakeSceneVector()
	{
		return base.MakeSceneVector().SetY(1f);
	}
}
