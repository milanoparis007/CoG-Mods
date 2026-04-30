using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class AmenityDef
{
	public sealed class SlotsBehavior
	{
		public ModValue slotCount;

		public ModValue slotFillChance;

		public ModValue slotDropChance;

		public ModValue slotStartingCashMin;

		public ModValue slotStartingCashMax;
	}

	public sealed class RakeBehavior
	{
		public ModValue rakePercent;

		public ModValue rakeEdge;
	}

	public sealed class AmenityBehavior
	{
		public enum BehaviorType
		{
			AOE,
			Slots,
			Rake,
			Mod
		}

		public BehaviorType type;

		public SlotsBehavior slots;

		public RakeBehavior rake;

		public ModValue minBetPerPlayer;

		public ModValue maxBetPerPlayer;

		public ModValue payoutMultiplier;

		public ModValue playerWinChancePercent;

		public ModValue heatPerPlayer;

		public ModValue maxCustomers;
	}

	public Label id;

	public VisitRequirementList visreqs = new VisitRequirementList();

	public VisitRequirementList reqs = new VisitRequirementList();

	public string locicon;

	public string locname;

	public string locdesc;

	public string banner;

	public ModValue buildCost;

	public ModValue buildDayz;

	public ModValue minOperationCost;

	public ModValue recommendedFunds;

	public ModValue weeklyCost;

	public AmenityBehavior behavior;
}
