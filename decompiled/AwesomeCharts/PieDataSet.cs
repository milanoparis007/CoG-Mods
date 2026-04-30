using System;
using System.Collections.Generic;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class PieDataSet : DataSet<PieEntry>
{
	private float totalValue;

	private List<float> calculatedPercentValues = new List<float>();

	private List<float> calculatedRotationValues = new List<float>();

	[SerializeField]
	private bool valuesAsPercentages;

	public bool ValuesAsPercentages
	{
		get
		{
			return valuesAsPercentages;
		}
		set
		{
			valuesAsPercentages = value;
			RecalculateValues();
		}
	}

	public PieDataSet()
		: this("")
	{
	}

	public PieDataSet(string title)
		: base(title)
	{
	}

	public PieDataSet(string title, List<PieEntry> entries)
		: base(title, entries)
	{
	}

	internal void RecalculateValues()
	{
		OnEntriesChanged();
	}

	protected override void OnEntriesChanged()
	{
		totalValue = CalculateTotalValue();
		calculatedPercentValues = CalculatePercentValues();
		calculatedRotationValues = CalculateRotationValues();
	}

	private float CalculateTotalValue()
	{
		float num = 0f;
		if (ValuesAsPercentages)
		{
			num = 100f;
		}
		else
		{
			foreach (PieEntry entry in base.Entries)
			{
				num += entry.Value;
			}
		}
		return num;
	}

	private List<float> CalculatePercentValues()
	{
		List<float> list = new List<float>();
		for (int i = 0; i < GetEntriesCount(); i++)
		{
			list.Add(base.Entries[i].Value / totalValue);
		}
		return list;
	}

	private List<float> CalculateRotationValues()
	{
		List<float> list = new List<float>();
		float num = 0f;
		for (int i = 0; i < GetEntriesCount(); i++)
		{
			list.Add(num * 360f);
			num += calculatedPercentValues[i];
		}
		return list;
	}

	public override List<PieEntry> GetSortedEntries()
	{
		return base.Entries;
	}

	public float GetTotalValue()
	{
		return totalValue;
	}

	public float GetPercentValue(int index)
	{
		if (calculatedPercentValues.Count > index)
		{
			return calculatedPercentValues[index];
		}
		return 0f;
	}

	public float GetRotationValue(int index)
	{
		if (calculatedRotationValues.Count > index)
		{
			return calculatedRotationValues[index];
		}
		return 0f;
	}

	public int EntryIndexForAngle(double angle)
	{
		for (int i = 0; i < GetEntriesCount(); i++)
		{
			if ((double)(GetRotationValue(i) + GetPercentValue(i) * 360f) > angle)
			{
				return i;
			}
		}
		return -1;
	}
}
