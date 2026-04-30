using Game.Session.Data;

namespace Game.Services;

public sealed class TerritoryAdvisorConfig : AIAdvisorConfig
{
	public class GrowthScores
	{
		public ModValue firstCheckDayz;

		public ModValue cooldownDayz;

		public ModValue cooldownDayzAfterStolen;

		public ModValue basePerNode;

		public ModValue ethBonusPerNode;

		public ModValue newBizBonusPerNode;

		public ModValue tradingBonusPerNode;
	}

	public class StealingScores
	{
		public ModValue firstCheckDayz;

		public ModValue cooldownDayzAfterCheck;

		public ModValue cooldownDayzAfterStolen;

		public ModValue stealingProbEnemy;

		public ModValue stealingProbNonEnemy;

		public ModValue maxDistanceToOurOutpost;

		public ModValue relThresholdNonEnemy;
	}

	public class ExplorationScores
	{
		public ModValue nearDistance;

		public ModValue farDistance;

		public ModValue maxDistance;

		public ModValue nearProb;

		public ModValue farProb;
	}

	public class ScopeOutScores
	{
		public ModValue scopeProb;
	}

	public class ExpansionTreatyDef
	{
		public ModValue lengthDayz;

		public ModValue acceptCost;

		public ModValue acceptTreatyProb;

		public ModValue askForTreatyProb;

		public ModValue askCooldownDayz;

		public int expansionTreatyDistance;
	}

	public sealed class OutpostAgreementDef
	{
		public ModValue lengthDayz;

		public ModValue acceptCost;

		public ModValue acceptOutpostAgreementProb;

		public ModValue askForOutpostAgreementProb;

		public ModValue askForOutpostAgreementMinTerritorySize;
	}

	public GrowthScores growth;

	public StealingScores stealingOutpost;

	public ExplorationScores exploration;

	public ScopeOutScores scopeout;

	public ExpansionTreatyDef expansionTreaty;

	public OutpostAgreementDef outpostAgreementDef;
}
