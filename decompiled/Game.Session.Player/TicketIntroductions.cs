using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Convo;
using SomaSim.Util;

namespace Game.Session.Player;

public static class TicketIntroductions
{
	private struct IntroResults
	{
		public Label label;

		public IBizModule mod;

		public bool consumedByPlayer;

		public bool Valid => label.IsSet;

		public bool ModuleBased => mod != null;
	}

	private const string CHECK_INTRO_REASON = "checkIntroReason";

	public static ConvoDataNPCSelection MakeBizIntroData(VisitState visit)
	{
		Entity npc = visit.npc;
		int num = Game.serv.globals.settings.people.social.intros.countToIntroduce;
		List<Entity> list = Game.ctx.players.Human.social.TicketActionFindIntroTargets(visit);
		IntroductionType randomIntroductionType = Game.ctx.players.Human.social.GetRandomIntroductionType(visit);
		List<ConvoDataNPCSelection.Entry> list2 = new List<ConvoDataNPCSelection.Entry>();
		if (randomIntroductionType != IntroductionType.LocalFriend)
		{
			foreach (Entity item2 in list)
			{
				IntroResults results = FindIntroReason(item2, randomIntroductionType);
				if (results.Valid)
				{
					ConvoDataNPCSelection.Entry item = ((randomIntroductionType == IntroductionType.Business) ? MakeBizIntroData(results, npc, item2) : MakeTransactionIntro(results, npc, item2));
					list2.Add(item);
					if (--num == 0)
					{
						break;
					}
				}
			}
		}
		for (int i = 0; i < num; i++)
		{
			if (list.Count <= 0)
			{
				break;
			}
			Entity target = list.RemoveAndReturn(0);
			if (!list2.Any((ConvoDataNPCSelection.Entry e) => e.targetId == target.Id))
			{
				list2.Add(MakeLocalIntro(npc, target));
			}
		}
		return new ConvoDataNPCSelection(list2);
	}

	private static IntroResults FindIntroReason(Entity target, IntroductionType introtype)
	{
		Game.ctx.botl.entity.Value.SetReference(target);
		Game.ctx.botl.arg3.Value.SetGeneral(introtype);
		Game.ctx.botl.IsTrue("checkIntroReason");
		Label label = Label.NULL;
		bool consumedByPlayer = false;
		IBizModule mod = null;
		if (Game.ctx.botl.label.Value.reference is Label { IsSet: not false } label2)
		{
			label = label2;
			consumedByPlayer = Game.ctx.botl.arg1.Value.boolean;
			mod = ((Game.ctx.botl.business.Value.reference is IBizModule bizModule) ? bizModule : null);
		}
		return new IntroResults
		{
			label = label,
			consumedByPlayer = consumedByPlayer,
			mod = mod
		};
	}

	private static ConvoDataNPCSelection.Entry MakeLocalIntro(Entity owner, Entity other)
	{
		string flavor = Loc.Get("convo.ticket-intro-flavor.generic");
		string rel = IntroductionsUtils.MakeRelFlavor(owner, other, "convo.ticket-intro-flavor.relationship");
		return new ConvoDataNPCSelection.Entry(other.Id, flavor, rel, "");
	}

	private static ConvoDataNPCSelection.Entry MakeBizIntroData(IntroResults results, Entity owner, Entity other)
	{
		string flavor = Loc.Get(results.mod.ModuleConfig.Common.display.locintroflavor);
		string rel = IntroductionsUtils.MakeRelFlavor(owner, other, "convo.ticket-intro-flavor.relationship");
		string ooc = MakeOutOfCharacterExplanation(other, results.consumedByPlayer, results.label);
		return new ConvoDataNPCSelection.Entry(other.Id, flavor, rel, ooc);
	}

	private static ConvoDataNPCSelection.Entry MakeTransactionIntro(IntroResults results, Entity owner, Entity other)
	{
		string text = Loc.Get(Game.ctx.simman.FindResource(results.label).locname);
		string flavor = Loc.Get("convo.ticket-intro-flavor.transaction.intro-flavor", "resource", text);
		string rel = IntroductionsUtils.MakeRelFlavor(owner, other, "convo.ticket-intro-flavor.relationship");
		string ooc = MakeOutOfCharacterExplanation(other, results.consumedByPlayer, results.label);
		return new ConvoDataNPCSelection.Entry(other.Id, flavor, rel, ooc);
	}

	private static string MakeOutOfCharacterExplanation(Entity peep, bool playerbuying, Label resourcelabel)
	{
		if (resourcelabel.IsNotSet)
		{
			return "";
		}
		string peepFirstName = NameUtils.GetPeepFirstName(peep);
		string text = Loc.Get(playerbuying ? "convo.ticket-intro-flavor.sells" : "convo.ticket-intro-flavor.buys");
		string text2 = Loc.Get(Game.ctx.simman.FindResource(resourcelabel).locname);
		return Loc.Get("convo.ticket-intro-flavor.justification", "name", peepFirstName, "buysorsells", text, "resource", text2);
	}

	public static void PerformIntro(EntityID targetId, EntityID crewpeep)
	{
		PerformIntro(targetId, crewpeep, BuffConstants.TICKET_INTRO, null);
	}

	public static void PerformGoonBoostSocialAction(EntityID targetId, EntityID crewpeep)
	{
		PerformIntro(targetId, crewpeep, null, SocialConstants.GOT_RELBOOST_FROM_NPC);
	}

	private static void PerformIntro(EntityID targetId, EntityID crewpeep, Label? buff, Label? socialAction)
	{
		PlayerSocial social = Game.ctx.players.Human.social;
		PlayerMeetings meetings = Game.ctx.players.Human.meetings;
		social.DiscoverSomeoneAndTheirWorkplace(targetId);
		PlayerID pid = targetId.FindEntity().data.agent.pid;
		if (pid.IsAIPlayer && !meetings.IsPlayerMet(pid))
		{
			meetings.MarkPlayersAsMutuallyMet(pid, introduceLeadersToCrew: true);
		}
		if (buff.HasValue)
		{
			social.GetRelationshipFromSourceToPlayer(targetId).AddBuff(buff.Value, crewpeep);
		}
		if (socialAction.HasValue)
		{
			social.PerformSocialActionOn(socialAction.Value, targetId, crewpeep, Extend);
		}
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.PlayerGotIntroImmediate, targetId, social.PID));
		PersonInfoUtil.TweenCameraToEntity(targetId);
		string peepFullName = NameUtils.GetPeepFullName(targetId);
		string message = Loc.Get("convo.select.intro.result", "name", peepFullName);
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.SOCIAL, TickerTitle.DEFAULT, message, targetId);
		Game.ctx.sfx.PlayIntroduction();
		HistoryLedgerItem Extend(HistoryLedgerItem info)
		{
			info.actor = crewpeep;
			info.node = (crewpeep.FindEntity()?.data?.agent?.nid).GetValueOrDefault();
			return info;
		}
	}
}
