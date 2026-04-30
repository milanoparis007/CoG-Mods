using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class SpecialResEventBlurb : ConvoBlurb
{
	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		if (!((button?.state.data ?? model.state?.data) is ConvoDataResEvent convoDataResEvent))
		{
			return null;
		}
		if (!convoDataResEvent.IsValid)
		{
			return null;
		}
		ResidentialEventConfig residentialEventConfig = Game.serv.globals.settings.people.residentialEvents.FindEvent(convoDataResEvent.eventId);
		Entity entity = convoDataResEvent.hostNpc.FindEntity();
		return Loc.GetGendered(residentialEventConfig.npcintro, entity.data.person.g, replacements);
	}
}
