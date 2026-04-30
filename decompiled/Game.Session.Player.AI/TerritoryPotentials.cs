using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Player.AI;

internal class TerritoryPotentials
{
	[DebuggerDisplay("{DebugString}")]
	internal sealed class Entry
	{
		public Node node;

		public Label eth;

		public int interestingBiz;

		public bool isMine;

		public bool isEnemy;

		public bool isNone;

		public PlayerID nearestEnemy;

		public bool nextToAnyOutpost;

		public int distToMine = int.MaxValue;

		public int distToEnemy = int.MaxValue;

		public float tmpscore;

		private string DebugOwner
		{
			get
			{
				if (!isMine)
				{
					if (!isEnemy)
					{
						return "none";
					}
					return "enemy";
				}
				return "mine";
			}
		}

		private string DebugDist
		{
			get
			{
				if (distToMine == int.MaxValue)
				{
					if (distToEnemy == int.MaxValue)
					{
						return "";
					}
					return $"toenemy:{distToEnemy}";
				}
				return $"tomine:{distToMine}";
			}
		}

		private string DebugString => $"{node.id} ({tmpscore}) int#={interestingBiz}, {DebugOwner} {DebugDist}";
	}

	private PlayerID _pid;

	private PlayerInfo _player;

	private TerritoryAdvisorConfig _def;

	private Dictionary<Node, Entry> _entries;

	private List<Entry> _sorted;

	private List<Node> _tmp_neighbors = new List<Node>(4);

	public void Initialize(PlayerID pid, TerritoryAdvisorConfig def)
	{
		_pid = pid;
		_player = pid.FindPlayer();
		_def = def;
	}

	public void Release()
	{
		_entries?.Clear();
	}

	public NodeID PickBestNode()
	{
		Recalculate();
		_sorted.AddRange(_entries.Values);
		_sorted.Sort((Entry a, Entry b) => Math.Sign(b.tmpscore - a.tmpscore));
		Entry entry = _sorted.FirstOrDefaultFast();
		_sorted.Clear();
		return entry?.node.id ?? NodeID.INVALID;
	}

	private void Recalculate()
	{
		if (_entries == null)
		{
			_entries = new Dictionary<Node, Entry>();
			Game.ctx.board.nodes.VisitNeighborhoodBFS(_pid.FindPlayer().territory.GetHeadquartersNode(), int.MaxValue, MakeNodeEntry, null, null, null, onlyBizNodes: true);
			_sorted = new List<Entry>(_entries.Count);
		}
		foreach (Entry value in _entries.Values)
		{
			MarkOwnership(value);
		}
		foreach (Entry value2 in _entries.Values)
		{
			MarkOutpostProximity(value2);
		}
		foreach (Entry value3 in _entries.Values)
		{
			MarkBorderProximity(value3, 1);
		}
		foreach (Entry value4 in _entries.Values)
		{
			MarkBorderProximity(value4, 2);
		}
		foreach (Entry value5 in _entries.Values)
		{
			MarkBorderProximity(value5, 3);
		}
		foreach (Entry value6 in _entries.Values)
		{
			Score(value6);
		}
	}

	private void MakeNodeEntry(Node node)
	{
		_entries.Add(node, new Entry
		{
			node = node,
			interestingBiz = node.interesting.Count,
			eth = node.maineth
		});
	}

	private void MarkOwnership(Entry entry)
	{
		bool isSet = entry.node.owner.IsSet;
		bool flag = entry.node.owner.Is(_pid);
		entry.isMine = isSet && flag;
		entry.isEnemy = isSet && !flag;
		entry.isNone = !isSet;
		entry.distToEnemy = ((!entry.isEnemy) ? int.MaxValue : 0);
		entry.nearestEnemy = (entry.isEnemy ? entry.node.owner.pid : PlayerID.INVALID);
		entry.distToMine = ((!entry.isMine) ? int.MaxValue : 0);
	}

	private void MarkOutpostProximity(Entry entry)
	{
		entry.node.FindAllNeighbors(_tmp_neighbors);
		foreach (Node tmp_neighbor in _tmp_neighbors)
		{
			PlayerID pid = tmp_neighbor.owner.Get();
			if (!entry.nextToAnyOutpost && pid.IsAnyPlayer)
			{
				entry.nextToAnyOutpost |= pid.FindPlayer().outposts.GetOutpostAtNode(tmp_neighbor).IsValid;
			}
		}
	}

	private void MarkBorderProximity(Entry entry, int pass)
	{
		entry.node.FindAllNeighbors(_tmp_neighbors);
		foreach (Node tmp_neighbor in _tmp_neighbors)
		{
			Entry entry2 = _entries.FindOrNull(tmp_neighbor);
			if (entry2 != null)
			{
				if (entry2.distToMine == pass - 1 && entry.distToMine > entry2.distToMine)
				{
					entry.distToMine = entry2.distToMine + 1;
				}
				if (entry2.distToEnemy == pass - 1 && entry.distToEnemy > entry2.distToEnemy)
				{
					entry.distToEnemy = entry2.distToEnemy + 1;
					entry.nearestEnemy = entry2.nearestEnemy;
				}
			}
		}
	}

	private void Score(Entry entry)
	{
		ModQuery query = new ModQuery(_pid, entry.node);
		Fixnum fixnum = FindScore(entry, query);
		entry.tmpscore = (float)fixnum;
	}

	private Fixnum FindScore(Entry entry, ModQuery query)
	{
		if (entry.distToMine > 2)
		{
			return 0;
		}
		if (entry.interestingBiz == 0)
		{
			return 0;
		}
		if (_player.ai.territory.HasExpansionTreatyWith(entry.nearestEnemy) && entry.distToEnemy <= 3)
		{
			return 0;
		}
		Fixnum fixnum = _def.growth.basePerNode.Evaluate(query);
		Fixnum fixnum2 = 0;
		if (entry.node.maineth == _player.social.PlayerEthnicity)
		{
			fixnum2 = _def.growth.ethBonusPerNode.Evaluate(query);
		}
		return fixnum + fixnum2;
	}
}
