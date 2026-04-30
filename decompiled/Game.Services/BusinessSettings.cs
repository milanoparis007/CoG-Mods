using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class BusinessSettings
{
	public sealed class BuySellIllegalEtc
	{
		public ModValue minrel;

		public string header;

		public string unknown;

		public string unknownmo;

		public string notrust;

		public string notrustmo;

		public string both;

		public string bothmo;
	}

	public sealed class BuyBuilding
	{
		public ModValue minrel;
	}

	public sealed class TiedHouseSettings
	{
		public ModValue durationDayz;

		public ModValue costFromOwner;

		public ModValue costFromGang;

		public ModValue costFromHuman;
	}

	public sealed class ForcedClosure
	{
		public ModValue durationDayz;

		public Label relBuffForOriginator;

		public Label socialForHumanCause;
	}

	public sealed class GlobalModuleModifiers
	{
		public ModValue buildTimeModifier;

		public ModValue buildCostModifier;

		public ModValue destroyCostModifier;
	}

	public List<Label> startingBusinesses;

	public ModValue positiveBlurbsAbove;

	public ModValue negativeBlurbsBelow;

	public BuySellIllegalEtc buySellIllegalEtc;

	public BuyBuilding buyBuilding;

	public TiedHouseSettings tiedHouseSettings;

	public ForcedClosure forcedClosure;

	public GlobalModuleModifiers globalModuleModifiers;
}
