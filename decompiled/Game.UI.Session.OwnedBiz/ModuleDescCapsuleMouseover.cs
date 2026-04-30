using Game.UI.Mouseovers;

namespace Game.UI.Session.OwnedBiz;

public class ModuleDescCapsuleMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		ModuleDescCapsuleContext component = context.GetComponent<ModuleDescCapsuleContext>();
		if (!(component != null))
		{
			return null;
		}
		return component.text;
	}
}
