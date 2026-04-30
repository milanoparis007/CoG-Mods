using System.Collections.Generic;
using System.Linq;
using Game.Services;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Ledger;

public class LedgerDialog : HUDView<LedgerModel, LedgerDialog, LedgerController>
{
	public ReportsSubview reportsview;

	public ChartsSubview chartsview;

	public List<LedgerDialogSubview> subviews;

	private GameObject _tmplButton;

	public const string BUTTON_TEMPLATE = "Templates/Ledger Button";

	public const string BUTTON_CONTAINER = "Panel/Reports";

	public const string CLOSE_BUTTON = "Panel/Close Button";

	public const string SUBVIEW_REPORTS = "Panel/Reports Subview";

	public const string SUBVIEW_CHARTS = "Panel/Charts Subview";

	public override UIReference UIReference => UIElements.LedgerDialog;

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Down;

	internal override void Initialize()
	{
		base.Initialize();
		_tmplButton = _go.GetChild("Templates/Ledger Button");
		_go.GetButton("Panel/Close Button").onClick.SetListener(Hide);
		reportsview = new ReportsSubview(_go, "Panel/Reports Subview", base.Controller);
		chartsview = new ChartsSubview(_go, "Panel/Charts Subview", base.Controller);
		subviews = TypeUtils.GetMemberInstances<LedgerDialogSubview>(this).ToList();
		RebuildPageButtons();
		Game.serv.mouseovers.Register(MouseoverType.LedgerDialog, new LedgerMouseover());
	}

	internal override void Release()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.LedgerDialog);
		subviews = null;
		reportsview = null;
		chartsview = null;
		base.Release();
	}

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		SwitchToSubview(reportsview, force: true);
	}

	internal void SwitchToSubview(LedgerDialogSubview expected, bool force = false)
	{
		if (expected.IsActive && !force)
		{
			return;
		}
		foreach (LedgerDialogSubview subview in subviews)
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

	private void RebuildPageButtons()
	{
		GameObject child = _go.GetChild("Panel/Reports");
		child.transform.DestroyAllChildren();
		foreach (LedgerButtonDef hANDLER in LedgerModel.HANDLERS)
		{
			AddButton(child, hANDLER);
		}
	}

	private void AddButton(GameObject container, LedgerButtonDef def)
	{
		LedgerButtonCtx orAddComponent = Object.Instantiate(_tmplButton, container.transform).GetOrAddComponent<LedgerButtonCtx>();
		ToggleGroup component = container.GetComponent<ToggleGroup>();
		orAddComponent.Initialize(def, component, base.Controller.OnLedgerButtonClick);
	}

	internal void RefreshReportDisplay()
	{
		reportsview.RefreshSubview();
		Game.serv.mouseovers.OnMouseOut(MouseoverType.LedgerDialog);
	}

	public override void Hide()
	{
		_go.GetChild("Panel/Reports").GetComponent<ToggleGroup>().SetAllTogglesOff();
		base.Hide();
	}
}
