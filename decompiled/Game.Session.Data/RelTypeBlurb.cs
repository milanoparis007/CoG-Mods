using Game.Services;
using Game.Session.Entities;
using Game.UI.Session.Convo;

namespace Game.Session.Data;

public sealed class RelTypeBlurb : ConvoBlurb
{
	public enum Type
	{
		Player,
		Target,
		Owner
	}

	public Type to;

	public string keyroot;

	private (Entity fromPeep, Entity toPeep) GetTargetPeep(ConversationModel model)
	{
		Entity npc = model.visit.npc;
		Entity playerPeep = model.visit.GetPlayer().social.GetPlayerPeep();
		switch (to)
		{
		case Type.Player:
			return (fromPeep: npc, toPeep: playerPeep);
		case Type.Owner:
			return (fromPeep: playerPeep, toPeep: npc);
		case Type.Target:
		{
			ConvoData data = model.state.data;
			if (!(data is ConvoDataCrewHire convoDataCrewHire))
			{
				if (!(data is ConvoDataNPCSelection convoDataNPCSelection))
				{
					if (!(data is ConvoDataTicketBuilding convoDataTicketBuilding))
					{
						break;
					}
					return (fromPeep: npc, toPeep: convoDataTicketBuilding.takeover.FindCandidate());
				}
				return (fromPeep: npc, toPeep: convoDataNPCSelection.selected.targetId.FindEntity());
			}
			return (fromPeep: npc, toPeep: convoDataCrewHire.targetId.FindEntity());
		}
		}
		Logger.Warning($"Convo: no target peep found for to = {to}, convodata = {model.state.data}");
		return (fromPeep: null, toPeep: null);
	}

	public override string GetBlurb(ConversationModel model, ConvoButton _, string[] replacements)
	{
		var (entity, entity2) = GetTargetPeep(model);
		if (entity == null || entity2 == null)
		{
			Logger.Warning("Missing convo data in rel-type-blurb, unsupported data " + model.state.data);
			return Loc.Get(keyroot);
		}
		return ConvoBlurbUtils.LocWithRelationship(entity, entity2, keyroot, replacements);
	}
}
