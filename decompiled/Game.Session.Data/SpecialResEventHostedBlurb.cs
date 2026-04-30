using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class SpecialResEventHostedBlurb : ConvoBlurb
{
	public string suffix;

	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		ResEventData resEventData = model.visit.building?.components.residence?.GetResEventOrNull();
		if (resEventData == null)
		{
			return null;
		}
		string key = resEventData.GetConfig().locroot + "." + suffix;
		replacements = AddReplacements(model, resEventData, replacements);
		object[] replacements2 = replacements;
		return Loc.Get(key, replacements2);
	}

	private string[] AddReplacements(ConversationModel model, ResEventData rev, string[] replacements)
	{
		Price cost = rev.GetCost(model.visit.pid);
		SimTime finishDate = rev.GetFinishDate();
		List<string> list = new List<string>();
		if (replacements != null)
		{
			list.AddRange(replacements);
		}
		return list.Concat(new string[8]
		{
			"price",
			Loc.Price(cost, abs: true),
			"date",
			Loc.FormatDateLong(finishDate),
			"name",
			model.visit.npc.data.person.FullName,
			"eventresult",
			rev.chosenResult?.eventResultMessage
		}).ToArray();
	}
}
