using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Player.AI;

public sealed class TerritoryAdvisorData : BaseAdvisorData
{
	public class NonExpansionTreaty
	{
		public PlayerID pid;

		public SimTime endOfTreaty;

		public NonExpansionTreaty()
		{
		}

		public NonExpansionTreaty(PlayerID pid, SimTime endOfTreaty)
		{
			this.pid = pid;
			this.endOfTreaty = endOfTreaty;
		}
	}

	public class OutpostAgreementEntry
	{
		public PlayerID with;

		public SimTime started;

		public SimTime agreedEnd;

		public OutpostAgreementEntry()
		{
		}

		public OutpostAgreementEntry(PlayerID with, SimTime agreedEnd)
		{
			this.with = with;
			started = Game.ctx.clock.Now;
			this.agreedEnd = agreedEnd;
		}
	}

	public SimTime nextOutpostCheck = SimTime.MIN_DATE;

	public SimTime nextStealCheck = SimTime.MIN_DATE;

	public EntityID startOutpost;

	public EntityID visitOutpost;

	public OutpostToSteal nextOutpostToSteal;

	public NodeID nextNodeToExplore;

	public float nextNodeProbPerTurn;

	public EntityID nextBuildingToScope;

	public float nextBuildingProbPerTurn;

	public EntityID outpostToDefend;

	public List<NonExpansionTreaty> nonExpansionTreaties = new List<NonExpansionTreaty>();

	public List<OutpostAgreementEntry> outpostAgreements = new List<OutpostAgreementEntry>();
}
