using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public abstract class CheckListOfItems : AbstractVisitRequirement
{
	public enum Type
	{
		Any,
		All,
		None
	}

	public Type has;

	public List<Label> of;

	public override bool DoesPass(VisitState visit)
	{
		int num = ((of != null) ? of.Count : 0);
		int num2 = ((of != null) ? Count(visit) : 0);
		return has switch
		{
			Type.All => num2 == num, 
			Type.Any => num2 > 0, 
			Type.None => num2 == 0, 
			_ => false, 
		};
	}

	protected abstract int Count(VisitState visit);

	protected abstract void VerifyData(VisitState visit);

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), MakeExplanationText());
	}

	protected abstract string MakeExplanationText();

	protected virtual string MakeExplanationText(string singleAllAny, string singleNone, string pluralAll, string pluralAny, string pluralNone)
	{
		int num = of?.Count ?? 0;
		string text = ((num == 0) ? Loc.Get("ui.requirements.items.none") : string.Join(", ", of.Select((Label id) => IdToName(id)).Distinct()));
		string text2 = ((num <= 1 && has == Type.None) ? singleNone : ((num <= 1) ? singleAllAny : ((has == Type.All) ? pluralAll : ((has == Type.Any) ? pluralAny : pluralNone))));
		return Loc.Get("ui.requirements.item.format", "key", text2, "names", text);
	}

	protected abstract string IdToName(Label id);
}
