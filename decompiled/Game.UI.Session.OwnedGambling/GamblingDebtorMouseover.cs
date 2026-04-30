using Game.Services;
using Game.Session.Entities;
using Game.UI.Mouseovers;
using Game.UI.Util;

namespace Game.UI.Session.OwnedGambling;

public class GamblingDebtorMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		DebtorMouseoverCtx component = context.GetComponent<DebtorMouseoverCtx>();
		if (component == null)
		{
			return Loc.Get("ui.ownedcasino.regular.mo.empty-slot");
		}
		string text = PersonInfoUtil.GenerateTraitsList(component.debtor.FindEntity(), showDesc: true, showDescLong: false);
		bool flag = component.debt < 0;
		return Loc.Get(flag ? "ui.ownedcasino.regular.mo.debt" : "ui.ownedcasino.regular.mo.full", "name", component.debtor.FindEntity().data.person.FullName, "money", TextUtil.ColorRedIf(flag, Loc.Money(component.debt)), "traits", text, "maxcredit", TextUtil.ColorRedIf(predicate: true, Loc.Money(component.maxcredit)), "lastturn", TextUtil.ColorGreenRed(component.lastturn, Loc.Money(component.lastturn)));
	}
}
