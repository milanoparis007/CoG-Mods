using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public class ConversationButtonMouseover : BaseMouseover
{
	public override UIMouseoverName MouseoverAssetName => UIMouseoverName.ConvoMouseover;

	protected string ProduceText(ConversationButtonContext ctx)
	{
		if (ctx == null)
		{
			return null;
		}
		return ctx.ProduceButtonMouseover();
	}

	public override void RefreshContents()
	{
		ConversationButtonContext component = context.GetComponent<ConversationButtonContext>();
		string text = ProduceText(component);
		go.SetText("Text", text ?? "");
		if (text == null)
		{
			Game.serv.mouseovers.OnMouseOut(MouseoverType.ConvoButton);
		}
	}
}
