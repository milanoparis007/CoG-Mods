using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGamblingDebtor : ConvoData
{
	public List<RepaymentChoiceAndSuccess> repayments;

	public Fixnum nextCredit;

	public Fixnum success;

	public Fixnum rollMoney;

	public Label selected;

	public EntityID gamblerId;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		PlayerGambling gambling = Game.ctx.players.Human.gambling;
		GamblingSettings gambling2 = Game.serv.globals.settings.gambling;
		Entity gambler = gamblerId.FindEntity();
		GamblerState gamblerState = gambling.FindGamblerState(gambler);
		Fixnum fixnum = 0;
		ModQuery query = gambling.MakeModQueryForGambler(gamblerState);
		Money money = -gamblerState.cash;
		if (selected.IsSet)
		{
			fixnum = gambling2.FindRepaymentById(selected)?.timeDayz?.Evaluate(query) ?? ((Fixnum)0);
		}
		SimTime time = Game.ctx.clock.Now;
		if (gamblerState.repaymentInProgress.IsSet)
		{
			time = gamblerState.repaymentDay;
		}
		return new string[14]
		{
			"newcredit",
			Loc.Money(nextCredit),
			"days",
			fixnum.ToString(),
			"date",
			Loc.FormatDateShort(time),
			"fullname",
			visit.crew.GetPeep().data.person.FullName,
			"name",
			gamblerId.FindEntity().data.person.FullName,
			"amount",
			Loc.Money(money),
			"rollAmount",
			Loc.Money(rollMoney)
		};
	}

	public ConvoDataGamblingDebtor()
	{
	}

	public ConvoDataGamblingDebtor(List<RepaymentChoiceAndSuccess> repayments, Label selected, Fixnum nextCredit, Fixnum success, EntityID gambler, Fixnum rollAmount)
	{
		this.repayments = repayments;
		this.selected = selected;
		this.nextCredit = nextCredit;
		this.success = success;
		gamblerId = gambler;
		rollMoney = rollAmount;
	}
}
