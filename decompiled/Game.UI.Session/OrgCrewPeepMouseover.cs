using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.UI.Mouseovers;

namespace Game.UI.Session;

public class OrgCrewPeepMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		Entity entity = context.GetComponent<OrgCrewPeepMouseoverCtx>().crew.FindEntity();
		string text = PersonInfoUtil.GenerateTraitsList(entity, showDesc: true, showDescLong: false);
		Dictionary<CrewStats, int> mostRelevantCrewStats = entity.components.agent.GetMostRelevantCrewStats();
		string text2 = "";
		foreach (KeyValuePair<CrewStats, int> item in mostRelevantCrewStats)
		{
			if (item.Value != 0)
			{
				text2 = text2 + Loc.GetCrewStatWithValueSpace(item.Key, Loc.GetCrewStatUnit(item.Key, item.Value)) + "\n";
			}
		}
		return Loc.Get("ui.orgchart.crew.mo", "name", entity.data.person.FullName, "topStats", text2, "traits", text);
	}
}
