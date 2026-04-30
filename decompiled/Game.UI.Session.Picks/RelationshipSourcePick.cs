using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public class RelationshipSourcePick : BasePick
{
	private const string PORTRAIT = "Button/Portrait/";

	private Entity _peep;

	private Entity _source;

	private Sprite _peepSprite;

	private const float PICK_Y_HEIGHT = 1f;

	public override PickType Type => PickType.RelSourcePick;

	public override void SetTarget(PickTarget e)
	{
		base.SetTarget(e);
		_peep = base.Target.FindEntity();
		_peepSprite = HUDUtil.GetCrewSprite(_peep);
		_source = BoardUtil.FindBoardInfoFor(_peep).boardEntity ?? base.Target.FindEntity().components.agent.FindCrewAssignment().GetTarget();
	}

	public override void RefreshContents()
	{
		go.GetImage("Button/Portrait/").sprite = _peepSprite;
	}

	public override void Reset()
	{
		_peep = (_source = null);
		_peepSprite = null;
		base.Reset();
	}

	public override void OnClick()
	{
	}

	public override string MakeMouseoverMessage()
	{
		return Loc.Get("ui.pick.relationship.mo", "name", _peep.data.person.FullName);
	}

	public override Vector3 MakeSceneVector()
	{
		return VFXManager.GetPositionForFX(_source.Id).AsVector3XZ.SetY(1f);
	}
}
