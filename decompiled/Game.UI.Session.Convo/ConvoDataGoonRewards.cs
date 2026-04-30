using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;
using Game.Session.Player.AI;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataGoonRewards : ConvoData
{
	public Price cost;

	public string coststring;

	public string rewardstring;

	public string locdesc;

	public string locexpdate;

	public string locreadydate;

	public Label goontype;

	public Label reward;

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[8] { "cost", coststring, "reward", rewardstring, "expiredate", locexpdate, "readydate", locreadydate };
	}

	public ConvoDataGoonRewards()
	{
	}

	public ConvoDataGoonRewards(PlayerInfo player, VisitState visit)
	{
		GoonRewardInfo active = player.ai.Data.goon.rewards.active;
		goontype = active.goontype;
		reward = active.rewardid;
		cost = active.cost;
		coststring = Loc.Money(cost.Abs.cash);
		locdesc = active.GetConfig().locdesc;
		GoonLootTableEntry goonLootTableEntry = Game.serv.globals.settings.npc.goons.FindLootTableEntry(goontype, reward);
		rewardstring = goonLootTableEntry.grants.Describe(new GrantContext(player.PID, visit, QuestUUID.EMPTY), multiline: false);
		SimTime expiration = active.expiration;
		locexpdate = (expiration.IsMinDate ? "-1" : Loc.FormatDateLong(expiration));
		SimTime ready = active.ready;
		locreadydate = (ready.IsMinDate ? "-1" : Loc.FormatDateLong(ready));
	}
}
