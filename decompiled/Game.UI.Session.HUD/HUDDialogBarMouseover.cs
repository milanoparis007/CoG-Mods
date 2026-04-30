using Game.UI.Mouseovers;

namespace Game.UI.Session.HUD;

public class HUDDialogBarMouseover : BaseCustomTextMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

	protected override string ProduceText()
	{
		HUDDialogBar.ButtonContext component = context.GetComponent<HUDDialogBar.ButtonContext>();
		if (!(component != null))
		{
			return null;
		}
		return component.def?.mo;
	}
}
