using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public static class RaidChecker
{
	public struct RaidPossibility
	{
		public static readonly RaidPossibility NONE;

		public bool heatAboveRaidThreshold;

		public bool heatNearOrAboveRaidThreshold;

		public bool recentlyRaided;

		public Fixnum currentHeat;

		public Fixnum goalHeat;
	}

	private const int MAX_NODES = int.MaxValue;

	public static PoliceSettings Settings => Game.serv.globals.settings.people.social.police;

	public static RaidTarget FindRaidTarget(PrecinctAdvisor advisor, PrecinctAdvisorData data)
	{
		EntityID stationId = data.stationId;
		_ = data.precinctId;
		stationId.FindEntity().components.board.GetNode();
		HeatCheck heatCheck = default(HeatCheck);
		using (ListPool<HeatCheck>.PooledBlockList pooledBlockList = ListPool<HeatCheck>.Allocate())
		{
			foreach (NodeID item in advisor.EnumerateAllBeatNodesInPrecinct())
			{
				CheckNodeForRaids(advisor, item.FindNode(), pooledBlockList);
			}
			if (pooledBlockList.Count == 0)
			{
				return RaidTarget.INVALID;
			}
			if (pooledBlockList.Count > 1)
			{
				pooledBlockList.StableSort((HeatCheck a, HeatCheck b) => a.val.CompareTo(b.val));
			}
			heatCheck = pooledBlockList.FirstOrDefaultFast();
		}
		ModQuery query = new ModQuery(heatCheck.pid, heatCheck.nid);
		float probability = (float)Settings.raidChance.Evaluate(query);
		if (!data.rng.CheckProbability(probability))
		{
			return RaidTarget.INVALID;
		}
		if (Game.ctx.tutorial.ArePoliceRaidsSuppressed)
		{
			return RaidTarget.INVALID;
		}
		int days = data.rng.Generate(Settings.raidDurationDayz);
		return new RaidTarget(heatCheck.nid, SimTimeSpan.FromDays(days));
	}

	private static void CheckNodeForRaids(PrecinctAdvisor advisor, Node node, ICollection<HeatCheck> candidates)
	{
		NodeRaidInfo raidOrNull = node.GetRaidOrNull();
		if (raidOrNull != null && raidOrNull.WasRecentlyRaided())
		{
			return;
		}
		PlayerID payer = node.owner.Get();
		if (payer.IsAnyPlayer && (advisor.HasDonationFrom(payer) == DonationState.PaidOff || advisor.HasDonationFrom(payer) == DonationState.WaitingForRefresh))
		{
			return;
		}
		foreach (Heat datum in node.heat.data)
		{
			if (CalculateRaidPossibility(node, datum).heatAboveRaidThreshold)
			{
				candidates.Add(new HeatCheck(datum.pid, node.id, datum.current));
			}
		}
	}

	public static RaidPossibility CalculateRaidPossibility(Node node, PlayerID pid)
	{
		return CalculateRaidPossibility(node, node.heat.GetOrNull(pid));
	}

	private static RaidPossibility CalculateRaidPossibility(Node node, Heat nodeheat)
	{
		if (nodeheat == null)
		{
			return RaidPossibility.NONE;
		}
		if (!nodeheat.pid.IsHumanPlayer)
		{
			return RaidPossibility.NONE;
		}
		if (!node.known.Get(nodeheat.pid))
		{
			return RaidPossibility.NONE;
		}
		RaidPossibility result = new RaidPossibility
		{
			currentHeat = nodeheat.current,
			goalHeat = nodeheat.goal
		};
		result.recentlyRaided = node.GetRaidOrNull()?.WasRecentlyRaided() ?? false;
		Fixnum fixnum = Settings.raidThreshold.Evaluate(new ModQuery(nodeheat.pid, node));
		result.heatAboveRaidThreshold = nodeheat.current >= fixnum;
		result.heatNearOrAboveRaidThreshold = nodeheat.current >= fixnum / 2;
		return result;
	}
}
