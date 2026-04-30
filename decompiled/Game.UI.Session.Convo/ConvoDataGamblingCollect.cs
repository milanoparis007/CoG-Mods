using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGamblingCollect : ConvoData
{
	public Price delta;

	public Money current;

	public Money operation;

	public Money vehAmt;

	public EntityID casino;

	public EntityID selected = EntityID.INVALID;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		GamblerState gamblerState = Game.ctx.players.Human.gambling.FindGamblerState(selected);
		return new string[14]
		{
			"delta",
			Loc.Price(delta.Abs),
			"current",
			Loc.Money(current),
			"operation",
			Loc.Money(operation),
			"vehAmount",
			Loc.Money(vehAmt),
			"name",
			selected.FindEntity()?.data.person.FullName ?? "?",
			"lastTurn",
			Loc.Money(gamblerState?.cashDeltaLastTurn.cash ?? ((Fixnum)0)),
			"total",
			Loc.Money(gamblerState?.cash.cash ?? ((Fixnum)0))
		};
	}

	public ConvoDataGamblingCollect()
	{
	}

	public ConvoDataGamblingCollect(Price delta, Money current, Money operation, Money vehAmt, EntityID casino, EntityID selected)
	{
		this.delta = delta;
		this.current = current;
		this.operation = operation;
		this.vehAmt = vehAmt;
		this.casino = casino;
		this.selected = selected;
	}
}
