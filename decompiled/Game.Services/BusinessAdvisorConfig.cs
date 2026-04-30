using Game.Session.Data;

namespace Game.Services;

public sealed class BusinessAdvisorConfig : AIAdvisorConfig
{
	public sealed class TradesConfig
	{
		public ModValue firstCheckDayz;

		public ModValue cooldownDayz;

		public ModValue probPerCheck;

		public ModValue tradeEfficiency;

		public ModValue maxDistanceFromBuilding;

		public ModValue tiedHouseProbability;

		public ModValue tiedHouseCooldownDayz;

		public ModValue reqsPerTurn;
	}

	public sealed class BuildingsConfig
	{
		public ModValue firstCheckDayz;

		public ModValue cooldownDayz;

		public ModValue setupProbPerCheck;

		public ModValue nodesPerControlledBuilding;
	}

	public sealed class CasinosConfig
	{
		public ModValue firstCheckDayz;

		public ModValue cooldownDayz;

		public ModValue setupProbPerCheck;
	}

	public TradesConfig trades;

	public BuildingsConfig buildings;

	public CasinosConfig casinos;
}
