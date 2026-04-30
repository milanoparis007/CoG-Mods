using Game.Services;
using Game.UI.Mouseovers;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI.Session.Ledger;

public class ReportsSubview : LedgerDialogSubview
{
	internal class LinkClickHelper : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
	{
		public LedgerDialog view;

		public int lastIndexMO = -1;

		private int FindIndexUnderCursor()
		{
			TextMeshProUGUI component = base.gameObject.GetComponent<TextMeshProUGUI>();
			int num = TMP_TextUtilities.FindIntersectingLink(component, Input.mousePosition, null);
			if (num == -1)
			{
				return -1;
			}
			TMP_LinkInfo tMP_LinkInfo = component.textInfo.linkInfo[num];
			if (!int.TryParse(tMP_LinkInfo.GetLinkID(), out var result))
			{
				return -1;
			}
			return result;
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (view != null)
			{
				int num = FindIndexUnderCursor();
				if (num != -1)
				{
					view.Controller.SortCurrentReport(num);
				}
			}
		}

		private void UpdateMouseover(int column)
		{
			if (Game.serv.mouseovers.ShowingType == MouseoverType.LedgerDialog && column != lastIndexMO)
			{
				lastIndexMO = column;
				if (column != -1 && Game.serv.mouseovers.GetMouseoverUnsafe(MouseoverType.LedgerDialog) is LedgerMouseover ledgerMouseover)
				{
					var (cansort, text) = view.reportsview.GetMouseoverText(column);
					ledgerMouseover.SetText(cansort, text);
				}
			}
		}

		private void OnDestroy()
		{
			view = null;
		}

		private void Update()
		{
			if (Game.serv.mouseovers.ShowingType == MouseoverType.LedgerDialog && view != null)
			{
				int column = FindIndexUnderCursor();
				UpdateMouseover(column);
			}
		}
	}

	private TextMeshProUGUI _title;

	private TextMeshProUGUI _header;

	private TextMeshProUGUI _body;

	private const string PANEL_TITLE = "Printout/Title";

	private const string PANEL_SCROLLVIEW = "Printout/Scroll View";

	private const string PANEL_BODY = "Printout/Scroll View/Viewport/Content/Text";

	private const string PANEL_HEADER = "Printout/Header Text";

	public ReportsSubview(GameObject go, string panelName, LedgerController controller)
		: base(go, panelName, controller)
	{
	}

	public override void Activate()
	{
		base.Activate();
		_title = panel.GetText("Printout/Title");
		_header = panel.GetText("Printout/Header Text");
		_body = panel.GetText("Printout/Scroll View/Viewport/Content/Text");
		_header.gameObject.GetOrAddComponent<LinkClickHelper>().view = View;
		InitButtonsAndPrintout();
	}

	public override void Deactivate()
	{
		_title = (_header = (_body = null));
		base.Deactivate();
	}

	private void InitButtonsAndPrintout()
	{
		_title.text = Loc.Get("ledger.title");
		_body.text = Cell.MaybeExpandText(Loc.Get("ledger.greeting"));
		_header.gameObject.SetActive(value: false);
	}

	public override void RefreshSubview()
	{
		ReportDataModel currentReport = Model.CurrentReport;
		if (currentReport == null)
		{
			InitButtonsAndPrintout();
			return;
		}
		_title.text = currentReport.title;
		_body.text = currentReport.Build();
		_header.text = currentReport.header.GetOrMakeLine();
		_header.gameObject.SetActive(currentReport.columns > 1);
		panel.GetChild("Printout/Scroll View").GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
	}

	public (bool cansort, string text) GetMouseoverText(int column)
	{
		ReportDataModel currentReport = Model.CurrentReport;
		if (currentReport == null)
		{
			return (cansort: false, text: "");
		}
		bool canBeResorted = currentReport.CanBeResorted;
		if (column == -1 || column >= currentReport.columns)
		{
			return (cansort: canBeResorted, text: currentReport.title);
		}
		string mo = currentReport.header.cells[column].mo;
		return (cansort: canBeResorted, text: mo);
	}
}
