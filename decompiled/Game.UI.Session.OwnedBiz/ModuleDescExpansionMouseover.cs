using Game.Services;
using Game.UI.Mouseovers;

namespace Game.UI.Session.OwnedBiz;

public class ModuleDescExpansionMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		ViewDescribeModule.ExpansionButtonContext component = context.GetComponent<ViewDescribeModule.ExpansionButtonContext>();
		if (component.exp == null)
		{
			return Loc.Get("ui.viewdescribemodule.module.mo.none");
		}
		string text = Loc.Get(component.exp.display.locname);
		string text2 = Loc.Get(component.exp.display.locdesc);
		return Loc.Get("ui.viewdescribemodule.module.mo", "name", text, "desc", text2);
	}
}
