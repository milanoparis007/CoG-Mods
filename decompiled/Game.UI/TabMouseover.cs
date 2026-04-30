using Game.Services;
using Game.UI.Mouseovers;

namespace Game.UI;

public class TabMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		TabCtx component = context.GetComponent<TabCtx>();
		return Loc.Get("ui.scheme.stage-title", "title", Loc.Get(component.chapterDef.loctitle), "number", component.stage + 1);
	}
}
