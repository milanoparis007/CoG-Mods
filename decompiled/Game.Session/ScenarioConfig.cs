using System;
using Game.Core;
using SomaSim.Util;

namespace Game.Session;

public class ScenarioConfig
{
	public GameParameters newgamepars;

	public uint rngseed;

	public uint savefileversion;

	public string mapdef;

	public static ScenarioConfig MakeForNewGame(GameParameters pars)
	{
		ScenarioConfig scenarioConfig = new ScenarioConfig
		{
			rngseed = pars.userrng,
			mapdef = pars.mapdef,
			newgamepars = pars
		};
		Logger.LogAlways($"Starting new game: map id = {pars.mapdef}, random = {scenarioConfig.rngseed}");
		return scenarioConfig;
	}

	public uint MakeSeed(uint salt)
	{
		return HashUtil.Hash(BitwiseUtil.ReverseBits(rngseed ^ salt));
	}

	public uint MakeSeed(PlayerID pid)
	{
		return MakeSeed((uint)pid.id);
	}

	public Xorshift MakeSeededRng(PlayerID pid)
	{
		return new Xorshift(MakeSeed(pid));
	}

	public Xorshift MakeSeededRng(uint salt = 0u)
	{
		return new Xorshift(MakeSeed(salt));
	}

	public Xorshift MakeSeededRng(string salt = "")
	{
		return MakeSeededRng(salt.GetStableHashCode());
	}

	public Xorshift MakeSeededRng(Type t)
	{
		return MakeSeededRng(t.Name.GetStableHashCode());
	}

	public Xorshift MakeSeededRng<T>()
	{
		return MakeSeededRng(typeof(T));
	}

	public static uint MakeRngSeedForSession()
	{
		if (!Game.settings.IsEditor)
		{
			return HashUtil.Hash((uint)DateTime.Now.Ticks);
		}
		return Game.serv.globals.settings.general.debug.forceRandomSeed;
	}
}
