using System.Linq;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckOwnerRelbuffs : CheckListOfItems
{
	protected override int Count(VisitState visit)
	{
		Relationship relationshipFromSourceToPlayer = GetPlayer(visit).social.GetRelationshipFromSourceToPlayer(visit);
		if (relationshipFromSourceToPlayer == null || relationshipFromSourceToPlayer.buffs == null)
		{
			return 0;
		}
		int num = 0;
		foreach (Label item in of)
		{
			if (relationshipFromSourceToPlayer.buffs.Contains(item))
			{
				num++;
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState visit)
	{
		BuffSettings allBuffs = Game.serv.globals.settings.people.allBuffs;
		foreach (Label item in of)
		{
			if (allBuffs.GetConfig(item) == null)
			{
				Label label = item;
				Logger.Warning("Check owner: unknown relbuff id: " + label.ToString());
			}
		}
	}

	protected override string MakeExplanationText()
	{
		return string.Join("\n", of.Select((Label id) => IdToName(id)));
	}

	protected override string IdToName(Label id)
	{
		return Game.serv.globals.settings.people.allBuffs.GetConfig(id)?.GetDesc() ?? "";
	}
}
