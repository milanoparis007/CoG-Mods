using SomaSim.Util;

namespace Game.UI.Mouseovers;

public abstract class BaseCustomTextMouseover : BaseMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverTL;

	protected abstract string ProduceText();

	public override void RefreshContents()
	{
		string text = ProduceText();
		go.SetText("Text", text ?? "");
	}
}
