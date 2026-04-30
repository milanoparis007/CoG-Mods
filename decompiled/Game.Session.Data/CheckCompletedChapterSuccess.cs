using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckCompletedChapterSuccess : AbstractVisitRequirement
{
	public bool succeeded;

	public Label id;

	public override bool DoesPass(VisitState visit)
	{
		IEnumerable<HistoryItem> completedChapterDatasById = visit.GetPlayer().schemes.GetCompletedChapterDatasById(id);
		if (completedChapterDatasById.Count() == 0)
		{
			return false;
		}
		bool result = false;
		bool result2 = false;
		foreach (HistoryItem item in completedChapterDatasById)
		{
			if (item.success)
			{
				result = true;
			}
			else
			{
				result2 = true;
			}
		}
		if (!succeeded)
		{
			return result2;
		}
		return result;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), null);
	}
}
