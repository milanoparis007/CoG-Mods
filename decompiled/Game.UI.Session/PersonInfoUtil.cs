using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Actions;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public static class PersonInfoUtil
{
	public struct Overview
	{
		public string name;

		public string workplace;

		public string eth;

		public string age;

		public string traitsShort;

		public string traitsLong;

		public Gender gender;

		internal Relationship relToHuman;

		internal string demandStateText;

		internal string levelups;

		internal Demand.State demandState;

		internal PlayerInfo bossPlayer;

		public string GetWorkplaceOrUnemployed()
		{
			return workplace ?? Loc.Get("ui.personinfo.unemployed");
		}

		public string GetRelAgeAndEth()
		{
			string text = Loc.RelationshipToYou(relToHuman?.type ?? RelationshipType.None, gender);
			return Loc.Get("ui.personinfo.infostring.short", "reltoyou", text, "age", age, "eth", eth);
		}

		public string GetAgeAndEth()
		{
			return Loc.Get("ui.personinfo.age-eth", "age", age, "eth", eth);
		}
	}

	public sealed class LevelupInfo
	{
		public XP.Levelup levelup;

		public LevelupChain chain;

		public string desc;

		public string details;

		public static LevelupInfo Make(XP.Levelup levelup, bool detailed)
		{
			LevelupInfo levelupInfo = new LevelupInfo
			{
				levelup = levelup,
				chain = levelup.FindLevelupChain()
			};
			levelupInfo.desc = levelupInfo.chain?.Describe(levelup.level) ?? "";
			levelupInfo.details = ((!detailed) ? "" : levelupInfo.chain?.GetDesc());
			return levelupInfo;
		}
	}

	public static Overview GenerateOverview(Entity peep, bool details)
	{
		PersonData person = peep.data.person;
		string eth = Loc.Get(person.GetEthDef().loc.adjEthnicity);
		string age = (person.IsAlive ? Loc.Get("ui.personinfo.age", "years", person.GetAge(Game.ctx.clock.Now).YearsInt) : Loc.Get("ui.personinfo.dead"));
		Entity entity = Game.ctx.players.Human.social.PlayerPeepId.FindEntity();
		Relationship orNull = Game.ctx.simman.rels.GetOrNull(entity.Id, peep.Id);
		Overview result = new Overview
		{
			name = GeneratePeepName(peep, showRank: true),
			gender = peep.data.person.g,
			workplace = GenerateEmploymentString(peep),
			eth = eth,
			age = age,
			relToHuman = orNull
		};
		if (details)
		{
			PlayerID pid = peep.data.agent.pid;
			Demand.Target target = (pid.IsAnyPlayer ? Demand.Target.MakeForPlayer(pid) : (peep.data.person.business.IsValid ? Demand.Target.MakeForBizOwner(peep) : Demand.Target.EMPTY));
			Demand.State item = Game.ctx.simman.demands.FindDemandState(PlayerID.HumanPlayer, target).state;
			string demandStateText = item switch
			{
				Demand.State.Compliant => Loc.Get("ui.personinfo.status.compliant"), 
				Demand.State.Defiant => Loc.Get("ui.personinfo.status.defiant"), 
				_ => "", 
			};
			result.bossPlayer = peep.data.agent?.pid.FindPlayer();
			result.demandState = item;
			result.demandStateText = demandStateText;
			result.traitsShort = GenerateTraitsList(peep, showDesc: true, showDescLong: false);
			result.traitsLong = GenerateTraitsList(peep, showDesc: true, showDescLong: true);
			result.levelups = GenerateLevelupDescription(peep, detailed: false);
		}
		return result;
	}

	public static string GenerateEmploymentString(Entity peep)
	{
		PlayerID pid = peep.data.agent.pid;
		if (!pid.IsAnyPlayer)
		{
			Entity entity = peep.data.person.business.FindEntity();
			if (entity == null)
			{
				bool flag = peep.components.person.IsOldEnoughToOwnBiz(Game.ctx.clock.Now);
				if (Game.ctx.simman.politics.GetPoliticianData(peep.Id) != null)
				{
					return Loc.Get("ui.personinfo.occ.politician", "ward", Game.ctx.simman.politics.GetWardForPolitician(peep.Id).WardName);
				}
				return Loc.Get(flag ? "ui.personinfo.occ.unk" : "ui.personinfo.occ.none");
			}
			string bizname = entity.data.biz.bizname;
			return Loc.Get("ui.personinfo.occ.info", "bizname", bizname);
		}
		PlayerInfo playerInfo = pid.FindPlayer();
		string text = playerInfo.social.FindPlayerGroupNameColorized();
		if (playerInfo.IsHuman)
		{
			return ((Game.ctx.players.Human.social.PlayerPeepId == peep.Id) ? (Loc.Get("ui.personinfo.selected.human") + " ") : (Loc.Get("ui.personinfo.selected.not-human") + " ")) + text;
		}
		if (playerInfo.IsCopOrFed)
		{
			return text;
		}
		bool flag2 = Game.ctx.players.Human.meetings.IsPlayerMet(pid);
		if (playerInfo.IsGangOrGoon && flag2)
		{
			string text2 = playerInfo.social.FindDemandTargetDescOrNull(PlayerID.HumanPlayer) ?? "";
			bool flag3 = peep.Id == playerInfo.social.PlayerPeepId;
			string text3 = (playerInfo.IsJustGoon ? Loc.Get("ui.personinfo.selected.goon") : (flag3 ? Loc.Get("ui.personinfo.selected.opp.boss") : Loc.Get("ui.personinfo.selected.opp.goon")));
			return Loc.Get("ui.personinfo.selected.compose", "prefix", text3, "groupname", text, "compliance", text2);
		}
		return Loc.Get("ui.personinfo.selected.unknown");
	}

	public static (Relationship rel, Fixnum pts, int tix) GetRelationshipAndTickets(EntityID peepId)
	{
		Relationship relationshipFromSourceToPlayer = Game.ctx.players.Human.social.GetRelationshipFromSourceToPlayer(peepId);
		if (relationshipFromSourceToPlayer == null)
		{
			return (rel: null, pts: 0, tix: 0);
		}
		bool num = Game.ctx.players.Human.crew.IsCrew(peepId);
		Relationship.Value value = relationshipFromSourceToPlayer.Evaluate();
		int item = ((!num) ? relationshipFromSourceToPlayer.GetTicketsAvailable() : 0);
		return (rel: relationshipFromSourceToPlayer, pts: value.current, tix: item);
	}

	public static void UpdateRelationshipBar(GameObject go, Entity peep)
	{
		if (peep == null)
		{
			Logger.Error("Peep in UpdateRelationshipBar is null");
		}
		(Relationship, Fixnum, int) relationshipAndTickets = GetRelationshipAndTickets(peep.Id);
		float num = (float)relationshipAndTickets.Item2;
		go.GetChild("Bar/Left Bar").SetUIElementWidth(MathUtil.Clamp(num * -1f, 0f, 50f));
		go.GetChild("Bar/Right Bar").SetUIElementWidth(MathUtil.Clamp(num * 1f, 0f, 50f));
		go.SetText("Bar/Number/Text", ((int)relationshipAndTickets.Item2).ToString());
		bool value = relationshipAndTickets.Item3 != 0 && !peep.data.person.HasResAssigned;
		go.SetActive("Tickets", value);
		string text = "";
		if (relationshipAndTickets.Item1 != null)
		{
			if (relationshipAndTickets.Item1.IsAffiliate())
			{
				text = text + Loc.Get("ui.affiliate") + " ";
			}
			if (relationshipAndTickets.Item3 != 0)
			{
				text += Loc.GetPluralized("ui.favors-avail", relationshipAndTickets.Item3, "num", relationshipAndTickets.Item3);
			}
		}
		go.SetText("Tickets/Text", text);
	}

	public static string GenerateRelationshipExplanation(Entity peep)
	{
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		if (peep == null)
		{
			return "";
		}
		var (relationship, value, num) = GetRelationshipAndTickets(peep.Id);
		if (relationship != null)
		{
			if (num > 0)
			{
				string pluralized = Loc.GetPluralized("ui.favors-avail", num, "num", num);
				string text = Loc.Get("ui.favors-accrued", "favors", pluralized);
				stringBuilder.AppendLine(text, 2);
			}
			if (relationship.IsAffiliate())
			{
				string text2 = Loc.FormatNumber(relationship.GetAffiliateThreshold());
				string text3 = Loc.Get("ui.affiliate") + " " + Loc.Get("ui.affiliate.desc", "level", text2);
				stringBuilder.AppendLine(text3, 2);
			}
			stringBuilder.AppendLine(Loc.Get("ui.personinfo.rel-exp.opinion", "rating", Loc.FormatNumberPlusMinus(value)));
			relationship.Explain(stringBuilder);
		}
		else
		{
			stringBuilder.AppendLine(Loc.Get("ui.personinfo.rel-exp.none"));
		}
		return stringBuilder.ToStringAndReturnToPool().TrimEnd();
	}

	public static string GeneratePeepName(Entity peep, bool showRank)
	{
		string fullName = peep.data.person.FullName;
		if (!(peep.data.agent.pid.IsHumanPlayer && showRank))
		{
			return fullName;
		}
		return peep.components.agent.WrapWithRankIcon(fullName);
	}

	public static (RelationshipType type, string text) GetRelationshipDetails(Entity from, Entity to)
	{
		RelationshipType typeOrNone = Game.ctx.simman.rels.GetTypeOrNone(from.Id, to.Id);
		string item = Loc.Relationship(typeOrNone, to.data.person.g);
		if (typeOrNone == RelationshipType.Self && from.data.agent != null && from.data.agent.IsInHumanCrew)
		{
			item = ((to.data.person.g == Gender.M) ? "reltype.self.human.m" : "reltype.self.human.s");
		}
		return (type: typeOrNone, text: item);
	}

	public static string GenerateTraitsList(Entity peep, bool showDesc, bool showDescLong)
	{
		string key = (showDesc ? "ui.personinfo.trait.details" : "ui.personinfo.trait");
		IEnumerable<string> values = peep.components.person.GetAllTraits().Select(delegate(Trait trait)
		{
			string locName = trait.GetLocName();
			string text = (showDescLong ? trait.GetLocDesc() : trait.GetLocShort());
			return Loc.Get(key, "traitname", locName, "traitdesc", text);
		});
		return Loc.Get("ui.personinfo.traits-header") + "\n" + string.Join("\n", values);
	}

	public static string GenerateTraitsIcons(Entity peep, string separator = " ")
	{
		IEnumerable<Trait> allTraits = peep.components.person.GetAllTraits();
		string text = string.Join(separator, allTraits.Select((Trait t) => t.GetLocIcon()));
		return Loc.Get("ui.personinfo.trait.icons", "traits", Loc.Get("ui.crewinspect.bio.traits"), "icons", text);
	}

	public static string GenerateGamblingTraitsList(Entity peep)
	{
		string key = "ui.personinfo.trait.details";
		IEnumerable<string> values = peep.components.person.GetAllTraits().Select(delegate(Trait trait)
		{
			string locName = trait.GetLocName();
			string locGambling = trait.GetLocGambling();
			return Loc.Get(key, "traitname", locName, "traitdesc", locGambling);
		});
		return Loc.Get("ui.personinfo.traits-header") + "\n" + string.Join("\n", values);
	}

	public static string GeneratePoliticalTraitsParagraph(Entity peep)
	{
		IEnumerable<string> values = from trait in peep.components.person.GetAllTraits()
			select trait.GetLocPolitics();
		return string.Join("", values);
	}

	public static string GenerateLevelupDescription(Entity peep, bool detailed)
	{
		List<LevelupInfo> list = (peep.components.agent?.GetLevelupsSortedOrNull())?.Select((XP.Levelup l) => LevelupInfo.Make(l, detailed)).ToList();
		if (list == null || list.Count == 0)
		{
			return Loc.Get("ui.personinfo.levelups-none");
		}
		string key = (detailed ? "ui.personinfo.levelups.details" : "ui.personinfo.levelups.oneline");
		return list.SelectToString((LevelupInfo info) => Loc.Get(key, "levelup", info.desc, "details", info.details), "\n");
	}

	public static string GenerateLevelupIcons(Entity peep, string separator = " ")
	{
		List<string> list = (from icon in (peep.components.agent?.GetLevelupsSortedOrNull())?.Select((XP.Levelup l) => l.FindLevelupChain()?.GetIcon())
			where icon != null
			select icon).ToList();
		if (list == null || list.Count == 0)
		{
			return Loc.Get("ui.personinfo.levelups.icons.0");
		}
		return Loc.Get("ui.personinfo.levelups.icons", "icons", string.Join(separator, list));
	}

	public static (bool active, string result) FindActionString(Entity peep, Entity vehicle)
	{
		Command command = Game.ctx.players.Human.commands.FindCommand(peep.Id, onlyActive: false);
		if (command != null)
		{
			return (active: true, result: command.Message);
		}
		if (vehicle?.components.script.queue == null)
		{
			return (active: false, result: "");
		}
		GameScriptType? gameScriptType = vehicle.components.script.queue.FirstOrDefault()?.data.type;
		if (!gameScriptType.HasValue)
		{
			return (active: false, result: Loc.Get("ui.personinfo.action-string.idle"));
		}
		if (gameScriptType.HasValue && gameScriptType == GameScriptType.CarNavigation)
		{
			return (active: true, result: Loc.Get("ui.personinfo.action-string.driving"));
		}
		return (active: true, result: Loc.Get("ui.personinfo.action-string.busy"));
	}

	public static void TweenCameraToCrew(CrewAssignment crew, bool showFx)
	{
		TweenCameraToEntity(crew.targetId.FindEntity(), showFx);
	}

	public static void TweenCameraToEntity(EntityID entityId, bool showFx = true)
	{
		TweenCameraToEntity(entityId.FindEntity(), showFx);
	}

	public static void TweenCameraToEntity(Entity entity, bool showFx = true)
	{
		TweenCameraToPos(BoardUtil.FindBoardPositionFor(entity), showFx);
	}

	public static void TweenCameraToNode(NodeID nodeId, bool showFx = true)
	{
		TweenCameraToPos(nodeId.FindNode().pos, showFx);
	}

	public static void TweenCameraToPos(WorldPos? pos, bool showFx)
	{
		if (pos.HasValue)
		{
			HUDUtil.GoTo(pos.Value, zoomIn: false, showFx);
		}
	}
}
