using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Data;

public static class NameUtils
{
	public const string NAME_PATTERN_HUMAN = "pattern-gang";

	public const string NAME_PATTERN_GANGS = "pattern-gang";

	public const string NAME_PATTERN_GOONS = "pattern-loafers";

	public const string NAME_PATTERN_COPS = "pattern-cops";

	public const string NAME_PATTERN_FEDS = "pattern-feds";

	public static string LocWithContext(string key, Entity peep, params object[] replacements)
	{
		return Loc.Get(key, MakeLocContext(peep, replacements));
	}

	public static string LocForNewGame(string key, IRandom rng, PeepCreationDetails deets, params object[] replacements)
	{
		return Loc.Get(key, MakeLocContext(rng, deets, replacements));
	}

	public static string LocWithContext(string key, IRandom rng, Entity peep, params object[] replacements)
	{
		return Loc.Get(key, MakeLocContext(rng, peep, replacements));
	}

	public static LocReplacementContext MakeLocContext(Entity peep, params object[] replacements)
	{
		return MakeLocContext(peep.components.ident.GetIdentityRNGUnchanging(), peep, replacements);
	}

	public static LocPersonReplacements MakeLocPerson(Entity peep)
	{
		PersonData person = peep.data.person;
		return new LocPersonReplacements
		{
			first = person?.first,
			last = person?.last,
			ethid = (person?.eth ?? Label.NULL)
		};
	}

	public static LocPersonReplacements MakeLocPerson(PeepCreationDetails deets)
	{
		return new LocPersonReplacements
		{
			first = deets.fname,
			last = deets.lname,
			ethid = deets.ethnicity
		};
	}

	public static LocReplacementContext MakeLocContext(IRandom rng, Entity peep, params object[] replacements)
	{
		return new LocReplacementContext
		{
			rng = rng,
			replacements = replacements,
			person = MakeLocPerson(peep)
		};
	}

	public static LocReplacementContext MakeLocContext(IRandom rng, PeepCreationDetails deets, params object[] replacements)
	{
		return new LocReplacementContext
		{
			rng = rng,
			replacements = replacements,
			person = MakeLocPerson(deets)
		};
	}

	public static LocReplacementContext MakeLocContextForBizName(IRandom rng, Label eth, params object[] replacements)
	{
		string forcedlang = (Game.serv.loc.IsCurrentLanguageNonLatin ? "en" : null);
		EthnicityDef ethnicityDef = Game.serv.globals.settings.ethnicities.FindEthnicityDef(eth);
		Gender gender = (rng.CoinFlip() ? Gender.M : Gender.F);
		LocReplacementContext ctx = new LocReplacementContext(rng, null, null, forcedlang);
		LocPersonReplacements value = new LocPersonReplacements
		{
			first = ethnicityDef.loc.GetRandomFirstName(ctx, gender),
			last = ethnicityDef.loc.GetRandomLastName(ctx, gender),
			ethid = eth
		};
		return new LocReplacementContext
		{
			rng = rng,
			replacements = replacements,
			person = value,
			forcedlang = forcedlang
		};
	}

	public static string FindNamePattern(PlayerType type)
	{
		switch (type)
		{
		case PlayerType.HumanPlayer:
			return "pattern-gang";
		case PlayerType.GangPlayer:
			return "pattern-gang";
		case PlayerType.GoonPlayer:
			return "pattern-loafers";
		case PlayerType.CopPlayer:
			return "pattern-cops";
		case PlayerType.AgentPlayer:
			return "pattern-feds";
		default:
			Logger.Warning("Unknown npc type: " + type);
			return "pattern-loafers";
		}
	}

	public static string MakeHumanGroupName(IRandom rng, PeepCreationDetails deets)
	{
		return LocForNewGame(FindNamePattern(PlayerType.HumanPlayer), rng, deets);
	}

	public static string GetPeepFullName(Entity e)
	{
		return e.data.person.FullName;
	}

	public static string GetPeepFullName(EntityID eid)
	{
		return eid.FindEntity().data.person.FullName;
	}

	public static string GetPeepFirstName(EntityID eid)
	{
		return eid.FindEntity().data.person.FirstName;
	}

	public static string GetPeepFirstName(Entity e)
	{
		return e.data.person.FirstName;
	}

	public static string GetPeepLastName(EntityID eid)
	{
		return eid.FindEntity().data.person.LastName;
	}

	public static string GetGroupOrPeepName(EntityID eid, bool forceName = false)
	{
		return GetGroupOrPeepName(eid.FindEntity(), forceName);
	}

	public static string GetGroupOrPeepName(Entity e, bool forceName = false)
	{
		if (forceName)
		{
			return GetPeepFullName(e);
		}
		PlayerID pid = e.data.agent.pid;
		if (!pid.IsAnyPlayer)
		{
			return GetPeepFullName(e);
		}
		return pid.FindPlayer().social.PlayerGroupName;
	}

	internal static (string name, string relstr) GetNpcNameAndRelationshipToPlayer(Entity npc, PlayerID pid)
	{
		string item = Loc.RelationshipToYou(pid.FindPlayer().social.GetRelationshipFromPlayerTo(npc.Id)?.type ?? RelationshipType.None, npc.data.person.g);
		return (name: npc.data.person.FullName, relstr: item);
	}

	internal static void MaybePrintDebugInfoAboutPeep(Entity peep)
	{
		if (Game.settings.DoEnableEntityLogging && peep.data.agent != null)
		{
			PlayerInfo playerInfo = peep.data.agent.pid.FindPlayer();
			if (playerInfo != null && playerInfo.PID.IsAnyPlayer)
			{
				string.Concat($" ... Member of {playerInfo.PID} / {playerInfo.social.FindPlayerGroupNameColorized()}" + " / AG:[" + playerInfo.ai?.combat?.DebugGetAggroScores() + "]", $" {playerInfo.ai?.goon?.GoonType} {playerInfo.ai?.Data.personality}");
			}
		}
	}
}
