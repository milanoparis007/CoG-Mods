using SomaSim.Util;

namespace Game.Session.Data;

public abstract class VisitWithValueRequirement : AbstractVisitRequirement
{
	public Test @is;

	public Fixnum value;

	protected bool CheckCurrentValue(Fixnum current)
	{
		Fixnum testValue = value;
		return ValueUtil.TestCurrentValue(current, @is, testValue);
	}

	protected abstract Fixnum GetCurrentValue(VisitState visit);

	public override bool DoesPass(VisitState visit)
	{
		return CheckCurrentValue(GetCurrentValue(visit));
	}
}
