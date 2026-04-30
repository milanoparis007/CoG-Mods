using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class SpecialAtOutpostStateBlurb : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		string costs = GetCosts(model);
		string[] replacements2 = AddReplacements(replacements, "costs", costs);
		return base.GetBlurb(model, button, replacements2);
	}

	private string GetCosts(ConversationModel model)
	{
		if (!model.IsBusinessVisitOK)
		{
			return "";
		}
		OutpostID outpostAt = Game.ctx.players.Human.outposts.GetOutpostAt(model.visit.building);
		if (outpostAt.IsNotValid)
		{
			return "";
		}
		List<(string, Price)> list = Game.ctx.players.Human.outposts.ExplainExpansions(outpostAt).ToList();
		if (list.Count == 0)
		{
			return "";
		}
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		stringBuilder.AppendLine("\n");
		stringBuilder.AppendLine(Loc.Get("convo.at-outpost-npc.costs"));
		foreach (var item in list)
		{
			stringBuilder.AppendLine(Loc.Get("convo.at-outpost-npc.costline", "desc", item.Item1, "cost", Loc.Price(item.Item2, abs: true)));
		}
		return stringBuilder.ToStringAndReturnToPool();
	}
}
