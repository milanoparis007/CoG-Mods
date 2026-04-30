using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class BuySellItemBlurb : ConvoBlurb
{
	public string buykey;

	public string sellkey;

	public override string GetBlurb(ConversationModel _, ConvoButton button, string[] replacements)
	{
		ConvoDataBuySell data = button.GetData<ConvoDataBuySell>();
		if (data == null)
		{
			Logger.Warning("Missing buysell data?");
			return null;
		}
		return Loc.Get(data.playerBuys ? buykey : sellkey, replacements);
	}
}
