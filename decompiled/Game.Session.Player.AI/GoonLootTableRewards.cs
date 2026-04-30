using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class GoonLootTableRewards
{
	public GoonLootFSM status;

	public GoonRewardInfo active;

	public PlayerID pid;

	private bool StateIs(GoonLootState state)
	{
		return status.currentstate == state;
	}

	public GoonLootTableRewards()
	{
	}

	public GoonLootTableRewards(PlayerID pid)
	{
		this.pid = pid;
	}

	public void CheckRelationshipRewards()
	{
		if (!(Game.ctx.clock.CurrentPlayer != pid))
		{
			CheckThreshold();
			if (!StateIs(GoonLootState.Disabled))
			{
				AdvanceLootFSM();
			}
		}
	}

	private void CheckThreshold()
	{
		Fixnum fixnum = pid.FindPlayer().social.EvaluateRelationshipFromPlayerTo(PlayerID.HumanPlayer);
		Fixnum fixnum2 = Game.serv.globals.settings.npc.goons.relToStartLoot.Evaluate(pid);
		Fixnum fixnum3 = Game.serv.globals.settings.npc.goons.relToStopLoot.Evaluate(pid);
		if (fixnum < fixnum3 && !StateIs(GoonLootState.Disabled))
		{
			ExitFSM();
		}
		else if (fixnum >= fixnum2 && StateIs(GoonLootState.Disabled))
		{
			EnterFSM();
		}
	}

	private void EnterFSM()
	{
		if (status.currentstate == GoonLootState.Disabled)
		{
			active = PickNewReward(pid);
			status = status.TransitionToCoolDown();
		}
	}

	private void ExitFSM()
	{
		if (status.currentstate != GoonLootState.Disabled)
		{
			ClearRewardForIdle();
			status = status.TransitionToIdle();
		}
	}

	private void ClearRewardForIdle()
	{
		if (!active.rewardid.IsNotSet)
		{
			PostTicker(pid, TickerIcon.LOOT_RETRACTED, TickerTitle.LOOT_RETRACTED, "ui.tickers.loot.retracted.below-threshold.message");
			active = GoonRewardInfo.NONE;
		}
	}

	private void AdvanceLootFSM()
	{
		SimTime now = Game.ctx.clock.Now;
		switch (status.currentstate)
		{
		case GoonLootState.Cooldown:
			CheckCooldown(now);
			break;
		case GoonLootState.Offered:
			CheckExpiration(now);
			break;
		case GoonLootState.Accepted:
			CheckReady(now);
			break;
		case GoonLootState.Raided:
			active = PickNewReward(pid);
			PostTicker(pid, TickerIcon.LOOT_RETRACTED, TickerTitle.LOOT_RETRACTED, "ui.tickers.loot.retracted.raided.message");
			status = status.TransitionToCoolDown();
			break;
		case GoonLootState.Disabled:
		case GoonLootState.Ready:
		case GoonLootState.Invalid:
			break;
		}
	}

	private void CheckCooldown(SimTime now)
	{
		if (!(now < active.cooldown))
		{
			if (active.rewardid == AIConstants.LOOT_ENTRY_NONE)
			{
				active = PickNewReward(pid);
				status = status.TransitionToCooldownFromEmpty();
			}
			else
			{
				active = active.MakeIntoOffered(pid);
				PostTicker(pid, TickerIcon.LOOT_AVAILABLE, TickerTitle.LOOT_AVAILABLE, "ui.tickers.loot.available.message");
				status = status.TransitionToOffered();
			}
		}
	}

	private void CheckExpiration(SimTime now)
	{
		if (!(now < active.expiration))
		{
			active = PickNewReward(pid);
			PostTicker(pid, TickerIcon.LOOT_RETRACTED, TickerTitle.LOOT_RETRACTED, "ui.tickers.loot.retracted.expired.message");
			status = status.TransitionToCoolDown();
		}
	}

	private void CheckReady(SimTime now)
	{
		if (!(now < active.ready))
		{
			PostTicker(pid, TickerIcon.LOOT_READY, TickerTitle.LOOT_READY, "ui.tickers.loot.ready.message");
			Game.ctx.sfx.PlayGoonQuestReady();
			status = status.TransitionToReady();
		}
	}

	private static GoonRewardInfo PickNewReward(PlayerID pid)
	{
		PlayerInfo playerInfo = pid.FindPlayer();
		CrewAssignment crewForPlayerPeep = playerInfo.crew.GetCrewForPlayerPeep();
		Entity peep = crewForPlayerPeep.GetPeep();
		BuildingAndBusinessData bbdata = BuildingUtil.MakeDataForBuildingAndOwner(playerInfo.territory.Safehouse.FindEntity(), peep);
		VisitState visit = new VisitState(crewForPlayerPeep, bbdata, Game.ctx.clock.Now, pid);
		List<GoonLootTableEntry> list = (from e in Game.serv.globals.settings.npc.goons.FindLootTable(playerInfo.ai.goon.GoonType)
			where e.visreqs.AllPass(visit)
			select e).ToList();
		List<float> weights = list.SelectIntoNewList((GoonLootTableEntry e) => (float)e.selectionweight.Evaluate(pid));
		GoonLootTableEntry config = playerInfo.ai.Data.goon.rng.PickElement(list, weights);
		return GoonRewardInfo.MakeReward(playerInfo, config);
	}

	private static void PostTicker(PlayerID pid, TickerIcon icon, TickerTitle title, string lockey)
	{
		EntityID playerPeepId = pid.FindPlayer().social.PlayerPeepId;
		if (!pid.FindPlayer().crew.IsCrewDefeated)
		{
			Game.ctx.hud.tickers.AddTextTicker(icon, title, Loc.Get(lockey), new TickerTarget
			{
				entityId = playerPeepId
			}, TickerPersistType.GoonOfferPersist);
		}
	}

	public void AcceptOfferedReward()
	{
		if (StateIs(GoonLootState.Offered))
		{
			active = active.MakeIntoAccepted(pid);
			status = status.TransitionToAccepted();
		}
	}

	public void CollectReward()
	{
		if (StateIs(GoonLootState.Ready))
		{
			active = PickNewReward(pid);
			status = status.TransitionToCoolDown();
		}
	}

	public void RaidGoonsLootReward()
	{
		if (StateIs(GoonLootState.Accepted) || StateIs(GoonLootState.Ready))
		{
			active = GoonRewardInfo.NONE;
			status = status.TransitionToRaided();
		}
	}
}
