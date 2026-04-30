using Game.Core;
using Game.Session.Player;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class SpecialControlledBuildingBlurb : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		PlayerID pid = model.visit.building?.components.building?.GetControllingPlayer() ?? PlayerID.INVALID;
		string text = (pid.IsAnyPlayer ? pid.FindPlayer().social.FindPlayerGroupNameColorized() : "?");
		replacements = AddReplacements(replacements, "groupname", text);
		return base.GetBlurb(model, button, replacements);
	}
}
