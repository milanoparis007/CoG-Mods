using Game.Session.Data;

namespace Game.Services;

public sealed class SocialAdvisorConfig : AIAdvisorConfig
{
	public sealed class KnownByOwners
	{
		public float chancePerOwner;

		public float maxRadius;
	}

	public sealed class InitiativeConfig
	{
		public bool enabled;

		public int timeoutTurns;
	}

	public KnownByOwners knownByNearbyOwners;

	public InitiativeConfig convoInitiative;

	public ModValue wrongFootChance;
}
