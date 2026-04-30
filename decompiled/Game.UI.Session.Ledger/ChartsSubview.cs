using System;
using System.Collections.Generic;
using AwesomeCharts;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session.Ledger;

public class ChartsSubview : LedgerDialogSubview
{
	public class LedgerListPair
	{
		public List<MoneyLedgerEntry> thisTurn;

		public List<MoneyLedgerEntry> prevTurn;

		public LedgerListPair()
		{
			thisTurn = new List<MoneyLedgerEntry>();
			prevTurn = new List<MoneyLedgerEntry>();
		}
	}

	private const int headerSizeLabel = 26;

	private const int headerSizeValue = 11;

	private const string listEntryIndent = "· ";

	private TextMeshProUGUI _info;

	private BarChart _barChart;

	private LineChart _lineChart;

	private const string PANEL_INFO = "Description/Scroll View/Viewport/Content/Text";

	private const string PANEL_HEADER = "Title";

	private const string CHART_TITLE = "Chart/Text";

	private const string CHART_LEGEND = "Chart/Legend";

	private const string BAR_CHART = "Chart/BarChart";

	private const string LINE_CHART = "Chart/LineChart";

	private const string GO_BUTTON_TEMPLATE = "Templates/Ledger Button";

	public ChartsSubview(GameObject go, string name, LedgerController c)
		: base(go, name, c)
	{
	}

	public override void Activate()
	{
		base.Activate();
		_info = panel.GetText("Description/Scroll View/Viewport/Content/Text");
		_barChart = panel.GetChild("Chart/BarChart").GetComponent<BarChart>();
		_lineChart = panel.GetChild("Chart/LineChart").GetComponent<LineChart>();
		panel.SetText("Title", Loc.Get("ledger.charts.header"));
		panel.SetText("Chart/Text", Loc.Get("ledger.charts.title", "num", 7));
		panel.SetText("Chart/Legend", Loc.Get("ledger.charts.legend"));
	}

	public override void Deactivate()
	{
		_info = null;
		base.Deactivate();
	}

	public override void RefreshSubview()
	{
		MoneyLedger moneyLedger = Game.ctx.players.Human.finances.Data.moneyLedger;
		RefreshChart(moneyLedger);
		RefreshDescription(moneyLedger);
	}

	private void RefreshDescription(MoneyLedger ledger)
	{
		_info.text = Loc.Get("lerger.charts.rhs") + "\n\n";
		TextMeshProUGUI info = _info;
		info.text = info.text + Cell.TrimOrPad(Loc.Get("ledger.charts.summary.hr.1"), 26, Cell.Alignment.Left) + Cell.TrimOrPad(Loc.Get("ledger.charts.summary.hr.2"), 11, Cell.Alignment.Right) + Cell.TrimOrPad(Loc.Get("ledger.charts.summary.hr.3"), 11, Cell.Alignment.Right) + "\n\n";
		info = _info;
		info.text = info.text + Cell.TrimOrPad(Loc.Get("ledger.charts.summary.startingcash"), 26, Cell.Alignment.Left) + Cell.TrimOrPad(LocMoney(ledger.turns[0].startMoney), 11, Cell.Alignment.Right) + Cell.TrimOrPad(LocMoney(ledger.turns[1].startMoney), 11, Cell.Alignment.Right) + "\n\n";
		LedgerListPair ledgerListPair = new LedgerListPair();
		LedgerListPair ledgerListPair2 = new LedgerListPair();
		LedgerListPair ledgerListPair3 = new LedgerListPair();
		Dictionary<MoneyReason, MoneyLedgerEntry> dictionary = new Dictionary<MoneyReason, MoneyLedgerEntry>();
		foreach (MoneyLedgerEntry entry in ledger.turns[0].entries)
		{
			if (!dictionary.ContainsKey(entry.reason))
			{
				if (entry.delta.cash != 0)
				{
					dictionary.Add(entry.reason, entry);
				}
			}
			else
			{
				MoneyLedgerEntry value = dictionary[entry.reason];
				value.delta += entry.delta;
				dictionary[entry.reason] = value;
			}
		}
		Dictionary<MoneyReason, MoneyLedgerEntry> dictionary2 = new Dictionary<MoneyReason, MoneyLedgerEntry>();
		foreach (MoneyLedgerEntry entry2 in ledger.turns[1].entries)
		{
			if (!dictionary2.ContainsKey(entry2.reason))
			{
				if (entry2.delta.cash != 0)
				{
					dictionary2.Add(entry2.reason, entry2);
				}
			}
			else
			{
				MoneyLedgerEntry value2 = dictionary2[entry2.reason];
				value2.delta += entry2.delta;
				dictionary2[entry2.reason] = value2;
			}
		}
		foreach (MoneyReason value3 in Enum.GetValues(typeof(MoneyReason)))
		{
			LedgerListPair ledgerListPair4 = (MoneyPerTurnListing.IsAnExpense(value3) ? ledgerListPair2 : (MoneyPerTurnListing.IsARevenue(value3) ? ledgerListPair : ledgerListPair3));
			bool flag = dictionary.ContainsKey(value3);
			bool flag2 = dictionary2.ContainsKey(value3);
			if (flag || flag2)
			{
				if (flag)
				{
					ledgerListPair4.thisTurn.Add(dictionary[value3]);
				}
				else
				{
					ledgerListPair4.thisTurn.Add(new MoneyLedgerEntry(value3, new Price(0), EntityID.INVALID));
				}
				if (flag2)
				{
					ledgerListPair4.prevTurn.Add(dictionary2[value3]);
				}
				else
				{
					ledgerListPair4.prevTurn.Add(new MoneyLedgerEntry(value3, new Price(0), EntityID.INVALID));
				}
			}
		}
		_info.text += Describe(ledgerListPair2, "ledger.charts.summary.expenses");
		_info.text += Describe(ledgerListPair, "ledger.charts.summary.revenues");
		_info.text += Describe(ledgerListPair3, "ledger.charts.summary.others");
		TextMeshProUGUI info2 = _info;
		info2.text = info2.text + Cell.TrimOrPad(Loc.Get("ledger.charts.summary.endingcash"), 26, Cell.Alignment.Left) + Cell.TrimOrPad(LocMoney(ledger.turns[0].endMoney), 11, Cell.Alignment.Right) + Cell.TrimOrPad(LocMoney(ledger.turns[1].endMoney), 11, Cell.Alignment.Right);
	}

	private string Describe(LedgerListPair pair, string key)
	{
		if (pair.thisTurn.Count <= 0)
		{
			return string.Empty;
		}
		return Cell.TrimOrPad(Loc.Get(key), 26, Cell.Alignment.Left) + "\n" + GetEntryPairString(pair) + "\n";
	}

	private string GetEntryPairString(LedgerListPair listPair)
	{
		string text = string.Empty;
		for (int i = 0; i < listPair.thisTurn.Count; i++)
		{
			text = text + Cell.TrimOrPad("· " + Loc.GetMoneyReason(listPair.thisTurn[i].reason), 26, Cell.Alignment.Left) + Cell.TrimOrPad(LocPrice(listPair.thisTurn[i].delta), 11, Cell.Alignment.Right) + Cell.TrimOrPad(LocPrice(listPair.prevTurn[i].delta), 11, Cell.Alignment.Right) + "\n";
		}
		return text;
	}

	private static string LocMoney(Money m)
	{
		return Loc.Money(m, 2);
	}

	private static string LocPrice(Price p)
	{
		return Loc.Price(p, abs: false, 2);
	}

	private void RefreshChart(MoneyLedger ledger)
	{
		List<LineEntry> list = new List<LineEntry>();
		List<LineEntry> list2 = new List<LineEntry>();
		List<BarEntry> list3 = new List<BarEntry>();
		for (int num = ledger.turns.Count - 1; num >= 0; num--)
		{
			list3.Add(new BarEntry(list3.Count, (int)ledger.turns[num].endMoney));
			list.Add(new LineEntry(list.Count, 0f));
			list2.Add(new LineEntry(list2.Count, 0f));
			foreach (MoneyLedgerEntry entry in ledger.turns[num].entries)
			{
				if ((float)(int)entry.delta < 0f)
				{
					list[list.Count - 1].Value += Mathf.Abs((float)(int)entry.delta);
				}
				if ((float)(int)entry.delta > 0f)
				{
					list2[list2.Count - 1].Value += (int)entry.delta;
				}
			}
		}
		_barChart.data.DataSets[0].Clear();
		_lineChart.GetChartData().DataSets[0].Clear();
		_lineChart.GetChartData().DataSets[1].Clear();
		_barChart.AxisConfig.VerticalAxisConfig.Bounds.Max = (float)ledger.lifetimeMax.cash;
		_barChart.AxisConfig.HorizontalAxisConfig.Bounds.Max = 6f;
		_lineChart.AxisConfig.VerticalAxisConfig.Bounds.Max = (float)ledger.lifetimeMax.cash;
		_lineChart.AxisConfig.HorizontalAxisConfig.Bounds.Max = 6f;
		_barChart.data.DataSets[0].Entries = list3;
		_lineChart.GetChartData().DataSets[0].Entries = list2;
		_lineChart.GetChartData().DataSets[1].Entries = list;
		_barChart.SetDirty();
		_lineChart.SetDirty();
	}
}
