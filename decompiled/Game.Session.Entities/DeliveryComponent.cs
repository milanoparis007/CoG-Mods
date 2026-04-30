using Game.Core;
using Game.Session.Data;

namespace Game.Session.Entities;

public sealed class DeliveryComponent : BaseComponent
{
	public static readonly SimTimeSpan RECENCY_THRESHOLD = new SimTimeSpan(90);

	internal void OnAutomatedDelivery(PlayerID pid, SimTime now, QtyAndDir qtyAndDir, bool insufficient, bool failed)
	{
		if (pid.IsHumanPlayer)
		{
			_entity.data.delivery.lastHumanDelivery = new DeliveryData.RememberedDelivery
			{
				pid = pid,
				time = now,
				qtyAndDir = qtyAndDir,
				insufficient = insufficient,
				failed = failed
			};
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.BuildingDeliveryHappened, _entity.Id, pid));
		}
	}

	public DeliveryData.RememberedDelivery? GetLastHumanDelivery(SimTime now)
	{
		DeliveryData.RememberedDelivery? result = _entity.data.delivery?.lastHumanDelivery;
		if (!result.HasValue || !result.Value.pid.IsHumanPlayer || result.Value.time.days != now.days)
		{
			return null;
		}
		return result;
	}

	public bool HasRecentHumanDelivery()
	{
		return HasRecentDelivery().IsHumanPlayer;
	}

	public PlayerID HasRecentDelivery()
	{
		DeliveryData.RememberedDelivery rememberedDelivery = _entity.data.delivery?.lastHumanDelivery ?? default(DeliveryData.RememberedDelivery);
		if (!rememberedDelivery.pid.IsAnyPlayer)
		{
			return PlayerID.INVALID;
		}
		SimTime simTime = Game.ctx.clock.Now.IncrementDays(-RECENCY_THRESHOLD.deltadays);
		if (rememberedDelivery.time < simTime)
		{
			return PlayerID.INVALID;
		}
		return rememberedDelivery.pid;
	}
}
