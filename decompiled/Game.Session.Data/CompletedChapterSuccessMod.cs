using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class CompletedChapterSuccessMod : BaseDeltaMultiplierModifier
{
	public Label id;

	public bool succeeded;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Player;

	public override bool DoesPass(ModQuery query)
	{
		IEnumerable<HistoryItem> completedChapterDatasById = query.FindPlayer().schemes.GetCompletedChapterDatasById(id);
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

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.chapter-success", "delta", AbstractModifier.FormatDelta(delta), "chapter", Loc.Get(Game.serv.globals.settings.schemes.FindChapterById(id).loctitle));
	}
}
