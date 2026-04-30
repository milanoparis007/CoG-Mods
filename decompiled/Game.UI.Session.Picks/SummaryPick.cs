using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public sealed class SummaryPick : BasePick
{
	private const string BUTTON = "Button";

	private const string BUTTON_BG = "Button BG";

	private const string PIPS_TAB = "Button/Pips";

	private const float PICK_Y_HEIGHT = 0f;

	private NodeID _nodeId;

	private SummaryPickData _pickdata;

	private const string PIP_BG = "BG Image";

	private const string PIP_TEXT = "Text";

	private const string PIP_TICKETS = "Actions";

	private const string PIP_UNSCOPED = "Unscoped";

	public override PickType Type => PickType.SummaryPick;

	public bool HasBuildings
	{
		get
		{
			List<SummaryPickData.Target> targets = _pickdata.targets;
			if (targets == null)
			{
				return false;
			}
			return targets.Count > 0;
		}
	}

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

	protected override bool CanShowPick()
	{
		return HasBuildings;
	}

	public override void RefreshContents()
	{
		_nodeId = GetNode().id;
		_pickdata = SummaryPickData.GenerateCornerButtonData(GetNode());
		RefreshPips(go.GetChild("Button/Pips"), _pickdata.targets);
	}

	private void RefreshPips(GameObject gameObject, List<SummaryPickData.Target> targets)
	{
		Transform transform = gameObject.transform;
		int num = 0;
		int i = 0;
		for (int childCount = transform.childCount; i < childCount; i++)
		{
			GameObject gameObject2 = transform.GetChild(i).gameObject;
			if (gameObject2.activeSelf)
			{
				num++;
			}
			if (i < targets.Count)
			{
				gameObject2.SetActive(value: true);
				RefreshPip(gameObject2, targets[i]);
			}
			else
			{
				gameObject2.SetActive(value: false);
			}
		}
		if (num != targets.Count)
		{
			gameObject.ForceRebuildLayoutImmediate();
		}
	}

	private static void RefreshPip(GameObject pip, SummaryPickData.Target target)
	{
		pip.GetImage("BG Image").color = target.color;
		GameObject child = pip.GetChild("Text");
		child.SetActive(target.scoped);
		if (target.scoped)
		{
			child.GetText().SetText(target.icon);
		}
		pip.GetChild("Unscoped").SetActive(!target.scoped);
		GameObject child2 = pip.GetChild("Actions");
		bool flag = target.FindBuildingPickOrNull()?.HasTickets ?? false;
		child2.SetActive(flag);
	}

	public override string MakeMouseoverMessage()
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		string cornerNameShort = _nodeId.FindNode().GetCornerNameShort();
		stringBuilder.AppendLine(cornerNameShort, 1);
		if (_pickdata.owner.IsAnyPlayer)
		{
			if (_pickdata.owner.IsHumanPlayer)
			{
				stringBuilder.AppendLine(Loc.Get("ui.pick.corner.you"), 1);
			}
			else
			{
				string text = _pickdata.owner.FindPlayer().social.FindPlayerGroupNameColorized();
				stringBuilder.AppendLine(Loc.Get("ui.pick.corner.else", "owner", text), 1);
			}
		}
		stringBuilder.AppendLine();
		int num = 0;
		foreach (SummaryPickData.Target target in _pickdata.targets)
		{
			string text2;
			if (target.building.components.building.IsScopedBy(PlayerID.HumanPlayer))
			{
				text2 = BuildingUtil.GenerateBuildingPickMouseover(target.building, brief: true);
			}
			else
			{
				text2 = Loc.Get("ui.pick.corner.build.unknown");
				num++;
			}
			stringBuilder.AppendLine(Loc.Get("ui.pick.corner.build.icon", "icon", target.icon, "text", text2));
		}
		if (num > 0)
		{
			stringBuilder.AppendLine(Loc.Get("ui.pick.corner.unscoped", "unscoped", num));
		}
		return stringBuilder.ToStringAndReturnToPool();
	}
}
