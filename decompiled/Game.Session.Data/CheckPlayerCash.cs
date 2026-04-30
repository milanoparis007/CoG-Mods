using Game.Services;
using SomaSim.Util;

namespace Game.Session.Data;

public class CheckPlayerCash : VisitWithValueRequirement
{
	public enum Type
	{
		Current,
		Highwater
	}

	public Type type;

	protected override Fixnum GetCurrentValue(VisitState visit)
	{
		if (type != Type.Highwater)
		{
			return GetPlayer(visit).finances.GetMoneyTotal().cash;
		}
		return GetPlayer(visit).finances.GetMoneyHighWatermark().cash;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), (type == Type.Highwater) ? Loc.Get("ui.requirements.cash.highwater", "value", Loc.Money(value)) : Loc.Get("ui.requirements.cash.total", "value", Loc.Money(value)));
	}
}
