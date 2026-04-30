using System.Collections.Generic;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public class AmenityData
{
	public struct LastTurnResults
	{
		public Price deltaGamblingIncome;

		public Price deltaGamblingPayouts;

		public Price deltaOperatingCost;

		public int numberOfPlayers;

		public int numberOfPayouts;

		public Price Delta => deltaGamblingIncome + deltaGamblingPayouts + deltaOperatingCost;

		public LastTurnResults AddGamblingResults(int numberOfPlayers, Price deltaGamblingIncome, Price deltaGamblingPayouts, int numberOfPayouts)
		{
			this.numberOfPlayers = numberOfPlayers;
			this.deltaGamblingIncome += deltaGamblingIncome;
			this.deltaGamblingPayouts += deltaGamblingPayouts;
			this.numberOfPayouts += numberOfPayouts;
			return this;
		}
	}

	public AmenityDef.AmenityBehavior.BehaviorType type;

	public List<EntityID> gamblers = new List<EntityID>();

	public int maxGamblers;

	public SimTime enableTime;

	public Label defID;

	public LastTurnResults lastTurnResults;

	public List<LastTurnResults> prevTurnResults = new List<LastTurnResults>();

	public Fixnum cashNeededLastTurn;

	public const int NUM_WEEKS_TRACKED = 12;

	public bool NoCashLastTurn => cashNeededLastTurn > 0;

	public bool IsEnabled(SimTime time)
	{
		return time.days >= enableTime.days;
	}

	public bool WasJustEnabled(SimTime time)
	{
		return ModulesUtil.WasJustEnabled(time, enableTime);
	}

	public AmenityDef GetAmenityDef()
	{
		return Game.serv.globals.settings.gambling.FindAmenityById(defID);
	}

	public void LogTurn(int numberOfPlayers, Price deltaGamblingIncome, Price deltaGamblingPayouts, int numberOfPayouts)
	{
		LastTurnResults item = lastTurnResults.AddGamblingResults(numberOfPlayers, deltaGamblingIncome, deltaGamblingPayouts, numberOfPayouts);
		prevTurnResults.Insert(0, item);
		if (prevTurnResults.Count == 12)
		{
			prevTurnResults.RemoveLast();
		}
	}
}
