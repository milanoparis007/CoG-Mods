using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class CornerPick : BasePick
{
	private const string BUTTON = "Button/";

	private const string PIP_TAB = "Button/Pips";

	private const string PIP_TEXT = "Button/Pips/Text";

	private const string SIDE_CONTAINER = "Button/Extras/";

	private const string BG_IMAGE = "Button/BG Overlay/";

	private const float PICK_Y_HEIGHT = 0f;

	private NodeID _nodeId;

	private CornerPickData _pickdata;

	public override PickType Type => PickType.CornerPick;

	public override Vector3 MakeSceneVector()
	{
		return Game.serv.camera.WorldToSceneVector(GetNode().pos).SetY(0f);
	}

	public override void OnClick()
	{
		Game.ctx.selection.SetActive(base.Target.FindEntity());
	}

	public Node GetNode()
	{
		return base.Target.eid.FindEntity().data.corner.FindNode();
	}

	public override void RefreshContents()
	{
		_nodeId = GetNode().id;
		_pickdata = CornerPickData.GenerateCornerButtonData(GetNode());
		go.GetChild("Button/BG Overlay/").GetImage().color = _pickdata.color;
		MaybeShowWarnings(_pickdata);
		RefreshPips(_pickdata);
	}

	private void MaybeShowWarnings(CornerPickData data)
	{
		go.SetActive("Button/Extras/", value: false);
	}

	private void RefreshPips(CornerPickData data)
	{
		string text = BasePickUtil.GeneratePipIcons(data.showpips, data.pips);
		go.SetText("Button/Pips/Text", text);
		go.SetActive("Button/Pips", !string.IsNullOrEmpty(text));
	}

	public override string MakeMouseoverMessage()
	{
		string text = _nodeId.FindNode().GetCornerNameShort();
		if (_pickdata.owner.IsAnyPlayer)
		{
			if (_pickdata.owner.IsHumanPlayer)
			{
				text = text + "\n\n" + Loc.Get("ui.cornerinfo.control.yours");
			}
			else
			{
				string playerGroupName = _pickdata.owner.FindPlayer().social.PlayerGroupName;
				text = text + "\n\n" + Loc.Get("ui.cornerinfo.control.npc", "owner", playerGroupName);
			}
		}
		if (_pickdata.pips == null || _pickdata.pips.Count == 0)
		{
			return text;
		}
		string text2 = BasePickUtil.GeneratePipDescriptions(_pickdata.pips);
		return text + "\n\n" + text2;
	}
}
