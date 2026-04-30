using Game.Core;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public abstract class AbstractSpecialCanPayButton : IConvoButtonRequirement, IRequirement
{
	public abstract Price? FindPrice(VisitState visit, ConvoButtonState bstate);

	public virtual bool CanPay(VisitState visit, Price price)
	{
		return this.CanChangePlayerMoney(visit, price);
	}

	public (Price price, bool canAfford) Evaluate(VisitState visit, ConvoButtonState bstate)
	{
		Price? price = FindPrice(visit, bstate);
		if (!price.HasValue)
		{
			return (price: Price.ZERO, canAfford: false);
		}
		Price value = price.Value;
		bool item = CanPay(visit, value);
		return (price: value, canAfford: item);
	}

	public bool DoesPass(VisitState visit, ConvoButtonState bstate)
	{
		return Evaluate(visit, bstate).canAfford;
	}

	public ReqExplanation Explain(VisitState visit, ConvoButtonState bstate)
	{
		var (price, flag) = Evaluate(visit, bstate);
		return new ReqExplanation(flag, this.MakeCostExplanation(visit, price, flag));
	}
}
