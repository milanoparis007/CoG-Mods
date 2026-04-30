using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class GeneratorSettings
{
	public sealed class TraitType
	{
		public string locname;

		public string locdesc;

		public List<Label> traits;
	}

	public IntRange procGenYears;

	public int startDayOfYear;

	public int endYear;

	public int endDayOfYear;

	public int daysPerTurnCityGenMax;

	public int daysPerTurnInteractive;

	public List<TraitType> traitTypes = new List<TraitType>();

	public SimTime GetEndOfGame()
	{
		return new SimTime(endYear, endDayOfYear);
	}
}
