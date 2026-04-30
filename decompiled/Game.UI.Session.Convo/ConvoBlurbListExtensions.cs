using Game.Session.Data;

namespace Game.UI.Session.Convo;

public static class ConvoBlurbListExtensions
{
	public static string GetFirstBlurb(this ConvoBlurbList blurbs, ConversationModel model, ConvoButton button, params string[] replacements)
	{
		if (blurbs == null)
		{
			return null;
		}
		foreach (ConvoBlurb blurb in blurbs)
		{
			if (blurb.reqs == null || blurb.reqs.AllPass(model.visit))
			{
				return blurb.GetBlurb(model, button, replacements);
			}
		}
		return null;
	}
}
