using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public struct DeliveryInfo
{
	public static readonly DeliveryInfo INVALID;

	public Label id;

	public int days;

	public QtyAndDir max;

	public bool IsValid => id.IsSet;

	public bool IsNotValid => id.IsNotSet;

	public DeliveryInfo(Label id, int days, Fixnum maxQty, bool toBuilding)
	{
		this.id = id;
		this.days = days;
		max = new QtyAndDir(maxQty, toBuilding);
	}
}
