using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Player;

public static class TicketGoonBoosts
{
	public static List<(Entity peep, float val)> FindBoostTargets(VisitState visit)
	{
		return Game.ctx.players.Human.social.TicketActionFindGoonIntroTickets(visit);
	}

	public static bool CanBoostForGoon(VisitState visit)
	{
		return FindBoostTargets(visit).Count > 0;
	}

	public static ConvoDataNPCSelection MakeGoonBoostData(VisitState visit)
	{
		List<(Entity, float)> list = FindBoostTargets(visit);
		if (list.Count == 0)
		{
			return new ConvoDataNPCSelection();
		}
		ConvoDataNPCSelection.Entry item = MakeGoonBoost(visit.npc, list.FirstOrDefaultFast().Item1);
		return new ConvoDataNPCSelection(new List<ConvoDataNPCSelection.Entry> { item });
	}

	private static ConvoDataNPCSelection.Entry MakeGoonBoost(Entity owner, Entity other)
	{
		PlayerInfo playerInfo = other.components.agent?.GetPlayer();
		Label? label = playerInfo.ai.goon?.GoonType;
		if (!label.HasValue)
		{
			return new ConvoDataNPCSelection.Entry(other.Id, "", "", "");
		}
		string flavor = Loc.Get(Game.serv.globals.settings.npc.goons.FindSpecialGoonConfig(label.Value).locintro);
		string rel = IntroductionsUtils.MakeRelFlavor(owner, other, "convo.ticket-boost-flavor.relationship");
		string ooc = Loc.Get("convo.ticket-boost.goon.leader", "name", other.data.person.FullName, "groupname", playerInfo.social.FindPlayerGroupNameColorized());
		return new ConvoDataNPCSelection.Entry(other.Id, flavor, rel, ooc);
	}
}
