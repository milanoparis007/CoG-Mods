using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Services;

public sealed class SchemeDef
{
	public enum TargetingStyle
	{
		FromPrefixes,
		FindPrecinctsForBlackmail,
		FindPrecincts,
		FindDistantFronts,
		FindBuildingsToTakeOver,
		FindNearbyIndependentPoliticians
	}

	public class SchemeDisplay
	{
		public string loctitle;

		public string locdesc;

		public string locconvo;
	}

	public class SchemeStartup
	{
		public List<Label> targetIdPrefixes = new List<Label>();

		public TargetingStyle alternateTargeting;

		public VisitRequirementList visreqs;

		public ConvoButtonRequirementList reqs;

		public List<ResOrCash> cost = new List<ResOrCash>();

		public Label firstChapter;
	}

	public Label id;

	public SchemeDisplay display;

	public SchemeStartup startup;

	public int cooldownDays;
}
