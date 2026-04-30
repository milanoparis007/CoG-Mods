using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class SpecialResEventResultBlurb : ConvoBlurb
{
	public string suffix;

	public override string GetBlurb(ConversationModel model, ConvoButton button, string[] replacements)
	{
		ResEventData resEventData = model.visit.building?.components.residence?.GetResEventOrNull();
		if (resEventData == null)
		{
			return null;
		}
		ResidentialEventResultConfig config = resEventData.chosenResult.GetConfig();
		if (config == null)
		{
			return null;
		}
		Entity entity = resEventData.chosenResult.targetNpc.FindEntity();
		string fullName = entity.data.person.FullName;
		Fixnum value = model.visit.GetPlayer().social.GetRelationshipFromSourceToPlayer(entity.Id)?.Evaluate().current ?? ((Fixnum)0);
		string key = config.locroot + "." + suffix;
		replacements = AddReplacements(replacements, "name", model.visit.npc.data.person.FullName, "othername", fullName, "rel", Loc.FormatNumber(value));
		object[] replacements2 = replacements;
		return Loc.Get(key, replacements2);
	}
}
