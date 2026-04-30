using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public static class ConvoBlurbUtils
{
	public static string LocList(IList<string> items)
	{
		return LocListHelper(0);
		string LocListHelper(int pos)
		{
			int num = items.Count - 1;
			if (pos <= num)
			{
				if (pos != num)
				{
					if (pos != num - 1)
					{
						return Loc.Get("ui.list", "0", items[pos], "rest", LocListHelper(pos + 1));
					}
					return Loc.Get("ui.list.2", "0", items[num - 1], "1", items[num]);
				}
				return Loc.Get("ui.list.1", "0", items[num]);
			}
			return "";
		}
	}

	public static string LocWithQuestID(string keyroot, QuestUUID quest)
	{
		_ = quest.IsNotSet;
		return Loc.Get(keyroot);
	}

	public static string LocWithRelationship(Entity peep1, Entity peep2, string keyroot, string[] replacements)
	{
		Relationship orNull = Game.ctx.simman.rels.GetOrNull(peep1.Id, peep2.Id);
		if (orNull == null)
		{
			Logger.Warning("Missing relationship or other peep in rel-type-blurb");
			return Loc.Get(keyroot);
		}
		Gender g = peep2.data.person.g;
		string key = keyroot + Loc.RelSuffix(orNull.type, g);
		if (!Game.serv.loc.HasKey(key))
		{
			key = keyroot + Loc.RelSuffix(RelationshipType.None, g);
			if (!Game.serv.loc.HasKey(key))
			{
				key = keyroot;
			}
		}
		return NameUtils.LocWithContext(key, peep2, replacements);
	}

	public static string LocWithGender(Entity peep, string keyroot, params string[] replacements)
	{
		Gender g = peep.data.person.g;
		string key = keyroot + Loc.RelSuffix(RelationshipType.None, g);
		if (!Game.serv.loc.HasKey(key))
		{
			key = keyroot;
		}
		return NameUtils.LocWithContext(key, peep, replacements);
	}

	public static string FindRelationshipBlurbKey(ConversationModel model, string rootkey)
	{
		ModQuery query = model.visit.MakeOwnerModQuery();
		BusinessSettings businessSettings = Game.serv.globals.settings.people.businessSettings;
		Fixnum fixnum = businessSettings.positiveBlurbsAbove.Evaluate(query);
		Fixnum fixnum2 = businessSettings.negativeBlurbsBelow.Evaluate(query);
		Fixnum fixnum3 = Game.ctx.players.WithID(model.visit.pid).social.EvaluateRelationshipFromSourceToPlayer(model.GetConvoTarget().Id);
		string text = ((fixnum3 <= fixnum2) ? ".neg" : ((fixnum3 >= fixnum) ? ".pos" : ".any"));
		return rootkey + text;
	}

	public static ConvoBuySellDefs GenerateBuySellDefs(VisitState visit)
	{
		ConvoBuySellDefs convoBuySellDefs = new ConvoBuySellDefs();
		PlayerInfo player = visit.GetPlayer();
		InventoryModule inventoryModule = visit.vehicle?.components.modules.inventory;
		List<BuySellElement> list = (from buySellElement in visit.building?.components.modules.ProduceAllItemsPlayerCanBuyOrSell(visit.pid, playerBuys: true, playerSells: true).Distinct()
			orderby (buySellElement.item.consumed ? "1" : "0") + buySellElement.item.FindResource().GetName()
			select buySellElement).ToList();
		if (list != null)
		{
			bool flag = visit.GetPlayer().social.AreIllegalItemsLocked(visit.npc, visit.building);
			ModuleQuery q = ModulesUtil.MakeModuleQuery(visit.building);
			for (int num = 0; num < list.Count; num++)
			{
				BuySellElement elt = list[num];
				Resource resource = Resource.Find(elt.item.id);
				if (resource != null)
				{
					DeliveryInfo info = ModulesUtil.FindItemDeliveryFromModules(visit.building, q, resource.resid);
					bool flag2 = !player.skills.HasResourceUnlocked(resource.resid);
					bool flag3 = elt.item.illegal && flag;
					bool playerBuys = !elt.item.consumed;
					bool haveInCar = (inventoryModule?.data.Get(elt.item.id).qty ?? ((Fixnum)0)) > 0;
					bool cardShowing = !(flag3 || flag2);
					ConvoBuySellDefs.Item item = new ConvoBuySellDefs.Item
					{
						elt = elt,
						info = info,
						cardShowing = cardShowing,
						haveInCar = haveInCar,
						playerBuys = playerBuys,
						lockedIllegal = flag3,
						lockedUnknown = flag2
					};
					convoBuySellDefs.defs.Add(item);
				}
			}
		}
		convoBuySellDefs.cardsShowingCount = convoBuySellDefs.defs.Count((ConvoBuySellDefs.Item item2) => item2.cardShowing);
		convoBuySellDefs.hasLockedIllegal = convoBuySellDefs.defs.Any((ConvoBuySellDefs.Item item2) => item2.lockedIllegal);
		convoBuySellDefs.hasLockedUnknown = convoBuySellDefs.defs.Any((ConvoBuySellDefs.Item item2) => item2.lockedUnknown);
		return convoBuySellDefs;
	}
}
