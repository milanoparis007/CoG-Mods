using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Quests;

public abstract class DeliveryGoal : BaseGoal
{
	public sealed class Summary
	{
		public VisitState visit;

		public ResOrCash delivered;
	}

	public abstract string LocBriefKey { get; }

	public override bool IsRefundable => true;

	public abstract string LocRefundKey { get; }

	public DeliveryGoal()
	{
	}

	public override void OnAdded()
	{
	}

	public override void OnRemoved()
	{
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		LocReplacementContext result = base.MakeStringReplacements();
		if (state.target.IsValid)
		{
			Entity peep = state.target.FindEntity();
			result = result.SetPerson(NameUtils.MakeLocPerson(peep));
		}
		return result;
	}

	public virtual string GetLocBrief()
	{
		return Loc.Get(LocBriefKey, MakeStringReplacements());
	}

	public override bool OnRefund(Fixnum refundRate)
	{
		return RefundImplementation(refundRate);
	}

	protected abstract string DescribeRefund(Fixnum refundRate);

	protected abstract bool RefundImplementation(Fixnum refundRate);
}
