using System.Collections.Generic;
using System.Linq;
using Game.Services;
using Game.UI.Session.Ledger;
using SomaSim.Util;

namespace Game.UI.Session.Victory;

public class VictoryDialog : HUDView<VictoryModel, VictoryDialog, VictoryController>
{
	public LandingSubview landingview;

	public VictoryCategorySubview categoryview;

	public VictoryEndSubview endingview;

	public List<VictoryDialogSubview> subviews;

	public const string CLOSE_BUTTON = "Panel/Close Button";

	public const string BUTTON_TEMPLATE = "Templates/Ledger Button";

	public const string SUBVIEW_LANDING = "Panel/Landing Subview";

	public const string SUBVIEW_VICTORY = "Panel/Victory Subview";

	public const string SUBVIEW_ENDING = "Panel/Gameover Subview";

	public const string BUTTON_PREV = "Panel/Back Button";

	public const string BUTTON_NEXT = "Panel/Forward Button";

	public const string BUTTON_THRONE = "Panel/Throne Button";

	public override UIReference UIReference => UIElements.VictoryDialog;

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Down;

	internal override void Initialize()
	{
		base.Initialize();
		_go.GetButton("Panel/Close Button").onClick.SetListener(base.Controller.Hide);
		_go.GetButton("Panel/Back Button").onClick.SetListener(base.Controller.SwitchLeft);
		_go.GetButton("Panel/Forward Button").onClick.SetListener(base.Controller.SwitchRight);
		landingview = new LandingSubview(_go, "Panel/Landing Subview", base.Controller);
		categoryview = new VictoryCategorySubview(_go, "Panel/Victory Subview", base.Controller);
		endingview = new VictoryEndSubview(_go, "Panel/Gameover Subview", base.Controller);
		subviews = TypeUtils.GetMemberInstances<VictoryDialogSubview>(this).ToList();
		foreach (VictoryDialogSubview subview in subviews)
		{
			subview.Deactivate();
		}
	}

	internal void SwitchToSubview(VictoryDialogSubview expected, bool force = false)
	{
		if (expected.IsActive && !force)
		{
			return;
		}
		foreach (VictoryDialogSubview subview in subviews)
		{
			if (subview == expected)
			{
				subview.Activate();
				subview.RefreshSubview();
			}
			else
			{
				subview.Deactivate();
			}
		}
	}

	internal void RefreshCategoryDisplay()
	{
		categoryview.RefreshSubview();
	}

	protected override void OnAfterHide()
	{
		VictoryScreenType type = base.Model.type;
		base.OnAfterHide();
		switch (type)
		{
		case VictoryScreenType.FromGameOver:
			if (!base.Model.dead)
			{
				VictoryController.RunFinishGO();
			}
			else
			{
				VictoryController.RunDeadGO();
			}
			break;
		case VictoryScreenType.FromEndOfYear:
			Game.ctx.hud.ledger.Controller.ShowReport(ReportType.NetWorth);
			break;
		}
	}
}
