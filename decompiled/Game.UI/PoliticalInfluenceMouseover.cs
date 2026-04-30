using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Mouseovers;

namespace Game.UI;

public class PoliticalInfluenceMouseover : BaseCustomTextMouseover
{
	protected override string ProduceText()
	{
		string text = "";
		List<InfluenceSource> humanInfluenceBreakdown = Game.ctx.simman.politics.GetHumanInfluenceBreakdown();
		foreach (InfluenceSource item in humanInfluenceBreakdown)
		{
			EntityID source = item.source;
			text = ((!source.IsNotValid) ? (text + Loc.Get("ui.law-shop.influence-source.candidate", "amount", item.amount, "candidate", item.source.FindEntity().data.person.FullName) + "\n") : (text + Loc.Get("ui.law-shop.influence-source.debug", "amount", item.amount) + "\n"));
		}
		if (humanInfluenceBreakdown.Count() != 0)
		{
			return text;
		}
		return Loc.Get("ui.law-shop.influence-source.none");
	}
}
