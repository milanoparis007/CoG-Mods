using Game.Services;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class RelationshipBasedBlurb : ConvoBlurb
{
	public string rootkey;

	public override string GetBlurb(ConversationModel model, ConvoButton _, string[] replacements)
	{
		return Loc.Get(ConvoBlurbUtils.FindRelationshipBlurbKey(model, rootkey), replacements);
	}
}
