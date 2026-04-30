using Game.Core;
using Game.Session.Data;

namespace Game.Session.Entities;

public sealed class DeliveryData : BaseData
{
	public struct RememberedDelivery
	{
		public PlayerID pid;

		public SimTime time;

		public QtyAndDir qtyAndDir;

		public bool insufficient;

		public bool failed;
	}

	public RememberedDelivery lastHumanDelivery;
}
