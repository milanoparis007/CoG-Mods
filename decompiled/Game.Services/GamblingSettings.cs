using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;

namespace Game.Services;

public sealed class GamblingSettings : IValidatingSettings
{
	public GamblingAOE aoe;

	public List<AmenityDef> amenityDefs = new List<AmenityDef>();

	public List<DebtLevelDef> debtLevels = new List<DebtLevelDef>();

	public List<GamblingRepayment> debtRepayments = new List<GamblingRepayment>();

	public GamblingBets bets;

	public GamblingGeneral startup;

	public AmenityDef FindAmenityById(Label id)
	{
		foreach (AmenityDef amenityDef in amenityDefs)
		{
			if (amenityDef.id == id)
			{
				return amenityDef;
			}
		}
		return null;
	}

	public DebtLevelDef FindDebtById(Label id)
	{
		foreach (DebtLevelDef debtLevel in debtLevels)
		{
			if (debtLevel.id == id)
			{
				return debtLevel;
			}
		}
		return null;
	}

	public GamblingRepayment FindRepaymentById(Label id)
	{
		foreach (GamblingRepayment debtRepayment in debtRepayments)
		{
			if (debtRepayment.id == id)
			{
				return debtRepayment;
			}
		}
		return null;
	}

	public void Validate()
	{
	}

	[Conditional("UNITY_EDITOR")]
	private void ValidateRepaymentChoices()
	{
		foreach (DebtLevelDef debtLevel in debtLevels)
		{
			foreach (GamblingRepayChoice repayment in debtLevel.repayments)
			{
				ValidateDef(debtLevel, repayment);
			}
		}
		static void ValidateDef(DebtLevelDef level, GamblingRepayChoice rc)
		{
			if (rc.idlist != null)
			{
				foreach (Label item in rc.idlist)
				{
					ValidateDefId(level, item);
				}
				return;
			}
			ValidateDefId(level, rc.id);
		}
		static void ValidateDefId(DebtLevelDef level, Label id)
		{
		}
	}
}
