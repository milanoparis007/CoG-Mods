using Game.Session.Data;
using SomaSim.Util;

namespace Game.Services;

public sealed class SafehouseAdvisorConfig : AIAdvisorConfig
{
	public class Skills
	{
		public bool growth;

		public ModValue checkDayzMin;

		public ModValue checkDayzMax;
	}

	public class Donations
	{
		public ModValue firstCheckDayz;

		public ModValue cooldownDayz;

		public ModValue probPerCheck;
	}

	public RandomRange carryCash;

	public RandomRange stashCash;

	public Skills skills;

	public Donations donations;
}
