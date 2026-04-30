using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Setup;

public class PlayerSetup
{
	private class AreaCandidate
	{
		public Node node;

		public bool neargang;

		public bool neargoon;

		public float tmpscore;

		public AreaCandidate(Node node)
		{
			this.node = node;
			tmpscore = 0f;
		}

		internal static int CompareDescending(AreaCandidate a, AreaCandidate b)
		{
			float num = b.tmpscore - a.tmpscore;
			if (!(num > 0f))
			{
				if (!(num < 0f))
				{
					return 0;
				}
				return -1;
			}
			return 1;
		}
	}

	private class CrewCandidate
	{
		public Entity peep;

		public FamilyTree fam;

		public float tmpscore;

		public CrewCandidate(Entity peep)
		{
			this.peep = peep;
			fam = peep.components.person.FindFamilyTree();
			tmpscore = 0f;
		}

		internal static int CompareDescending(CrewCandidate a, CrewCandidate b)
		{
			float num = b.tmpscore - a.tmpscore;
			if (!(num > 0f))
			{
				if (!(num < 0f))
				{
					return 0;
				}
				return -1;
			}
			return 1;
		}
	}

	public struct SetupCtx
	{
		public readonly PlayerID pid;

		public readonly EntityConfig frontConfig;

		public readonly Label movesInto;

		public readonly VisitRequirementList reqsToAttachToBuilding;

		public readonly bool skipCopBldgs;

		public SetupCtx(PlayerID pid, EntityConfig frontConfig, bool skipCopBldgs)
		{
			this.pid = pid;
			this.frontConfig = frontConfig;
			movesInto = frontConfig?.biz.movesinto ?? Label.NULL;
			reqsToAttachToBuilding = frontConfig?.biz.reqsToAttachToBuilding;
			this.skipCopBldgs = skipCopBldgs;
		}
	}

	private Dictionary<NodeID, AreaCandidate> _startAreas;

	private List<CrewCandidate> _crewPeeps;

	private HashSet<Label> _usedEths;

	private static FloatMapper DISTANCE_SCORING_FN = new FloatMapper
	{
		x = { 0f, 10f, 20f, 50f, 100f },
		y = { 100f, 50f, 10f, 1f, 0.1f }
	};

	public IRandom rng { get; private set; }

	public PlayerSetup()
	{
		IEnumerable<Node> source = Game.ctx.board.nodes.GetAllNodesUnsafe().Where(PeopleTracker.IsNodeEligibleForStartPosition);
		SimTime now = Game.ctx.clock.Now;
		rng = Game.ctx.scenario.MakeSeededRng<PlayerSetup>();
		_usedEths = new HashSet<Label>();
		_crewPeeps = (from e in Game.ctx.simman.peoplegen.GetAllTrackedPeople()
			where PlayerSocial.IsEligibleCrewMember(now, e)
			select new CrewCandidate(e)).ToList();
		_startAreas = new Dictionary<NodeID, AreaCandidate>(new NodeIDEqualityComparer());
		source.ForEach(delegate(Node node)
		{
			_startAreas.Add(node.id, new AreaCandidate(node));
		});
	}

	public Entity GetAndRemoveCandidatePeep(IRandom rng, bool goons)
	{
		if (_crewPeeps.Count == 0)
		{
			return null;
		}
		ScoreAndSortCandidates(rng, goons);
		CrewCandidate crewCandidate = _crewPeeps.SwapRemoveAt(0);
		_usedEths.Add(crewCandidate.fam.eth);
		return crewCandidate.peep;
	}

	private void ScoreAndSortCandidates(IRandom rng, bool goons)
	{
		int i = 0;
		for (int count = _crewPeeps.Count; i < count; i++)
		{
			float num = (goons ? ScoreGoonPeep(_crewPeeps[i]) : ScoreGangPeep(_crewPeeps[i], _usedEths));
			float num2 = rng.Generate(0f, 0.1f);
			_crewPeeps[i].tmpscore = num + num2;
		}
		_crewPeeps.Sort(CrewCandidate.CompareDescending);
	}

	private float ScoreGangPeep(CrewCandidate c, HashSet<Label> used)
	{
		if (used.Contains(c.fam.eth))
		{
			return 0f;
		}
		return c.fam.anchor.ethscore;
	}

	private float ScoreGoonPeep(CrewCandidate c)
	{
		if (c.fam.anchor.ethscore == 0f)
		{
			return 0f;
		}
		return 1f;
	}

	public (bool success, PlayerStartData data) ScoreAndRemoveBestArea(PlayerInfo player, Entity peep, EntityConfig frontConfig, SetupCtx ctx)
	{
		List<AreaCandidate> list = _startAreas.Values.Where((AreaCandidate area) => SuitableFirstNode(area, ctx)).ToList();
		if (list.Count == 0)
		{
			return (success: false, data: null);
		}
		list.ForEach(delegate(AreaCandidate area)
		{
			area.tmpscore = ScoreAreaForThisPeep(player, area, peep);
		});
		list.Sort(AreaCandidate.CompareDescending);
		AreaCandidate areaCandidate = list.FirstOrDefaultFast();
		_startAreas.Remove(areaCandidate.node.id);
		MarkAreaNeighbors(player, areaCandidate);
		return (success: true, data: new PlayerStartData
		{
			node = areaCandidate.node,
			frontBiz = frontConfig
		});
	}

	private static bool SuitableFirstNode(AreaCandidate area, SetupCtx ctx)
	{
		if (area.node == null)
		{
			return false;
		}
		if (area.node.owner.IsSet)
		{
			return false;
		}
		if (area.node.known.IsAnySet)
		{
			return false;
		}
		if (!CreatePlayers.DoesNodeHaveBuildingToReplace(area.node, ctx))
		{
			return false;
		}
		return true;
	}

	private static float ScoreAreaForThisPeep(PlayerInfo player, AreaCandidate area, Entity peep)
	{
		float magnitude = (area.node.pos - peep.components.person.FindFamilyTree().anchor.pos).Magnitude;
		float num = DISTANCE_SCORING_FN.Eval(magnitude);
		if (!area.node.FindIfInsideSafeMargins())
		{
			num *= Game.ctx.session.mapconfig.playerStart.mapEdgePlacementPenalty;
		}
		if (area.neargang && player.IsGangOrGoon)
		{
			num *= 0.01f;
		}
		if (area.neargoon && player.IsJustGoon)
		{
			num *= 0.1f;
		}
		return num;
	}

	private void MarkAreaNeighbors(PlayerInfo player, AreaCandidate start)
	{
		if (player.IsJustGang || player.IsHuman)
		{
			foreach (Node item in Game.ctx.board.nodes.FindAndSortNodesInRadius(start.node.pos, 30f))
			{
				AreaCandidate areaCandidate = _startAreas.FindOrNull(item.id);
				if (areaCandidate != null)
				{
					areaCandidate.neargang = true;
				}
			}
		}
		if (!player.IsJustGoon)
		{
			return;
		}
		using ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate();
		start.node.FindAllNeighbors(pooledBlockList);
		foreach (Node item2 in pooledBlockList)
		{
			AreaCandidate areaCandidate2 = _startAreas.FindOrNull(item2.id);
			if (areaCandidate2 != null)
			{
				areaCandidate2.neargoon = true;
			}
		}
	}
}
