using Game.Services;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public class ConvoBlurb
{
	public VisitRequirementList reqs;

	public string lockey;

	public virtual string GetBlurb(ConversationModel _, ConvoButton __, string[] replacements)
	{
		return Loc.Get(lockey, replacements);
	}

	protected string[] AddReplacements(string[] replacements, params string[] extras)
	{
		if (replacements != null)
		{
			return replacements.Append(extras);
		}
		return extras;
	}
}
