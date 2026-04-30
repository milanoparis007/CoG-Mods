using Game.Core;
using Game.Services;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public struct GoonRewardInfo
{
	public static GoonRewardInfo NONE = new GoonRewardInfo
	{
		goontype = Label.NULL,
		rewardid = Label.NULL,
		cooldown = SimTime.MIN_DATE,
		expiration = SimTime.MIN_DATE
	};

	public Label goontype;

	public Label rewardid;

	public SimTime cooldown;

	public SimTime expiration;

	public SimTime ready;

	public Price cost;

	public GoonLootTableEntry GetConfig()
	{
		return Game.serv.globals.settings.npc.goons.FindLootTableEntry(goontype, rewardid);
	}

	public static GoonRewardInfo MakeReward(PlayerInfo player, GoonLootTableEntry config)
	{
		ModQuery query = new ModQuery(player.PID);
		Fixnum fixnum = config.cooldowndayz?.Evaluate(query) ?? ((Fixnum)10);
		SimTime simTime = Game.ctx.clock.Now.IncrementDays((int)fixnum);
		Xorshift rng = player.ai.Data.goon.rng;
		Fixnum fixnum2 = config.cashbuyin.Evaluate(query);
		if (config.cashbuyinmultiplier != null)
		{
			fixnum2 *= (Fixnum)rng.Generate(config.cashbuyinmultiplier);
		}
		return new GoonRewardInfo
		{
			cooldown = simTime,
			expiration = SimTime.MIN_DATE,
			ready = SimTime.MIN_DATE,
			cost = new Price(fixnum2.PosCeilingNegFloor()),
			goontype = player.ai.goon.GoonType,
			rewardid = config.id
		};
	}

	public GoonRewardInfo MakeIntoOffered(PlayerID pid)
	{
		int deltaDays = (int)GetConfig().expirationdayz.Evaluate(pid);
		SimTime simTime = Game.ctx.clock.Now.IncrementDays(deltaDays);
		return new GoonRewardInfo
		{
			cooldown = SimTime.MIN_DATE,
			expiration = simTime,
			ready = SimTime.MIN_DATE,
			cost = cost,
			goontype = goontype,
			rewardid = rewardid
		};
	}

	public GoonRewardInfo MakeIntoAccepted(PlayerID pid)
	{
		int deltaDays = (int)GetConfig().readyafterdayz.Evaluate(pid);
		SimTime simTime = Game.ctx.clock.Now.IncrementDays(deltaDays);
		return new GoonRewardInfo
		{
			cooldown = SimTime.MIN_DATE,
			expiration = SimTime.MIN_DATE,
			ready = simTime,
			cost = cost,
			goontype = goontype,
			rewardid = rewardid
		};
	}
}
