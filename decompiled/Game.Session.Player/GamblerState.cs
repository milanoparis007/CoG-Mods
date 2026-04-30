using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player;

public sealed class GamblerState
{
	public EntityID gamblerId = EntityID.INVALID;

	public EntityID gamblingHouseId = EntityID.INVALID;

	public EntityID gamblerResidence = EntityID.INVALID;

	public Money startCash = Money.ZERO;

	public Money cash = Money.ZERO;

	public List<Label> debtsForgivenIds = new List<Label>();

	public Label currentDebtDue = Label.NULL;

	public Price cashDeltaLastTurn = 0;

	public List<RepaymentChoiceAndSuccess> repayments = new List<RepaymentChoiceAndSuccess>();

	public Label repaymentInProgress = Label.NULL;

	public SimTime repaymentDay = SimTime.MAX_DATE;

	public Fixnum rollAmount = 0;

	public bool success = true;

	public bool DebtIsDue => currentDebtDue.IsSet;

	public bool CanKeepGambling => currentDebtDue.IsNotSet;

	public GamblerState()
	{
	}

	public GamblerState(Entity gamblingHouse, Entity gambler, Money startingCash)
	{
		gamblerId = gambler.Id;
		gamblingHouseId = gamblingHouse.Id;
		cash = startingCash;
		startCash = startingCash;
	}

	public Entity FindGambler()
	{
		return gamblerId.FindEntity();
	}

	public Entity FindGamblingHouse()
	{
		return gamblingHouseId.FindEntity();
	}

	public AmenityData FindAmenity()
	{
		foreach (AmenityData amenity in gamblingHouseId.FindEntity().components.modules.gambling.data.amenities)
		{
			if (amenity.gamblers.Contains(gamblerId))
			{
				return amenity;
			}
		}
		Logger.Warning("Gambling house does not contain an amenity with this gambler?");
		return null;
	}

	public void IncrementCash(Price delta)
	{
		cash += delta;
		cashDeltaLastTurn = delta;
	}

	public void ResetLastCashDelta()
	{
		cashDeltaLastTurn = 0;
	}

	public bool WasRepaymentDayReached()
	{
		return repaymentDay <= Game.ctx.clock.Now;
	}
}
