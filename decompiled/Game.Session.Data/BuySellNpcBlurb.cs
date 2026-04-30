using System.Collections.Generic;
using Game.Services;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class BuySellNpcBlurb : ConvoBlurb
{
	public string rootkey;

	public string buydeets;

	public string selldeets;

	public string buyselldeets;

	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		string key = ConvoBlurbUtils.FindRelationshipBlurbKey(model, rootkey);
		string text = MakeDetailsList(model, button);
		replacements = AddReplacements(replacements, "details", text);
		object[] replacements2 = replacements;
		return Loc.Get(key, replacements2);
	}

	private string MakeDetailsList(ConversationModel model, ConvoButton _)
	{
		ConvoBuySellDefs items = ConvoBlurbUtils.GenerateBuySellDefs(model.visit);
		string text = MakeDetailSublist(items, playerBuys: false);
		string text2 = MakeDetailSublist(items, playerBuys: true);
		string text3 = ((text != null && text2 != null) ? buyselldeets : ((text != null) ? buydeets : ((text2 != null) ? selldeets : null)));
		string obj = ((text3 == null) ? "" : Loc.Get(text3, "blist", text, "slist", text2));
		string text4 = FindExtraEpilogue(model, items);
		return (obj + "\n" + text4).Trim();
	}

	private string FindExtraEpilogue(ConversationModel model, ConvoBuySellDefs items)
	{
		bool hasLockedIllegal = items.hasLockedIllegal;
		bool hasLockedUnknown = items.hasLockedUnknown;
		BusinessSettings.BuySellIllegalEtc buySellIllegalEtc = Game.serv.globals.settings.people.businessSettings.buySellIllegalEtc;
		string text;
		string text2;
		if (!(hasLockedIllegal && hasLockedUnknown))
		{
			if (!hasLockedIllegal)
			{
				(text, text2) = (hasLockedUnknown ? (buySellIllegalEtc.unknown, buySellIllegalEtc.unknownmo) : (null, null));
			}
			else
			{
				string notrust = buySellIllegalEtc.notrust;
				string notrustmo = buySellIllegalEtc.notrustmo;
				text = notrust;
				text2 = notrustmo;
			}
		}
		else
		{
			string both = buySellIllegalEtc.both;
			string notrustmo = buySellIllegalEtc.bothmo;
			text = both;
			text2 = notrustmo;
		}
		if (text != null && text2 != null)
		{
			return ConvoBlurbUtils.LocWithGender(model.visit.npc, text);
		}
		return "";
	}

	private string MakeDetailSublist(ConvoBuySellDefs items, bool playerBuys)
	{
		List<ConvoBuySellDefs.Item> showingCards = items.GetShowingCards(playerBuys);
		if (showingCards.Count == 0)
		{
			return null;
		}
		return ConvoBlurbUtils.LocList(showingCards.SelectIntoNewList(delegate(ConvoBuySellDefs.Item item)
		{
			string listName = item.elt.item.FindResource().GetListName();
			return "<b>" + listName + "</b>";
		}));
	}
}
