using Game.Core;
using Game.Services;
using Game.UI.Mouseovers;

namespace Game.UI.Session.OwnedBiz;

public class ModuleToggleMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		ModuleToggleContext component = context.GetComponent<ModuleToggleContext>();
		if (component.HasMouseover)
		{
			return component.mouseover;
		}
		string text = ((component.module != null) ? "occupied" : "available");
		Label label = component.slotdef?.GetFirstTag() ?? Label.NULL;
		string key = "module." + text + "." + label.String + ".mo";
		if (Game.serv.loc.HasKey(key))
		{
			return component.MakeModuleButtonText() + "\n\n" + Loc.Get(key);
		}
		return null;
	}
}
