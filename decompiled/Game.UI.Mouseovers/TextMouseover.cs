using SomaSim.Util;

namespace Game.UI.Mouseovers;

public class TextMouseover : BaseMouseover
{
	private readonly bool rightAligned;

	public override UIMouseoverName MouseoverAssetName
	{
		get
		{
			if (!rightAligned)
			{
				return UIMouseoverName.TextMouseoverTL;
			}
			return UIMouseoverName.TextMouseoverTR;
		}
	}

	public TextMouseover(bool rightAligned)
	{
		this.rightAligned = rightAligned;
	}

	public override void RefreshContents()
	{
		TextMouseoverContext component = context.GetComponent<TextMouseoverContext>();
		if (component != null)
		{
			string text = component.GetText();
			go.GetText("Text").text = text;
		}
	}
}
