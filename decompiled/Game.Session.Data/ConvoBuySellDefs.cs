using System.Collections.Generic;
using System.Linq;

namespace Game.Session.Data;

public class ConvoBuySellDefs
{
	public struct Item
	{
		public BuySellElement elt;

		public DeliveryInfo info;

		public bool cardShowing;

		public bool playerBuys;

		public bool haveInCar;

		public bool lockedUnknown;

		public bool lockedIllegal;
	}

	public List<Item> defs = new List<Item>();

	public int cardsShowingCount;

	public bool hasLockedUnknown;

	public bool hasLockedIllegal;

	public List<Item> GetShowingCards(bool playerBuys)
	{
		return defs.Where((Item def) => def.cardShowing && def.playerBuys == playerBuys).ToList();
	}
}
