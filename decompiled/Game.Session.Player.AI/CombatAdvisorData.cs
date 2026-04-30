using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class CombatAdvisorData : BaseAdvisorData
{
	public class AggroEntry
	{
		public PlayerID pid;

		public SimTime started;

		public Fixnum lastScore;

		public bool hasTruce;

		public SimTime truceEnd;

		public SimTime nextTruceAskTime;

		public SimTime nextJointWarAskTime;

		public AggroEntry()
		{
		}

		public AggroEntry(PlayerID pid, SimTime started, Fixnum lastScore)
		{
			this.pid = pid;
			this.started = started;
			this.lastScore = lastScore;
			hasTruce = false;
			truceEnd = SimTime.MIN_DATE;
			nextTruceAskTime = SimTime.MIN_DATE;
			nextJointWarAskTime = SimTime.MIN_DATE;
		}
	}

	public class JointWarEntry
	{
		public PlayerID requested;

		public PlayerID target;

		public SimTime started;

		public SimTime agreedEnd;

		public bool fulfilled;

		public JointWarEntry()
		{
		}

		public JointWarEntry(PlayerID requested, PlayerID target, SimTime agreedEnd)
		{
			this.requested = requested;
			this.target = target;
			started = Game.ctx.clock.Now;
			this.agreedEnd = agreedEnd;
			fulfilled = false;
		}
	}

	public class HitmanEntry
	{
		public PlayerID employer;

		public PlayerID target;

		public SimTime started;

		public Fixnum aggroRelBonus;
	}

	public List<AggroEntry> aggro = new List<AggroEntry>();

	public List<JointWarEntry> jointWars = new List<JointWarEntry>();

	public HitmanEntry hitman;

	public int IndexOf(PlayerID pid)
	{
		int i = 0;
		for (int count = aggro.Count; i < count; i++)
		{
			if (aggro[i].pid == pid)
			{
				return i;
			}
		}
		return -1;
	}

	public AggroEntry GetAggroOrNull(PlayerID pid)
	{
		return aggro.GetOrDefaultFast(IndexOf(pid));
	}

	public bool HasAggro(PlayerID pid)
	{
		return IndexOf(pid) >= 0;
	}

	public bool HasAggroAny()
	{
		return aggro.Count > 0;
	}

	public void AddAggroHelper(PlayerID pid, Fixnum score)
	{
		aggro.Add(new AggroEntry(pid, Game.ctx.clock.Now, score));
	}

	public bool RemoveAggroHelper(PlayerID pid)
	{
		int num = IndexOf(pid);
		if (num >= 0)
		{
			aggro.RemoveAt(num);
		}
		return num >= 0;
	}

	public void RemoveAllAggro()
	{
		aggro.Clear();
	}

	public void UpdateTruce(PlayerID pid, bool hasTruce, SimTime? truceEnd)
	{
		AggroEntry aggroOrNull = GetAggroOrNull(pid);
		if (aggroOrNull != null)
		{
			aggroOrNull.hasTruce = hasTruce;
			aggroOrNull.truceEnd = ((hasTruce && truceEnd.HasValue) ? truceEnd.Value : SimTime.MIN_DATE);
		}
	}

	public void UpdateAggroScore(PlayerID pid, Fixnum score)
	{
		AggroEntry aggroOrNull = GetAggroOrNull(pid);
		if (aggroOrNull != null)
		{
			aggroOrNull.lastScore = score;
		}
	}

	public void UpdateTruceNextAsk(PlayerID pid, SimTime time)
	{
		AggroEntry aggroOrNull = GetAggroOrNull(pid);
		if (aggroOrNull != null)
		{
			aggroOrNull.nextTruceAskTime = time;
		}
	}
}
