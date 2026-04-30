using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class OutpostSettings
{
	public sealed class ExpansionDef
	{
		public Label id;

		public string locdesc;

		public string locline;

		public string convonpc;

		public string convosay;

		public VisitRequirementList visreqs;

		public ModValue monthlyCost;

		internal Price FindMonthlyCost(ModQuery q)
		{
			return new Price(monthlyCost.Evaluate(q).RoundCoarse());
		}
	}

	public ModValue buildCost;

	public ModValue monthlyCost;

	public ModValue tributePerBiz;

	public ModValue respTargetAtOutpost;

	public ModValue respTargetNearOutpost;

	public ModValue pumpPerDayUp;

	public ModValue pumpPerDayDown;

	public List<ExpansionDef> expansionDefs;

	public ExpansionDef FindExpansion(Label id)
	{
		foreach (ExpansionDef expansionDef in expansionDefs)
		{
			if (expansionDef.id == id)
			{
				return expansionDef;
			}
		}
		return null;
	}

	public List<ExpansionDef> GenerateMatchingExpansionDefs(VisitState visit)
	{
		List<ExpansionDef> list = expansionDefs.Where((ExpansionDef e) => e.visreqs == null || e.visreqs.AllPass(visit)).ToList();
		if (list.Count != 0)
		{
			return list;
		}
		return new List<ExpansionDef> { expansionDefs.LastOrDefaultFast() };
	}
}
